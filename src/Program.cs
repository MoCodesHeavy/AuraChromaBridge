// AuraChromaBridge - one lighting effect, shown in sync on Razer Chroma devices and ASUS Aura (motherboard ARGB headers).
// Written in C# 5 so it builds with the compiler that ships inside Windows (.NET Framework 4.x).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Principal;
using System.Text;

namespace AuraChromaBridge
{
    internal static class Program
    {
        public const string Version = "0.7.0";

        private static string _dataDir;

        /// <summary>The arguments this copy was started with (used to start a fresh copy).</summary>
        public static string[] Args = new string[0];

        /// <summary>How many times the background copy has restarted itself (relaunch=N), 0 for a normal start.</summary>
        public static int RelaunchCount;

        /// <summary>
        /// Folder with settings.ini, logs and stop.request. Defaults to the exe's folder;
        /// the installed copy is started with data="..." pointing at the project folder.
        /// </summary>
        public static string DataDir
        {
            get { return _dataDir ?? AppDomain.CurrentDomain.BaseDirectory; }
        }

        public static string SettingsPath
        {
            get { return Path.Combine(DataDir, "settings.ini"); }
        }

        [STAThread]
        private static int Main(string[] args)
        {
            string mode = args.Length > 0 ? args[0].Trim().ToLowerInvariant() : "run";
            if (mode.StartsWith("--")) mode = mode.Substring(2);
            if (mode == "bridge") mode = "run";
            if (mode != "aura-test" && mode != "identify" && mode != "razer-test" && mode != "run" && mode != "scan")
            {
                Console.WriteLine("Usage: AuraChromaBridge.exe [run | scan [all] | aura-test | identify | razer-test] [key=value ...]");
                return 2;
            }

            Args = args;
            for (int i = 1; i < args.Length; i++)
            {
                if (args[i].StartsWith("data=", StringComparison.OrdinalIgnoreCase))
                {
                    string dir = args[i].Substring(5).Trim().Trim('"');
                    if (dir.Length > 0) _dataDir = dir;
                }
                else if (args[i].StartsWith("relaunch=", StringComparison.OrdinalIgnoreCase))
                {
                    int n;
                    if (int.TryParse(args[i].Substring(9).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out n) && n > 0) RelaunchCount = n;
                }
            }

            try
            {
                Log.Open(mode, RelaunchCount > 0);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Could not open the log file: " + ex.Message);
            }

            try
            {
                if (RelaunchCount > 0)
                {
                    Log.Line("");
                    Log.Line("Started again after a stuck ASUS write (restart {0} of {1}).", RelaunchCount, Relauncher.MaxRelaunches);
                }
                Log.Line("AuraChromaBridge {0} - {1} (data folder: {2})", Version, mode, DataDir);
                Log.Line("Windows {0} | 64-bit process: {1} | admin: {2}",
                    Environment.OSVersion.Version, Environment.Is64BitProcess ? "yes" : "no", IsAdmin() ? "yes" : "NO");
                if (!IsAdmin())
                {
                    Log.Line("WARNING: not running as administrator. ASUS only reports its devices to admin programs.");
                }

                Settings settings = Settings.Load(SettingsPath);
                for (int i = 1; i < args.Length; i++)
                {
                    int eq = args[i].IndexOf('=');
                    if (eq <= 0) continue;
                    string key = args[i].Substring(0, eq).Trim();
                    if (key.Equals("data", StringComparison.OrdinalIgnoreCase)) continue;
                    if (key.Equals("relaunch", StringComparison.OrdinalIgnoreCase)) continue;
                    settings.Set(key, args[i].Substring(eq + 1).Trim());
                }
                Log.Line("Settings: {0}", settings);
                Log.Line("");

                if (mode == "aura-test") return AuraTest.Run(settings);
                if (mode == "identify") return IdentifyTest.Run(settings);
                if (mode == "razer-test") return RazerTest.Run(settings);
                if (mode == "scan")
                {
                    bool all = false;
                    for (int i = 1; i < args.Length; i++)
                    {
                        if (args[i].Trim().Equals("all", StringComparison.OrdinalIgnoreCase)) all = true;
                    }
                    return Scan.Run(settings, all);
                }
                return Sync.Run(settings);
            }
            catch (Exception ex)
            {
                Log.Line("FATAL: {0}", ex);
                return 1;
            }
            finally
            {
                if (Log.FilePath != null) Log.Line("Log saved to {0}", Log.FilePath);
                Log.Close();
            }
        }

