// Razer Chroma SDK (the public app-side SDK): sets colours on Razer devices.
// Loads the RzChromaSDK64.dll that Synapse installs. Layouts follow Razer's RzChromaSDKTypes.h.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace AuraChromaBridge
{
    internal sealed class RazerSdk : IDisposable
    {
        public const string DllName = "RzChromaSDK64.dll";
        public const int Categories = 6;
        private const uint LoadWithAlteredSearchPath = 0x00000008;

        // Indexes into the per-category arrays below.
        private static readonly string[] CategoryNames = { "keyboard", "mouse", "headset", "mousepad / stand", "keypad", "Chroma Link" };
        private static readonly string[] CreateExports =
        {
            "CreateKeyboardEffect", "CreateMouseEffect", "CreateHeadsetEffect",
            "CreateMousepadEffect", "CreateKeypadEffect", "CreateChromaLinkEffect"
        };

        // CHROMA_STATIC in each device namespace: Keyboard 4, Mouse 6, Headset 1, Mousepad 4, Keypad 5, ChromaLink 2.
        private static readonly int[] StaticEffect = { 4, 6, 1, 4, 5, 2 };
        private const int MouseAllLeds = 0xFFFF;   // ChromaSDK::Mouse::RZLED_ALL

        // CHROMA_CUSTOM per device and how many colours its custom struct holds:
        // Keyboard CUSTOM 2: Color[6][22], Mouse CUSTOM2 8: Color[9][7], Headset CUSTOM 4: Color[5],
        // Mousepad CUSTOM 2: Color[15], Keypad CUSTOM 2: Color[4][5], ChromaLink CUSTOM 1: Color[5].
        private static readonly int[] CustomEffect = { 2, 8, 4, 2, 2, 1 };
        private static readonly int[] CustomLeds = { 132, 63, 5, 15, 20, 5 };

        // razer_style = auto: newer headsets (e.g. Kraken V4) ignore the static command, so headsets and
        // Chroma Link get the per-LED custom command; everything else keeps the static command.
        private static readonly bool[] AutoCustom = { false, false, true, false, false, true };

        // ChromaSDK::APPINFOTYPE
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct AppInfo
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Title;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1024)] public string Description;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string AuthorName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string AuthorContact;
            public uint SupportedDevice;   // 0x01 keyboard | 0x02 mouse | 0x04 headset | 0x08 mousepad | 0x10 keypad | 0x20 ChromaLink
            public uint Category;          // 0x01 utility, 0x02 game
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int InitSdkFn(ref AppInfo info);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int NoArgFn();

        // RZRESULT Create<Device>Effect(<Device>::EFFECT_TYPE Effect, PRZPARAM pParam, RZEFFECTID *pEffectId)
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int CreateFn(int effect, IntPtr param, IntPtr effectId);

        [DllImport("kernel32.dll", EntryPoint = "LoadLibraryW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string fileName);

        [DllImport("kernel32.dll", EntryPoint = "LoadLibraryExW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr LoadLibraryEx(string fileName, IntPtr reserved, uint flags);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr module, string procName);

        [DllImport("kernel32.dll", EntryPoint = "GetModuleFileNameW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        private static extern uint GetModuleFileName(IntPtr module, StringBuilder fileName, int size);

        private readonly CreateFn[] _create = new CreateFn[Categories];
        private readonly int[] _results = new int[Categories];
        private readonly bool[] _usedCustom = new bool[Categories];
        private readonly long[] _skipUntil = new long[Categories];   // monotonic ms; 0 = don't skip
        private IntPtr _module;
        private InitSdkFn _initSdk;
        private NoArgFn _init;
        private NoArgFn _uninit;
        private IntPtr _param;
        private bool _initialized;
        private int _mask = 0x3F;   // which device kinds this app claims: bit i = category i

        public string DllPath { get; private set; }

        public bool IsInitialized
        {
            get { return _initialized; }
        }

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
                StringBuilder sb = new StringBuilder(1024);
                DllPath = GetModuleFileName(module, sb, sb.Capacity) == 0 ? candidate : sb.ToString();

                _initSdk = Export<InitSdkFn>("InitSDK");
                _init = Export<NoArgFn>("Init");
                _uninit = Export<NoArgFn>("UnInit");
                for (int i = 0; i < Categories; i++) _create[i] = Export<CreateFn>(CreateExports[i]);

                if ((_initSdk == null && _init == null) || _uninit == null || _create[0] == null)
                {
                    error = DllPath + " doesn't have the expected Chroma SDK functions.";
                    return false;
                }
                error = null;
                return true;
            }

            error = "Could not load " + DllName + ". Tried:" + Environment.NewLine + "  " +
                    string.Join(Environment.NewLine + "  ", tried.ToArray());
            return false;
        }

        /// <summary>Bit for each device kind, in category order: keyboard, mouse, headset, mousepad, keypad, Chroma Link.</summary>
        public static int MaskFor(string[] skip)
        {
            int mask = 0x3F;
            foreach (string raw in skip)
            {
                string k = raw.Trim().ToLowerInvariant().Replace(" ", "");
                if (k == "all" || k == "everything" || k == "razer") return 0;   // ASUS-only mode: leave every Razer device to Synapse
                if (k == "keyboard" || k == "keyboards") mask &= ~0x01;
                else if (k == "mouse" || k == "mice") mask &= ~0x02;
                else if (k == "headset" || k == "headsets" || k == "headphones") mask &= ~0x04;
                else if (k == "mousepad" || k == "mousepads" || k == "stand" || k == "dock") mask &= ~0x08;
                else if (k == "keypad" || k == "keypads") mask &= ~0x10;
                else if (k == "chromalink") mask &= ~0x20;
            }
            return mask;
        }

        /// <summary>
        /// Registers with Synapse. The app shows up in Synapse's Chroma Apps list under this title, and only claims
        /// the device kinds in mask - kinds left out keep their own Synapse lighting.
        /// </summary>
        public int Init(int mask)
        {
            _mask = mask & 0x3F;
            int result;
            if (_initSdk != null)
            {
                AppInfo info = new AppInfo();
                info.Title = "AuraChromaBridge";
                info.Description = "Syncs Razer Chroma devices with ASUS Aura case lighting.";
                info.AuthorName = "MoCodesHeavy";
                info.AuthorContact = "https://github.com/MoCodesHeavy";
                info.SupportedDevice = (uint)_mask;
                info.Category = 0x01;
                result = _initSdk(ref info);
            }
            else
            {
                result = _init();
            }

            _initialized = result == 0;
            if (_initialized && _param == IntPtr.Zero) _param = Marshal.AllocHGlobal(1024);   // largest custom struct is 132 colours
            for (int i = 0; i < Categories; i++) _skipUntil[i] = 0;
            return result;
        }

        /// <summary>
        /// Sets every Razer device category to one colour (0x00BBGGRR).
        /// style: "auto" (custom command for headsets and Chroma Link, static for the rest), "static" or "custom".
        /// Returns true if at least one category accepted it.
        /// </summary>
        public bool SetAll(int bgr, string style)
        {
            if (!_initialized) return false;
            int color = bgr & 0xFFFFFF;
            long now = Stopwatch.GetTimestamp() / (Stopwatch.Frequency / 1000);
            bool any = false;
            for (int i = 0; i < Categories; i++)
            {
                if (_create[i] == null) continue;
                if ((_mask & (1 << i)) == 0) continue;
                if (_skipUntil[i] != 0 && now < _skipUntil[i]) continue;

                bool custom = style == "custom" || (style != "static" && AutoCustom[i]);
                int r = custom ? SendCustom(i, color) : SendStatic(i, color);
                if (custom && r != 0 && r != 1167 && r != 4319)
                {
                    // This device doesn't take the custom command - fall back to static.
                    r = SendStatic(i, color);
                    custom = false;
                }

                _results[i] = r;
                _usedCustom[i] = custom;
                if (r == 0)
                {
                    any = true;
                    _skipUntil[i] = 0;
                }
                else if (r == 1167 || r == 4319)
                {
                    // Nothing of this kind is plugged in - check again in 10 seconds.
                    _skipUntil[i] = now + 10000;
                }
            }
            return any;
        }

        private int SendStatic(int i, int color)
        {
            if (i == 1)
            {
                // Mouse::STATIC_EFFECT_TYPE { RZLED LEDId; COLORREF Color; }
                Marshal.WriteInt32(_param, 0, MouseAllLeds);
                Marshal.WriteInt32(_param, 4, color);
            }
            else
            {
                // { COLORREF Color; }
                Marshal.WriteInt32(_param, 0, color);
            }
            return _create[i](StaticEffect[i], _param, IntPtr.Zero);
        }

        private int SendCustom(int i, int color)
        {
            int n = CustomLeds[i];
            for (int k = 0; k < n; k++) Marshal.WriteInt32(_param, k * 4, color);
            return _create[i](CustomEffect[i], _param, IntPtr.Zero);
        }

        /// <summary>Per-category result of the last SetAll, for the log.</summary>
        public string DescribeLastResults()
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < Categories; i++)
            {
                if (_create[i] == null) continue;
                if (sb.Length > 0) sb.Append(Environment.NewLine);
                if ((_mask & (1 << i)) == 0)
                {
                    sb.AppendFormat(CultureInfo.InvariantCulture, "    {0,-17} left alone (razer_skip)", CategoryNames[i] + ":");
                    continue;
                }
                sb.AppendFormat(CultureInfo.InvariantCulture, "    {0,-17} {1} ({2} command)", CategoryNames[i] + ":", Describe(_results[i]),
                    _usedCustom[i] ? "custom" : "static");
            }
            return sb.ToString();
        }

        public void Dispose()
        {
            if (_initialized)
            {
                try
                {
                    _uninit();
                }
                catch (Exception)
                {
                }
                _initialized = false;
            }
            if (_param != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_param);
                _param = IntPtr.Zero;
            }
            // The DLL itself stays loaded; Razer's own threads may still be winding down.
        }

        public static string Describe(int code)
        {
            switch (code)
            {
                case 0: return "OK";
                case -1: return "INVALID";
                case 5: return "ACCESS_DENIED";
                case 6: return "INVALID_HANDLE";
                case 50: return "NOT_SUPPORTED";
                case 87: return "INVALID_PARAMETER";
                case 259: return "NO_MORE_ITEMS";
                case 1062: return "SERVICE_NOT_ACTIVE - Razer's Chroma service isn't running";
                case 1152: return "SINGLE_INSTANCE_APP";
                case 1167: return "DEVICE_NOT_CONNECTED - none of this kind plugged in";
                case 1168: return "NOT_FOUND";
                case 1235: return "REQUEST_ABORTED";
                case 1247: return "ALREADY_INITIALIZED";
                case 4309: return "RESOURCE_DISABLED - Chroma Apps may be switched off in Synapse";
                case 4319: return "DEVICE_NOT_AVAILABLE";
                case 5023: return "NOT_VALID_STATE";
                default:
                    if (code == unchecked((int)0x80004005)) return "FAILED";
                    return "code " + code.ToString(CultureInfo.InvariantCulture);
            }
        }

        private T Export<T>(string name) where T : class
        {
            IntPtr p = GetProcAddress(_module, name);
            return p == IntPtr.Zero ? null : Marshal.GetDelegateForFunctionPointer(p, typeof(T)) as T;
        }

        private static List<string> CandidatePaths()
        {
            List<string> list = new List<string>();
            list.Add(DllName);   // normal Windows search order - Synapse puts it in System32
            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            foreach (string dir in new[] { Path.Combine(pf, @"Razer Chroma SDK\bin"), Path.Combine(pf86, @"Razer Chroma SDK\bin") })
            {
                string candidate = Path.Combine(dir, DllName);
                if (File.Exists(candidate)) list.Add(candidate);
            }
            return list;
        }
    }
}
