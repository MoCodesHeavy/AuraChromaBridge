// Razer Chroma Broadcast: loads the RzChromaBroadcastAPI64.dll that Razer Synapse installs
// and receives the five colours Synapse broadcasts to connected apps.
// Function and struct layouts follow the public RzChromaBroadcastAPI headers.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace AuraChromaBridge
{
    internal sealed class ChromaBroadcast : IDisposable
    {
        public const string DllName = "RzChromaBroadcastAPI64.dll";
        private const string RegKeyPath = @"SOFTWARE\Razer\ChromaBroadcast";
        private const uint LoadWithAlteredSearchPath = 0x00000008;

        // RZRESULT Init(RZAPPID app)
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int InitFn(Guid appId);

        // RZRESULT UnInit() / UnRegisterEventNotification()
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int NoArgFn();

        // RZRESULT RegisterEventNotification(RZEVENTNOTIFICATIONCALLBACK callback)
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int RegisterFn(EventCallback callback);

        // RZRESULT (*RZEVENTNOTIFICATIONCALLBACK)(CHROMA_BROADCAST_TYPE type /* BYTE */, PRZPARAM pData)
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int EventCallback(byte type, IntPtr data);

        [DllImport("kernel32.dll", EntryPoint = "LoadLibraryW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string fileName);

        [DllImport("kernel32.dll", EntryPoint = "LoadLibraryExW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr LoadLibraryEx(string fileName, IntPtr reserved, uint flags);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr module, string procName);

        [DllImport("kernel32.dll", EntryPoint = "GetModuleFileNameW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        private static extern uint GetModuleFileName(IntPtr module, StringBuilder fileName, int size);

        private IntPtr _module;
        private InitFn _init;
        private NoArgFn _uninit;
        private RegisterFn _register;
        private NoArgFn _unregister;
        private EventCallback _callback;   // kept in a field so the GC never collects it while Razer holds the pointer
        private bool _initialized;
        private bool _registered;

        /// <summary>Five colours (CL1..CL5), each 0x00BBGGRR. Called on Razer's thread - keep handlers quick.</summary>
        public event Action<int[]> ColorsReceived;

        /// <summary>True when Synapse starts sending, false when it stops.</summary>
        public event Action<bool> LiveChanged;

        public string DllPath { get; private set; }

        public bool Load(out string error)
        {
            List<string> tried = new List<string>();
            foreach (string candidate in CandidatePaths())
            {
                IntPtr module = Path.IsPathRooted(candidate)
                    ? LoadLibraryEx(candidate, IntPtr.Zero, LoadWithAlteredSearchPath)
                    : LoadLibrary(candidate);
                if (module == IntPtr.Zero)
                {
                    tried.Add(string.Format(CultureInfo.InvariantCulture, "{0} (Windows error {1})", candidate, Marshal.GetLastWin32Error()));
                    continue;
                }

                _module = module;
                DllPath = ModulePath(module) ?? candidate;

                string missing = null;
                _init = Export<InitFn>("Init", ref missing);
                _uninit = Export<NoArgFn>("UnInit", ref missing);
                _register = Export<RegisterFn>("RegisterEventNotification", ref missing);
                _unregister = Export<NoArgFn>("UnRegisterEventNotification", ref missing);
                if (missing != null)
                {
                    error = DllPath + " is missing the function(s): " + missing;
                    return false;
                }

                error = null;
                return true;
            }

            error = "Could not load " + DllName + ". Tried:" + Environment.NewLine + "  " +
                    string.Join(Environment.NewLine + "  ", tried.ToArray());
            return false;
        }

        public int Init(Guid appId)
        {
            int result = _init(appId);
            _initialized = result == 0;
            return result;
        }

        public int Register()
        {
            _callback = OnEvent;
            NativeCallbacks.Keep(_callback);   // Razer may call it even after this object is gone
            int result = _register(_callback);
            _registered = result == 0;
            return result;
        }

        private int OnEvent(byte type, IntPtr data)
        {
            try
            {
                if (type == 1)
                {
                    // CHROMA_BROADCAST_EFFECT { RZCOLOR CL1..CL5; BOOL IsAppSpecific; } (packed, 24 bytes)
                    if (data == IntPtr.Zero) return 0;
                    int[] colors = new int[5];
                    for (int i = 0; i < 5; i++) colors[i] = Marshal.ReadInt32(data, i * 4) & 0xFFFFFF;
                    Action<int[]> colorHandler = ColorsReceived;
                    if (colorHandler != null) colorHandler(colors);
                }
                else if (type == 2)
                {
                    // For status messages the pointer value itself is the status: 1 = LIVE, 2 = NOT_LIVE.
                    long status = data.ToInt64();
                    Action<bool> liveHandler = LiveChanged;
                    if (liveHandler != null) liveHandler(status == 1);
                }
            }
            catch (Exception)
            {
                // Never let an exception cross back into Razer's native code.
            }
            return 0;
        }

        public void Dispose()
        {
            try
            {
                if (_registered)
                {
                    int r = _unregister();
                    if (r != 0) Log.Line("Feed: Razer's UnRegisterEventNotification returned {0} = {1}.", r, Describe(r));
                }
            }
            catch (Exception)
            {
            }
            _registered = false;

            try
            {
                if (_initialized)
                {
                    int r = _uninit();
                    if (r != 0) Log.Line("Feed: Razer's UnInit returned {0} = {1}.", r, Describe(r));
                }
            }
            catch (Exception)
            {
            }
            _initialized = false;
            // The DLL stays loaded on purpose: its own threads may still be winding down.
        }

        public static string Describe(int code)
        {
            switch (code)
            {
                case 0: return "OK";
                case -1: return "INVALID";
                case 5: return "ACCESS_DENIED - Synapse did not accept this app ID";
                case 6: return "INVALID_HANDLE";
                case 12: return "UNKNOWN_APPID - Synapse did not accept this app ID";
                case 50: return "NOT_SUPPORTED";
                case 87: return "INVALID_PARAMETER";
                case 1062: return "SERVICE_NOT_ACTIVE - Razer's service isn't running";
                case 1152: return "SINGLE_INSTANCE_APP - another copy is already connected";
                case 1168: return "NOT_FOUND - the Chroma Broadcast part of Synapse isn't installed";
                case 1247: return "ALREADY_INITIALIZED";
                case 4309: return "RESOURCE_DISABLED - Razer test app IDs are switched off on this PC";
                case 5023: return "NOT_VALID_STATE";
                default:
                    if (code == unchecked((int)0x80004005)) return "FAILED";
                    return "unrecognised code";
            }
        }

        /// <summary>Logs Razer's broadcast settings from the registry - useful when nothing arrives.</summary>
        public static void LogRegistry()
        {
            bool found = false;
            foreach (RegistryView view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
            {
                try
                {
                    using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                    using (RegistryKey key = hklm.OpenSubKey(RegKeyPath))
                    {
                        if (key == null) continue;
                        found = true;
                        Log.Line("Razer broadcast settings ({0}-bit registry): Enable={1}  InstallPath={2}  DataPath={3}",
                            view == RegistryView.Registry32 ? 32 : 64, Value(key, "Enable"), Value(key, "InstallPath"), Value(key, "DataPath"));
                        foreach (string name in key.GetSubKeyNames())
                        {
                            using (RegistryKey app = key.OpenSubKey(name))
                            {
                                if (app == null) continue;
                                Log.Line("  connected app \"{0}\": Enable={1}  Index={2}  Path={3}",
                                    Value(app, "Title") ?? name, Value(app, "Enable"), Value(app, "Index"), Value(app, "Path"));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Line("Could not read Razer broadcast registry settings: {0}", ex.Message);
                }
            }
            if (!found) Log.Line("Razer broadcast registry key HKLM\\{0} not found.", RegKeyPath);
        }

        /// <summary>Razer's DLL keeps its own log named after our exe; show its last lines.</summary>
        public static void LogRazerLogTail(int maxLines)
        {
            try
            {
                string installPath = RegString("InstallPath");
                if (string.IsNullOrEmpty(installPath)) return;
                string exeName = Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().Location);
                string path = Path.Combine(Path.Combine(installPath, "Logs"), exeName + ".log");
                if (!File.Exists(path))
                {
                    Log.Line("(No Razer log at {0})", path);
                    return;
                }

                List<string> lines = new List<string>();
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (StreamReader reader = new StreamReader(fs))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        lines.Add(line);
                        if (lines.Count > maxLines) lines.RemoveAt(0);
                    }
                }
                Log.Line("Last lines of Razer's own log ({0}):", path);
                foreach (string l in lines) Log.Line("  razer: {0}", l);
            }
            catch (Exception ex)
            {
                Log.Line("(Could not read Razer's log: {0})", ex.Message);
            }
        }

        private T Export<T>(string name, ref string missing) where T : class
        {
            IntPtr p = GetProcAddress(_module, name);
            if (p == IntPtr.Zero)
            {
                missing = missing == null ? name : missing + ", " + name;
                return null;
            }
            return Marshal.GetDelegateForFunctionPointer(p, typeof(T)) as T;
        }

        private static List<string> CandidatePaths()
        {
            List<string> list = new List<string>();
            list.Add(DllName);   // the normal Windows search order (System32 and so on)

            List<string> roots = new List<string>();
            string installPath = RegString("InstallPath");
            if (!string.IsNullOrEmpty(installPath)) roots.Add(installPath);
            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            roots.Add(Path.Combine(pf, @"Razer\ChromaBroadcast"));
            roots.Add(Path.Combine(pf, "Razer Chroma SDK"));
            roots.Add(Path.Combine(pf86, "Razer Chroma SDK"));
            roots.Add(Path.Combine(pf, "Razer"));
            roots.Add(Path.Combine(pf86, "Razer"));

            int budget = 5000;   // directories to look in at most
            foreach (string root in roots) FindFile(root, DllName, 4, list, ref budget);

            List<string> unique = new List<string>();
            foreach (string p in list)
            {
                bool seen = false;
                foreach (string u in unique)
                {
                    if (string.Equals(u, p, StringComparison.OrdinalIgnoreCase)) { seen = true; break; }
                }
                if (!seen) unique.Add(p);
            }
            return unique;
        }

        private static void FindFile(string dir, string fileName, int depth, List<string> results, ref int budget)
        {
            if (depth < 0 || budget <= 0 || string.IsNullOrEmpty(dir)) return;
            budget--;
            try
            {
                if (!Directory.Exists(dir)) return;
                string candidate = Path.Combine(dir, fileName);
                if (File.Exists(candidate)) results.Add(candidate);
                foreach (string sub in Directory.GetDirectories(dir))
                {
                    FindFile(sub, fileName, depth - 1, results, ref budget);
                    if (budget <= 0) return;
                }
            }
            catch (Exception)
            {
                // No access to this folder; skip it.
            }
        }

        private static string RegString(string valueName)
        {
            foreach (RegistryView view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
            {
                try
                {
                    using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                    using (RegistryKey key = hklm.OpenSubKey(RegKeyPath))
                    {
                        if (key == null) continue;
                        object v = key.GetValue(valueName);
                        if (v != null) return v.ToString();
                    }
                }
                catch (Exception)
                {
                }
            }
            return null;
        }

        private static string Value(RegistryKey key, string name)
        {
            object v = key.GetValue(name);
            return v == null ? null : v.ToString();
        }

        private static string ModulePath(IntPtr module)
        {
            StringBuilder sb = new StringBuilder(1024);
            uint n = GetModuleFileName(module, sb, sb.Capacity);
            return n == 0 ? null : sb.ToString();
        }
    }

    /// <summary>
    /// Callbacks handed to Razer's DLL, kept for the life of the process. Razer holds a raw pointer, and its threads
    /// may still call it while winding down after we unregister; if .NET collected the delegate by then, that call
    /// would crash the bridge with nothing able to catch it. A few bytes per reconnect.
    /// </summary>
    internal static class NativeCallbacks
    {
        private static readonly List<Delegate> Kept = new List<Delegate>();

        public static void Keep(Delegate callback)
        {
            if (callback == null) return;
            lock (Kept) Kept.Add(callback);
        }
    }
}