        internal static bool IsAdmin()
        {
            try
            {
                using (WindowsIdentity id = WindowsIdentity.GetCurrent())
                {
                    return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static string Describe(Exception ex)
        {
            System.Runtime.InteropServices.COMException com = ex as System.Runtime.InteropServices.COMException;
            if (com != null) return string.Format("{0} (HRESULT 0x{1:X8})", com.Message.Trim(), com.ErrorCode);
            return ex.GetType().Name + ": " + ex.Message;
        }
    }

    /// <summary>Writes every line to the console and to logs\&lt;mode&gt;.log.</summary>
    internal static class Log
    {
        private static readonly object Gate = new object();
        private static StreamWriter _file;

        public static string FilePath { get; private set; }

        /// <summary>Opens logs\&lt;name&gt;.log. append = keep what's there (a restarted copy keeps the reason it restarted).</summary>
        public static void Open(string name, bool append)
        {
            string dir = Path.Combine(Program.DataDir, "logs");
            Directory.CreateDirectory(dir);
            OpenFile(Path.Combine(dir, name + ".log"), append);
        }

        public static void OpenFile(string path, bool append)
        {
            lock (Gate)
            {
                if (_file != null) _file.Dispose();
                FilePath = path;
                FileStream stream = new FileStream(path, append ? FileMode.Append : FileMode.Create, FileAccess.Write,
                    FileShare.ReadWrite | FileShare.Delete);
                _file = new StreamWriter(stream, new UTF8Encoding(false));
                _file.AutoFlush = true;
            }
        }

        public static void Line(string format, params object[] args)
        {
            string text = (args == null || args.Length == 0) ? format : string.Format(CultureInfo.InvariantCulture, format, args);
            lock (Gate)
            {
                Console.WriteLine(text);
                if (_file != null)
                {
                    try
                    {
                        string stamp = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
                        foreach (string part in text.Split('\n')) _file.WriteLine(stamp + "  " + part.TrimEnd('\r'));
                    }
                    catch (Exception)
                    {
                        // A full disk shouldn't stop the lights.
                    }
                }
            }
        }

        public static void Close()
        {
            lock (Gate)
            {
                if (_file != null)
                {
                    _file.Dispose();
                    _file = null;
                }
            }
        }
    }

    /// <summary>Settings from settings.ini (key = value, ; starts a comment).</summary>
    internal sealed class Settings
    {
        public static readonly string[] EffectNames = { "static", "breathing", "spectrum", "wave" };
        public static readonly string[] RazerStyles = { "auto", "static", "custom" };

        public string Effect = "spectrum";
        public int Color = Rgb.Make(0, 160, 255);
        public double Speed = 10;
        public double Phase = 0;
        public bool DsxEnabled = false;
        public int DsxPort = 6969;
        public int Brightness = 100;
        public int AsusBrightness = 100;
        public int MaxFps = 30;
        public string RazerStyle = "auto";
        public string[] RazerSkip = new string[0];
        public string[] Only = new string[0];
        public string[] Skip = new string[0];
        public AppIdMode FeedMode = AppIdMode.Off;
        public Guid FeedAppId = Guid.Empty;
        public string ClockAnchor = "fixed";

        public static Settings Load(string path)
        {
            Settings s = new Settings();
            if (!File.Exists(path))
            {
                Log.Line("No settings.ini next to the exe - using defaults.");
                return s;
            }

            string[] lines;
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (StreamReader reader = new StreamReader(fs))
            {
                List<string> list = new List<string>();
                string l;
                while ((l = reader.ReadLine()) != null) list.Add(l);
                lines = list.ToArray();
            }

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                int comment = line.IndexOf(';');
                if (comment >= 0) line = line.Substring(0, comment);
                line = line.Trim();
                if (line.Length == 0 || line.StartsWith("[")) continue;

                int eq = line.IndexOf('=');
                if (eq <= 0)
                {
                    Log.Line("settings.ini line {0} ignored (no '='): {1}", i + 1, lines[i]);
                    continue;
                }
                s.Set(line.Substring(0, eq).Trim(), line.Substring(eq + 1).Trim());
            }
            return s;
        }

        public void Set(string key, string value)
        {
            switch (key.ToLowerInvariant())
            {
                case "effect":
                    string e = value.ToLowerInvariant();
                    if (Array.IndexOf(EffectNames, e) >= 0) Effect = e;
                    else Log.Line("settings: effect must be one of {0} - keeping '{1}'", string.Join(", ", EffectNames), Effect);
                    break;
                case "color":
                case "colour":
                    int c;
                    if (Rgb.TryParse(value, out c)) Color = c;
                    else Log.Line("settings: color '{0}' isn't #RRGGBB or a colour name - keeping {1}", value, Rgb.Hex(Color));
                    break;
                case "speed":
                    double sp;
                    if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out sp) && sp >= 0.5 && sp <= 600) Speed = sp;
                    else Log.Line("settings: speed must be seconds per cycle from 0.5 to 600 - keeping {0}", Speed);
                    break;
                case "dsx":
                    string dv = value.Trim().ToLowerInvariant();
                    DsxEnabled = dv == "on" || dv == "yes" || dv == "true" || dv == "1";
                    break;
                case "dsx_port":
                    DsxPort = ParseInt(key, value, 1, 65535, DsxPort);
                    break;
                case "phase":
                    double ph;
                    if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out ph) && ph >= -600 && ph <= 600) Phase = ph;
                    else Log.Line("settings: phase must be seconds from -600 to 600 - keeping {0}", Phase);
                    break;
                case "brightness":
                    Brightness = ParseInt(key, value, 1, 100, Brightness);
                    break;
                case "asus_brightness":
                    AsusBrightness = ParseInt(key, value, 1, 100, AsusBrightness);
                    break;
                case "maxfps":
                    MaxFps = ParseInt(key, value, 1, 60, MaxFps);
                    break;
                case "razer_style":
                    string rs = value.ToLowerInvariant();
                    if (Array.IndexOf(RazerStyles, rs) >= 0) RazerStyle = rs;
                    else Log.Line("settings: razer_style must be one of {0} - keeping '{1}'", string.Join(", ", RazerStyles), RazerStyle);
                    break;
                case "razer_skip":
                    RazerSkip = SplitList(value);
                    break;
                case "only":
                    Only = SplitList(value);
                    break;
                case "skip":
                    Skip = SplitList(value);
                    break;
                case "razer_app_id":
                    string fv = value.Trim().ToLowerInvariant();
                    Guid feedId;
                    if (fv == "off") FeedMode = AppIdMode.Off;
                    else if (fv == "auto") FeedMode = AppIdMode.Auto;
                    else if (Guid.TryParse(fv, out feedId))
                    {
                        FeedMode = AppIdMode.Pinned;
                        FeedAppId = feedId;
                    }
                    else Log.Line("settings: razer_app_id must be off, auto or an app ID like {{4C24AD2B-D0A6-409F-B895-EC7632F3BEE6}} - keeping {0}", FeedKey);
                    break;
                case "clock_anchor":
                    string ca = value.Trim().ToLowerInvariant();
                    if (ca == "fixed" || ca == "unlock") ClockAnchor = ca;
                    else Log.Line("settings: clock_anchor must be fixed or unlock - keeping '{0}'", ClockAnchor);
                    break;
                default:
                    Log.Line("settings: unknown key '{0}' ignored", key);
                    break;
            }
        }

        /// <summary>True if this ASUS device should be controlled, based on only/skip.</summary>
        public bool Includes(string deviceName)
        {
            string name = deviceName ?? "";
            if (Only.Length > 0)
            {
                bool match = false;
                foreach (string o in Only)
                {
                    if (name.IndexOf(o, StringComparison.OrdinalIgnoreCase) >= 0) { match = true; break; }
                }
                if (!match) return false;
            }
            foreach (string sk in Skip)
            {
                if (name.IndexOf(sk, StringComparison.OrdinalIgnoreCase) >= 0) return false;
            }
            return true;
        }

        /// <summary>Razer device kinds the bridge claims (see RazerSdk.MaskFor).</summary>
        public int RazerMask
        {
            get { return RazerSdk.MaskFor(RazerSkip); }
        }

        /// <summary>
        /// Razer device kinds the sync drives: none while razer_app_id is on, because the feed follows Synapse and
        /// would otherwise receive the bridge's own colours back. The tests still use RazerMask.
        /// </summary>
        public int EffectiveRazerMask
        {
            get { return FeedMode == AppIdMode.Off ? RazerMask : 0; }
        }

        /// <summary>Text that changes whenever razer_app_id changes: off, auto, or the pinned ID.</summary>
        public string FeedKey
        {
            get
            {
                switch (FeedMode)
                {
                    case AppIdMode.Auto: return "auto";
                    case AppIdMode.Pinned: return FeedAppId.ToString("B");
                    default: return "off";
                }
            }
        }

        /// <summary>Text that changes whenever the ASUS device selection changes.</summary>
        public string DeviceFilterKey
        {
            get { return string.Join(",", Only) + "|" + string.Join(",", Skip); }
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "effect={0} color={1} speed={2}s brightness={3} asus_brightness={4} maxfps={5} razer_style={6} razer_skip=[{7}] only=[{8}] skip=[{9}] razer_app_id={10} clock_anchor={11}",
                Effect, Rgb.Hex(Color), Speed, Brightness, AsusBrightness, MaxFps, RazerStyle, string.Join(", ", RazerSkip),
                string.Join(", ", Only), string.Join(", ", Skip), FeedKey, ClockAnchor);
        }

        private static int ParseInt(string key, string value, int min, int max, int fallback)
        {
            int n;
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out n) && n >= min && n <= max) return n;
            Log.Line("settings: {0} must be a number from {1} to {2} - keeping {3}", key, min, max, fallback);
            return fallback;
        }

        private static string[] SplitList(string value)
        {
            List<string> items = new List<string>();
            foreach (string part in value.Split(','))
            {
                string t = part.Trim();
                if (t.Length > 0) items.Add(t);
            }
            return items.ToArray();
        }
    }

    /// <summary>
    /// Colours are kept as 0x00BBGGRR ints - the layout Razer (COLORREF) and ASUS Aura both use,
    /// so they pass through without conversion.
    /// </summary>
    internal static class Rgb
    {
        public const int Black = 0;

        public static int Make(int r, int g, int b)
        {
            return (Clamp(b) << 16) | (Clamp(g) << 8) | Clamp(r);
        }

        public static int R(int c) { return c & 0xFF; }
        public static int G(int c) { return (c >> 8) & 0xFF; }
        public static int B(int c) { return (c >> 16) & 0xFF; }

        public static string Hex(int c)
        {
            return string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}", R(c), G(c), B(c));
        }

        public static int Scale(int c, int percent)
        {
            if (percent >= 100) return c & 0xFFFFFF;
            return Make(R(c) * percent / 100, G(c) * percent / 100, B(c) * percent / 100);
        }

        public static int ScaleF(int c, double f)
        {
            if (f >= 1) return c & 0xFFFFFF;
            if (f <= 0) return Black;
            return Make((int)(R(c) * f + 0.5), (int)(G(c) * f + 0.5), (int)(B(c) * f + 0.5));
        }

        /// <summary>Hue 0..1 (wraps), full saturation and value.</summary>
        public static int Hue(double h)
        {
            h = h - Math.Floor(h);
            double x = h * 6.0;
            int sector = (int)x;
            double f = x - sector;
            double r, g, b;
            switch (sector % 6)
            {
                case 0: r = 1; g = f; b = 0; break;
                case 1: r = 1 - f; g = 1; b = 0; break;
                case 2: r = 0; g = 1; b = f; break;
                case 3: r = 0; g = 1 - f; b = 1; break;
                case 4: r = f; g = 0; b = 1; break;
                default: r = 1; g = 0; b = 1 - f; break;
            }
            return Make((int)(r * 255 + 0.5), (int)(g * 255 + 0.5), (int)(b * 255 + 0.5));
        }

        /// <summary>Parses #RRGGBB, RRGGBB, 0xRRGGBB or a basic colour name.</summary>
        public static bool TryParse(string value, out int color)
        {
            string v = (value ?? "").Trim().ToLowerInvariant();
            switch (v)
            {
                case "red": color = Make(255, 0, 0); return true;
                case "orange": color = Make(255, 100, 0); return true;
                case "yellow": color = Make(255, 220, 0); return true;
                case "green": color = Make(0, 255, 0); return true;
                case "cyan": color = Make(0, 255, 255); return true;
                case "blue": color = Make(0, 0, 255); return true;
                case "purple": color = Make(160, 0, 255); return true;
                case "pink": color = Make(255, 0, 160); return true;
                case "white": color = Make(255, 255, 255); return true;
            }
            if (v.StartsWith("#")) v = v.Substring(1);
            else if (v.StartsWith("0x")) v = v.Substring(2);
            int rgb;
            if (v.Length == 6 && int.TryParse(v, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out rgb))
            {
                color = Make((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
                return true;
            }
            color = 0;
            return false;
        }

        private static int Clamp(int v)
        {
            return v < 0 ? 0 : (v > 255 ? 255 : v);
        }
    }

    /// <summary>The lighting effects. Everything is a function of time, so Razer and ASUS stay in step.</summary>
    internal static class Effects
    {
        /// <summary>clock_anchor = fixed counts from here, so the cycle position is the same after every restart.</summary>
        public static readonly DateTime Epoch = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Seconds into the clock effect. clock_anchor = fixed counts from Epoch; unlock counts from the last
        /// unlock or wake (anchorUtc), so phase is minus the delay before Synapse restarts its cycle.
        /// </summary>
        public static double ClockSeconds(Settings s, DateTime anchorUtc, DateTime nowUtc)
        {
            DateTime from = s.ClockAnchor == "unlock" ? anchorUtc : Epoch;
            return (nowUtc - from).TotalSeconds + s.Phase;
        }

        /// <summary>The colour every Razer device shows (and the first ASUS LED), before brightness.</summary>
        public static int Base(Settings s, double t)
        {
            switch (s.Effect)
            {
                case "breathing":
                    double f = 0.5 - 0.5 * Math.Cos(2 * Math.PI * t / s.Speed);
                    return Rgb.ScaleF(s.Color, 0.04 + 0.96 * f);
                case "spectrum":
                case "wave":
                    return Rgb.Hue(t / s.Speed);
                default:
                    return s.Color & 0xFFFFFF;
            }
        }

        /// <summary>Fills one ASUS strip. "wave" runs a rainbow along the LEDs; everything else is one colour.</summary>
        public static void Fill(Settings s, double t, int baseColor, int[] leds)
        {
            int n = leds.Length;
            if (s.Effect == "wave")
            {
                double phase = t / s.Speed;
                for (int j = 0; j < n; j++) leds[j] = Rgb.Hue(phase - (double)j / n);
            }
            else
            {
                for (int j = 0; j < n; j++) leds[j] = baseColor;
            }
        }
    }
}
