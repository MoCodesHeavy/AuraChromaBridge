using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32;

namespace AuraChromaBridge
{
    /// <summary>Step 1: can we drive the case fans through ASUS Aura at all?</summary>
    internal static class AuraTest
    {
        private static readonly int Purple = Rgb.Make(160, 0, 255);

        public static int Run(Settings settings)
        {
            Log.Line("== ASUS fan test ==");
            Log.Line("Watch your case fans and strip. This takes about half a minute.");
            Log.Line("");

            Aura aura = new Aura();
            try
            {
                try
                {
                    aura.Connect();
                }
                catch (Exception ex)
                {
                    Log.Line("Could not connect to ASUS Aura: {0}", Program.Describe(ex));
                    Log.Line("=> Check that Armoury Crate is installed and that its Aura Sync page can change your fans.");
                    return 1;
                }

                Log.Line("ASUS Aura devices found: {0}", aura.Devices.Count);
                List<AuraDevice> devices = new List<AuraDevice>();
                foreach (AuraDevice d in aura.Devices)
                {
                    bool use = settings.Includes(d.Name) && d.Lights.Length > 0;
                    Log.Line("  {0}{1}", d, use ? "" : "   (not controlled - skipped by settings or has no LEDs)");
                    if (use) devices.Add(d);
                }
                if (devices.Count == 0)
                {
                    Log.Line("=> Aura reported nothing to control. Make sure this window is running as administrator,");
                    Log.Line("   and that Armoury Crate's Aura Sync page shows your fans.");
                    return 1;
                }
                Log.Line("");

                Step(aura, devices, "RED", Rgb.Make(255, 0, 0), 2500);
                Step(aura, devices, "GREEN", Rgb.Make(0, 255, 0), 2500);
                Step(aura, devices, "BLUE", Rgb.Make(0, 0, 255), 2500);
                Step(aura, devices, "WHITE", Rgb.Make(255, 255, 255), 2500);

                if (devices.Count > 1)
                {
                    Log.Line("");
                    Log.Line("Now one ASUS device at a time in PURPLE, everything else off. Note which number your fans light up on.");
                    foreach (AuraDevice d in devices)
                    {
                        try
                        {
                            foreach (AuraDevice other in devices) aura.Fill(other, other == d ? Purple : Rgb.Black);
                            Log.Line("  Lit now: #{0} \"{1}\"", d.Index, d.Name);
                        }
                        catch (Exception ex)
                        {
                            Log.Line("  #{0} \"{1}\": FAILED - {2}", d.Index, d.Name, Program.Describe(ex));
                        }
                        Thread.Sleep(3000);
                    }
                }

                Rainbow(aura, devices, 6000);
                Step(aura, devices, "OFF", Rgb.Black, 1000);

                Log.Line("");
                Log.Line("Fan test finished.");
                return 0;
            }
            finally
            {
                try
                {
                    bool wasConnected = aura.IsConnected;
                    aura.Release();
                    if (wasConnected) Log.Line("Lighting handed back to Armoury Crate.");
                }
                catch (Exception ex)
                {
                    Log.Line("Releasing ASUS control failed: {0}", Program.Describe(ex));
                }
            }
        }

        private static void Step(Aura aura, List<AuraDevice> devices, string label, int color, int holdMs)
        {
            Log.Line("{0} {1}", label, Rgb.Hex(color));
            foreach (AuraDevice d in devices)
            {
                Stopwatch sw = Stopwatch.StartNew();
                try
                {
                    aura.Fill(d, color);
                    Log.Line("    #{0} \"{1}\": written in {2} ms", d.Index, d.Name, sw.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    Log.Line("    #{0} \"{1}\": FAILED - {2}", d.Index, d.Name, Program.Describe(ex));
                }
            }
            Thread.Sleep(holdMs);
        }

        private static void Rainbow(Aura aura, List<AuraDevice> devices, int durationMs)
        {
            Log.Line("");
            Log.Line("RAINBOW moving along each strip for {0} s (checks per-LED control and speed)...", durationMs / 1000);
            Stopwatch sw = Stopwatch.StartNew();
            int frames = 0;
            int writes = 0;
            long writeTicks = 0;
            try
            {
                while (sw.ElapsedMilliseconds < durationMs)
                {
                    double t = sw.ElapsedMilliseconds / 1000.0;
                    foreach (AuraDevice d in devices)
                    {
                        int n = d.Lights.Length;
                        int[] colors = new int[n];
                        for (int j = 0; j < n; j++) colors[j] = Rgb.Hue((double)j / n + t * 0.5);
                        long start = Stopwatch.GetTimestamp();
                        if (aura.Write(d, colors))
                        {
                            writes++;
                            writeTicks += Stopwatch.GetTimestamp() - start;
                        }
                    }
                    frames++;
                    Thread.Sleep(10);
                }
            }
            catch (Exception ex)
            {
                Log.Line("  Rainbow stopped: {0}", Program.Describe(ex));
            }
            double secs = Math.Max(0.001, sw.ElapsedMilliseconds / 1000.0);
            double avgMs = writes > 0 ? writeTicks * 1000.0 / Stopwatch.Frequency / writes : 0;
            Log.Line("  {0} frames in {1:0.0} s = {2:0.0} frames per second, average device write {3:0.0} ms",
                frames, secs, frames / secs, avgMs);
        }
    }

    /// <summary>Step 1b: every ASUS device gets its own colour at once, so the fan header is easy to spot.</summary>
    internal static class IdentifyTest
    {
        private static readonly int[] Colors =
        {
            Rgb.Make(255, 255, 255), Rgb.Make(255, 0, 0), Rgb.Make(0, 255, 0), Rgb.Make(0, 0, 255),
            Rgb.Make(255, 255, 0), Rgb.Make(0, 255, 255), Rgb.Make(255, 0, 255)
        };

        private static readonly string[] ColorNames = { "WHITE", "RED", "GREEN", "BLUE", "YELLOW", "CYAN", "PINK" };

        public static int Run(Settings settings)
        {
            Log.Line("== Which ASUS header are the fans on? ==");
            Aura aura = new Aura();
            try
            {
                try
                {
                    aura.Connect();
                }
                catch (Exception ex)
                {
                    Log.Line("Could not connect to ASUS Aura: {0}", Program.Describe(ex));
                    return 1;
                }

                Log.Line("");
                int slot = 0;
                foreach (AuraDevice d in aura.Devices)
                {
                    if (d.Lights.Length == 0) continue;
                    int i = slot % Colors.Length;
                    slot++;
                    try
                    {
                        aura.Fill(d, Colors[i]);
                        Log.Line("  {0,-7} = #{1} \"{2}\" ({3} LEDs)", ColorNames[i], d.Index, d.Name, d.Lights.Length);
                    }
                    catch (Exception ex)
                    {
                        Log.Line("  #{0} \"{1}\": FAILED - {2}", d.Index, d.Name, Program.Describe(ex));
                    }
                }

                Log.Line("");
                Log.Line("Holding these colours for 30 seconds. Look at your case fans and strip and note their colour.");
                for (int left = 30; left > 0; left -= 5)
                {
                    Log.Line("  {0} s left...", left);
                    Thread.Sleep(5000);
                }
                return 0;
            }
            finally
            {
                try
                {
                    bool wasConnected = aura.IsConnected;
                    aura.Release();
                    if (wasConnected) Log.Line("Lighting handed back to Armoury Crate.");
                }
                catch (Exception ex)
                {
                    Log.Line("Releasing ASUS control failed: {0}", Program.Describe(ex));
                }
            }
        }
    }

    /// <summary>Step 2: can we set colours on the Razer devices through Razer's public Chroma SDK?</summary>
    internal static class RazerTest
    {
        public static int Run(Settings settings)
        {
            Log.Line("== Razer test (Chroma SDK) ==");
            Log.Line("Watch your Razer keyboard, mouse, headset and dock. This takes about 20 seconds.");
            Log.Line("");

            RazerSdk razer = new RazerSdk();
            string error;
            if (!razer.Load(out error))
            {
                Log.Line(error);
                Log.Line("=> Razer's Chroma SDK library wasn't found. It's installed with Razer Synapse.");
                return 1;
            }
            Log.Line("Loaded {0}", razer.DllPath);

            try
            {
                int r = razer.Init(settings.RazerMask);
                Log.Line("InitSDK result: {0} = {1}", r, RazerSdk.Describe(r));
                if (r != 0)
                {
                    Log.Line("=> Make sure Razer Synapse is running, then try again.");
                    return 1;
                }
                Thread.Sleep(1000);   // Razer asks for a short pause before the first effect

                Step(razer, settings, "RED", Rgb.Make(255, 0, 0), true);
                Step(razer, settings, "GREEN", Rgb.Make(0, 255, 0), false);
                Step(razer, settings, "BLUE", Rgb.Make(0, 0, 255), false);
                Step(razer, settings, "WHITE", Rgb.Make(255, 255, 255), false);

                Log.Line("SPECTRUM for 8 s...");
                Stopwatch sw = Stopwatch.StartNew();
                int frames = 0;
                int accepted = 0;
                while (sw.ElapsedMilliseconds < 8000)
                {
                    if (razer.SetAll(Rgb.Hue(sw.ElapsedMilliseconds / 4000.0), settings.RazerStyle)) accepted++;
                    frames++;
                    Thread.Sleep(33);
                }
                Log.Line("  {0} updates in {1:0.0} s, {2} accepted.", frames, sw.ElapsedMilliseconds / 1000.0, accepted);
                Log.Line("");
                Log.Line("Razer test finished.");
                return 0;
            }
            finally
            {
                bool wasInitialized = razer.IsInitialized;
                razer.Dispose();
                if (wasInitialized) Log.Line("Razer lighting handed back to Synapse.");
            }
        }

        private static void Step(RazerSdk razer, Settings settings, string label, int color, bool showResults)
        {
            bool ok = razer.SetAll(color, settings.RazerStyle);
            Log.Line("{0} {1}: {2}", label, Rgb.Hex(color), ok ? "sent" : "NOT accepted by any Razer device");
            if (showResults || !ok) Log.Line(razer.DescribeLastResults());
            Thread.Sleep(2500);
        }
    }

    /// <summary>The real thing: one effect, in step on Razer and ASUS.</summary>
    internal static class Sync
    {
        private static readonly ManualResetEvent StopEvent = new ManualResetEvent(false);
        private static readonly ManualResetEvent Stopped = new ManualResetEvent(false);
        private static volatile bool _stop;
        private static volatile bool _reconnect;
        private static long _writeStarted;   // Stopwatch timestamp while an ASUS write is in progress, else 0
        private static long _anchorUtcTicks;  // last start, unlock or wake: where clock_anchor = unlock counts from
        private static ConsoleCtrlHandler _ctrlHandler;
        private static Mutex _instance;

        private delegate bool ConsoleCtrlHandler(int ctrlType);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCtrlHandler(ConsoleCtrlHandler handler, bool add);

        public static int Run(Settings settings)
        {
            Log.Line("== Sync: one effect on Razer and ASUS ==");

            // Only one sync at a time - two would fight over the same lights.
            bool firstCopy;
            _instance = new Mutex(true, @"Local\AuraChromaBridge.Sync", out firstCopy);
            if (!firstCopy && Program.RelaunchCount > 0)
            {
                // Started by a copy that is still exiting: give it a moment to let go.
                try
                {
                    firstCopy = _instance.WaitOne(15000);
                }
                catch (AbandonedMutexException)
                {
                    firstCopy = true;   // it exited without letting go, which is expected after the watchdog
                }
            }
            if (!firstCopy)
            {
                Log.Line("Another copy of the sync is already running - this one is exiting.");
                return 1;
            }

            // A stop.request left over from before this start would stop us straight away.
            try
            {
                System.IO.File.Delete(System.IO.Path.Combine(Program.DataDir, "stop.request"));
            }
            catch (Exception)
            {
            }

            _ctrlHandler = OnConsoleCtrl;
            SetConsoleCtrlHandler(_ctrlHandler, true);
            WatchLockAndSleep();
            StartWatchdog();

            SetAnchor();
            RazerSdk razer = ConnectRazer(settings, true);
            List<AuraDevice> devices;
            Aura aura = ConnectAura(settings, out devices, true);
            bool feedNotReady;             // Razer's feed wasn't up yet when we tried (normal right after sign-in)
            Feed feed = ConnectFeed(settings, out feedNotReady);
            int feedStartTries = 0;
            long nextFeedStart = 60000;
            bool usingFeed = false;        // which colour source the last frame used, to log changes once
            bool feedRetry = false;        // the feed stalled and reconnecting hasn't worked yet
            long nextFeedReconnect = 0;
            long feedRetryWait = 30000;
            if (razer == null && aura == null && settings.EffectiveRazerMask != 0)
            {
                Log.Line("Neither Razer nor ASUS could be reached - stopping.");
                return 1;
            }

            Log.Line("");
            Log.Line("Running '{0}'. Edit settings.ini while this runs - changes apply within a second.", settings.Effect);
            Log.Line("Close this window or press Ctrl+C to stop; Synapse and Armoury Crate get the lights back.");

            Stopwatch clock = Stopwatch.StartNew();
            Dictionary<AuraDevice, int[]> buffers = new Dictionary<AuraDevice, int[]>();
            DateTime settingsStamp = SettingsStamp();
            long nextSettingsCheck = 1000;
            long nextRetry = 10000;
            long nextAsusRefresh = 5000;
            long nextStats = 60000;
            long lastLoop = 0;
            long lastRazerSend = -100000;
            int lastRazerColor = -1;
            int razerFailures = 0;
            bool firstRazerFrame = true;

            Dsx dsx = null;
            long lastDsxSend = -100000;
            int lastDsxColor = -1;
            bool dsxWasOk = false;

            long takeoverWindowStart = 0;
            long lastTakeback = -100000;
            int instantWrites = 0;
            int realWrites = 0;
            int takeovers = 0;

            long statsStart = 0;
            int frames = 0;
            int razerSends = 0;
            int asusWrites = 0;
            long asusTicks = 0;

            try
            {
                while (!_stop)
                {
                    long now = clock.ElapsedMilliseconds;

                    // A long gap means the PC slept; Synapse and Armoury Crate take the lights back meanwhile.
                    if (lastLoop > 0 && now - lastLoop > 15000)
                    {
                        Log.Line("Woke up after {0:0} s - reconnecting.", (now - lastLoop) / 1000.0);
                        SetAnchor();
                        _reconnect = true;
                    }
                    lastLoop = now;

                    if (_reconnect)
                    {
                        _reconnect = false;
                        if (StopEvent.WaitOne(3000)) break;   // give the ASUS and Razer services a moment
                        if (aura != null)
                        {
                            try
                            {
                                aura.Release();
                            }
                            catch (Exception)
                            {
                                aura.Abandon();
                            }
                            aura = null;
                        }
                        if (razer != null)
                        {
                            razer.Dispose();
                            razer = null;
                        }
                        razer = ConnectRazer(settings, true);
                        aura = ConnectAura(settings, out devices, true);
                        if (feed != null) feed.Dispose();
                        feed = ConnectFeed(settings, out feedNotReady);
                        feedStartTries = 0;
                        nextFeedStart = clock.ElapsedMilliseconds + 60000;
                        feedRetry = false;
                        feedRetryWait = 30000;
                        lastLoop = clock.ElapsedMilliseconds;   // a slow ASUS connect is not a sleep/wake gap
                        buffers.Clear();
                        lastRazerColor = -1;
                        firstRazerFrame = true;
                        nextRetry = clock.ElapsedMilliseconds + 10000;
                        lastLoop = clock.ElapsedMilliseconds;
                        continue;
                    }

                    if (now >= nextSettingsCheck)
                    {
                        nextSettingsCheck = now + 1000;

                        // Dropping a file called stop.request next to the exe stops the sync cleanly.
                        string stopFile = System.IO.Path.Combine(Program.DataDir, "stop.request");
                        if (System.IO.File.Exists(stopFile))
                        {
                            try
                            {
                                System.IO.File.Delete(stopFile);
                            }
                            catch (Exception)
                            {
                            }
                            Log.Line("stop.request found - stopping.");
                            break;
                        }

                        DateTime stamp = SettingsStamp();
                        if (stamp != settingsStamp)
                        {
                            settingsStamp = stamp;
                            Settings fresh = Settings.Load(Program.SettingsPath);
                            bool filterChanged = fresh.DeviceFilterKey != settings.DeviceFilterKey;
                            bool razerDevicesChanged = fresh.EffectiveRazerMask != settings.EffectiveRazerMask;
                            bool feedChanged = fresh.FeedKey != settings.FeedKey;
                            settings = fresh;
                            if (feedChanged)
                            {
                                Log.Line("Feed: razer_app_id is now {0}.", settings.FeedKey);
                                if (feed != null) feed.Dispose();
                                feed = ConnectFeed(settings, out feedNotReady);
                                feedStartTries = 0;
                                nextFeedStart = clock.ElapsedMilliseconds + 60000;
                                feedRetry = false;
                                feedRetryWait = 30000;
                                lastLoop = clock.ElapsedMilliseconds;   // trying IDs is not a sleep/wake gap
                            }
                            Log.Line("settings.ini changed: {0}", settings);
                            if (filterChanged && aura != null)
                            {
                                devices = Select(aura, settings, true);
                                buffers.Clear();
                            }
                            if (razerDevicesChanged && razer != null)
                            {
                                // Synapse only learns which devices an app claims when it connects.
                                Log.Line("Razer devices to control changed - reconnecting to Synapse.");
                                razer.Dispose();
                                razer = ConnectRazer(settings, true);
                                firstRazerFrame = true;
                            }
                            lastRazerColor = -1;
                        }
                    }

                    if (now >= nextRetry)
                    {
                        nextRetry = now + 10000;
                        if (razer == null)
                        {
                            razer = ConnectRazer(settings, false);
                            firstRazerFrame = true;
                            lastRazerColor = -1;
                        }
                        if (aura == null)
                        {
                            aura = ConnectAura(settings, out devices, false);
                            lastLoop = clock.ElapsedMilliseconds;   // a slow ASUS connect is not a sleep/wake gap
                            buffers.Clear();
                        }
                    }

                    // Razer's feed wasn't up yet: try again after 1, 2, 4 and 8 minutes. Not when Synapse refused the
                    // IDs - that answer doesn't change, and every try writes to Razer's own log.
                    if (feed == null && feedNotReady && now >= nextFeedStart)
                    {
                        feedStartTries++;
                        feed = Feed.Connect(settings, true, out feedNotReady);
                        if (feed == null && feedNotReady)
                        {
                            if (feedStartTries >= 4)
                            {
                                feedNotReady = false;
                                Log.Line("Feed: still not ready - using the clock effect until the next unlock, wake or settings change.");
                            }
                            else
                            {
                                nextFeedStart = clock.ElapsedMilliseconds + (60000L << feedStartTries);
                            }
                        }
                        lastLoop = clock.ElapsedMilliseconds;
                    }

                    // An animation stopped arriving: Synapse closed, restarted, or switched to a static colour. Reconnect with
                    // the same ID (checked before the colour is read, so a reconnect that works shows no clock colour),
                    // waiting 30 s, then longer each time it fails (up to 10 min), so Razer's own log stays small.
                    if (feed != null && (feedRetry || feed.IsStalled) && now >= nextFeedReconnect)
                    {
                        if (!feedRetry) Log.Line("Feed: no new colours from Synapse for 5 s - reconnecting to check.");
                        bool ok = feed.Reconnect();
                        if (ok)
                        {
                            feedRetryWait = 30000;
                        }
                        else
                        {
                            if (!feedRetry) Log.Line("Feed: reconnecting didn't work - trying again in a while.");
                            feedRetryWait = Math.Min(feedRetryWait * 2, 600000);
                        }
                        feedRetry = !ok;
                        nextFeedReconnect = clock.ElapsedMilliseconds + feedRetryWait;
                        lastLoop = clock.ElapsedMilliseconds;
                    }

                    // Synapse's own colour when the feed has one; otherwise the clock effect, which counts wall-clock
                    // time (clock_anchor = fixed) or time since the last unlock (clock_anchor = unlock).
                    double t = Effects.ClockSeconds(settings, new DateTime(Interlocked.Read(ref _anchorUtcTicks), DateTimeKind.Utc), DateTime.UtcNow);
                    int feedColor = 0;
                    bool fromFeed = feed != null && feed.TryGetColor(out feedColor);
                    int baseColor = fromFeed ? feedColor : Effects.Base(settings, t);
                    if (fromFeed != usingFeed)
                    {
                        usingFeed = fromFeed;
                        if (fromFeed) Log.Line("Feed: following Synapse ('{0}').", feed.AppId.Name);
                        else Log.Line("Feed: no colours from Synapse - using the clock effect.");
                    }


                    // DSX: the PS5 controller light bar follows the same colour (about 10 updates a second).
                    if (settings.DsxEnabled)
                    {
                        if (dsx == null || dsx.Port != settings.DsxPort)
                        {
                            if (dsx != null) dsx.Dispose();
                            dsx = new Dsx(settings.DsxPort);
                            lastDsxColor = -1;
                            Log.Line("DSX: sending the controller light bar colour to 127.0.0.1:{0}.", settings.DsxPort);
                        }
                        int dc = Rgb.Scale(baseColor, settings.Brightness);
                        if ((dc != lastDsxColor && now - lastDsxSend >= 100) || now - lastDsxSend >= 2000)
                        {
                            bool dsxOk = dsx.SetColor(dc);
                            if (dsxOk != dsxWasOk)
                            {
                                Log.Line(dsxOk ? "DSX: accepting colours." : "DSX: not reachable (is DSX running with its UDP listener on?). Will keep trying.");
                                dsxWasOk = dsxOk;
                            }
                            lastDsxColor = dc;
                            lastDsxSend = now;
                        }
                    }
                    else if (dsx != null)
                    {
                        dsx.Reset();
                        dsx.Dispose();
                        dsx = null;
                        dsxWasOk = false;
                        Log.Line("DSX: light bar handed back to DSX.");
                    }

                    // Razer: one colour for every device. Re-sent every 2 s even if unchanged, in case Synapse took over.
                    if (razer != null)
                    {
                        int rc = Rgb.Scale(baseColor, settings.Brightness);
                        if (rc != lastRazerColor || now - lastRazerSend >= 2000)
                        {
                            bool ok = razer.SetAll(rc, settings.RazerStyle);
                            if (firstRazerFrame)
                            {
                                firstRazerFrame = false;
                                Log.Line("Razer devices:");
                                Log.Line(razer.DescribeLastResults());
                            }
                            lastRazerColor = rc;
                            lastRazerSend = now;
                            razerSends++;
                            if (ok)
                            {
                                razerFailures = 0;
                            }
                            else if (++razerFailures >= 30)
                            {
                                Log.Line("Razer stopped accepting colours:");
                                Log.Line(razer.DescribeLastResults());
                                Log.Line("Reconnecting to Synapse in 10 s.");
                                razer.Dispose();
                                razer = null;
                                razerFailures = 0;
                                nextRetry = now + 10000;
                            }
                        }
                    }

                    // ASUS: the selected headers and the board. Repainted fully every 5 s in case Armoury Crate took over.
                    if (aura != null)
                    {
                        if (now >= nextAsusRefresh)
                        {
                            aura.Invalidate();
                            nextAsusRefresh = now + 5000;
                        }
                        try
                        {
                            int percent = Math.Max(1, settings.Brightness * settings.AsusBrightness / 100);
                            foreach (AuraDevice d in devices)
                            {
                                int[] buf;
                                if (!buffers.TryGetValue(d, out buf))
                                {
                                    buf = new int[d.Lights.Length];
                                    buffers[d] = buf;
                                }
                                if (fromFeed)
                                {
                                    for (int j = 0; j < buf.Length; j++) buf[j] = baseColor;   // one colour, like Synapse
                                }
                                else
                                {
                                    Effects.Fill(settings, t, baseColor, buf);
                                }
                                for (int j = 0; j < buf.Length; j++) buf[j] = Rgb.Scale(buf[j], percent);

                                long start = Stopwatch.GetTimestamp();
                                Interlocked.Exchange(ref _writeStarted, start);
                                bool sent = aura.Write(d, buf);
                                Interlocked.Exchange(ref _writeStarted, 0);
                                if (sent)
                                {
                                    long ticks = Stopwatch.GetTimestamp() - start;
                                    asusWrites++;
                                    asusTicks += ticks;

                                    // A real write to an ARGB strip takes 10-40 ms. One that finishes in under 1 ms
                                    // went nowhere: Armoury Crate has quietly taken the lights back.
                                    if (d.Lights.Length >= 20)
                                    {
                                        if (ticks * 1000.0 / Stopwatch.Frequency < 1.0) instantWrites++;
                                        else realWrites++;
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Exchange(ref _writeStarted, 0);
                            Log.Line("ASUS write failed: {0} - reconnecting in 2 s.", Program.Describe(ex));
                            aura.Abandon();
                            aura = null;
                            buffers.Clear();
                            nextRetry = now + 2000;
                        }
                    }

                    // Every 10 s: if ASUS writes only went nowhere, take the lights back (at most every 30 s).
                    if (now - takeoverWindowStart >= 10000)
                    {
                        if (aura != null && instantWrites >= 3 && realWrites == 0 && now - lastTakeback >= 30000)
                        {
                            takeovers++;
                            Log.Line("ASUS writes are going nowhere - Armoury Crate has taken the lights back. Reconnecting.");
                            if (takeovers >= 3)
                            {
                                Log.Line("  This keeps happening: check Armoury Crate > Lighting & Aura Sync > Aura Effects >");
                                Log.Line("  In-Game lighting effects is ON.");
                            }
                            try
                            {
                                aura.Release();
                            }
                            catch (Exception)
                            {
                                aura.Abandon();
                            }
                            aura = ConnectAura(settings, out devices, true);
                            lastLoop = clock.ElapsedMilliseconds;   // a slow ASUS connect is not a sleep/wake gap
                            buffers.Clear();
                            lastTakeback = now;
                            if (aura == null) nextRetry = now + 10000;
                        }
                        else if (realWrites > 0)
                        {
                            takeovers = 0;
                        }
                        takeoverWindowStart = now;
                        instantWrites = 0;
                        realWrites = 0;
                    }

                    frames++;
                    if (now >= nextStats)
                    {
                        double secs = Math.Max(0.001, (now - statsStart) / 1000.0);
                        double avgMs = asusWrites > 0 ? asusTicks * 1000.0 / Stopwatch.Frequency / asusWrites : 0;
                        Log.Line("[status] {0:0.0} frames/s | Razer updates/s {1:0.0} | ASUS writes/s {2:0.0}, avg {3:0.0} ms | colour {4} | Razer {5}, ASUS {6} | source {7}",
                            frames / secs, razerSends / secs, asusWrites / secs, avgMs, Rgb.Hex(baseColor),
                            razer != null ? "on" : "off", aura != null ? "on" : "off", fromFeed ? "feed" : "clock");
                        statsStart = now;
                        nextStats = now + 60000;
                        frames = 0;
                        razerSends = 0;
                        asusWrites = 0;
                        asusTicks = 0;
                    }

                    lastLoop = clock.ElapsedMilliseconds;
                    long wait = 1000 / settings.MaxFps - (clock.ElapsedMilliseconds - now);
                    if (wait > 0) StopEvent.WaitOne((int)wait);
                }
            }
            finally
            {
                if (feed != null)
                {
                    feed.Dispose();
                    Log.Line("Feed: disconnected.");
                }
                if (dsx != null)
                {
                    dsx.Reset();
                    dsx.Dispose();
                    Log.Line("DSX: light bar handed back to DSX.");
                }
                if (aura != null)
                {
                    try
                    {
                        aura.Release();
                        Log.Line("ASUS lighting handed back to Armoury Crate.");
                    }
                    catch (Exception ex)
                    {
                        Log.Line("Releasing ASUS control failed: {0}", Program.Describe(ex));
                    }
                }
                if (razer != null)
                {
                    razer.Dispose();
                    Log.Line("Razer lighting handed back to Synapse.");
                }
                Stopped.Set();
            }

            Log.Line("Stopped.");
            return 0;
        }

        private static void SetAnchor()
        {
            Interlocked.Exchange(ref _anchorUtcTicks, DateTime.UtcNow.Ticks);
        }

        /// <summary>Connects the feed unless razer_app_id = off. Null, with one log line, if no app ID works.</summary>
        private static Feed ConnectFeed(Settings settings, out bool notReady)
        {
            notReady = false;
            if (settings.FeedMode == AppIdMode.Off) return null;
            Feed feed = Feed.Connect(settings, true, out notReady);
            if (feed == null)
            {
                Log.Line(notReady
                    ? "Feed: Razer's colour feed isn't ready yet - using the clock effect and trying again in a minute."
                    : "Feed: not available - using the clock effect. Trying again after the next unlock, wake or settings change.");
            }
            return feed;
        }

        private static RazerSdk ConnectRazer(Settings settings, bool verbose)
        {
            if (settings.EffectiveRazerMask == 0)
            {
                if (!verbose) return null;
                if (settings.RazerMask != 0)
                {
                    Log.Line("Razer: left to Synapse because razer_app_id is on - the feed follows Synapse, so the bridge only drives ASUS.");
                }
                else
                {
                    Log.Line("Razer: left to Synapse (razer_skip = all) - driving ASUS only.");
                }
                return null;
            }
            RazerSdk razer = new RazerSdk();
            try
            {
                string error;
                if (!razer.Load(out error))
                {
                    if (verbose) Log.Line("Razer: {0} Will keep trying every 10 s.", error);
                    return null;
                }

                int r = razer.Init(settings.EffectiveRazerMask);
                if (r != 0)
                {
                    if (verbose) Log.Line("Razer: Chroma SDK init failed: {0} = {1}. Will keep trying every 10 s.", r, RazerSdk.Describe(r));
                    razer.Dispose();
                    return null;
                }
            }
            catch (Exception ex)
            {
                if (verbose) Log.Line("Razer: could not start the Chroma SDK: {0}. Will keep trying every 10 s.", Program.Describe(ex));
                razer.Dispose();
                return null;
            }

            Log.Line("Razer: connected ({0}).", razer.DllPath);
            Thread.Sleep(500);   // Razer asks for a short pause before the first effect
            return razer;
        }

        private static Aura ConnectAura(Settings settings, out List<AuraDevice> devices, bool verbose)
        {
            devices = new List<AuraDevice>();
            Aura aura = new Aura();
            try
            {
                aura.Connect();
            }
            catch (Exception ex)
            {
                if (verbose) Log.Line("ASUS: could not connect: {0}. Will keep trying every 10 s.", Program.Describe(ex));
                aura.Abandon();
                return null;
            }

            devices = Select(aura, settings, true);
            if (devices.Count == 0)
            {
                Log.Line("ASUS: nothing selected - check 'only' and 'skip' in settings.ini.");
                aura.Release();
                return null;
            }
            return aura;
        }

        private static List<AuraDevice> Select(Aura aura, Settings settings, bool log)
        {
            List<AuraDevice> selected = new List<AuraDevice>();
            if (log) Log.Line("ASUS devices ([x] = controlled):");
            foreach (AuraDevice d in aura.Devices)
            {
                bool use = settings.Includes(d.Name) && d.Lights.Length > 0;
                if (log) Log.Line("  [{0}] {1}", use ? "x" : " ", d);
                if (use) selected.Add(d);
            }
            return selected;
        }

        private static DateTime SettingsStamp()
        {
            try
            {
                return System.IO.File.GetLastWriteTimeUtc(Program.SettingsPath);
            }
            catch (Exception)
            {
                return DateTime.MinValue;
            }
        }

        private static bool OnConsoleCtrl(int ctrlType)
        {
            // 0 = Ctrl+C, 1 = Ctrl+Break, 2 = window closed, 5 = log off, 6 = shut down
            Log.Line("Stopping...");
            _stop = true;
            StopEvent.Set();
            Stopped.WaitOne(4000);
            return true;
        }

        /// <summary>Synapse and Armoury Crate take the lights back while Windows is locked or asleep; reconnect afterwards.</summary>
        private static void WatchLockAndSleep()
        {
            // Subscribing from an MTA thread makes .NET run its own hidden message window for these events.
            Thread t = new Thread(delegate()
            {
                try
                {
                    SystemEvents.SessionSwitch += OnSessionSwitch;
                    SystemEvents.PowerModeChanged += OnPowerModeChanged;
                    SystemEvents.SessionEnding += OnSessionEnding;
                }
                catch (Exception ex)
                {
                    Log.Line("(Can't watch for lock and sleep: {0})", ex.Message);
                }
            });
            t.IsBackground = true;
            t.SetApartmentState(ApartmentState.MTA);
            t.Start();
            t.Join(3000);
        }

        private static void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            if (e.Reason == SessionSwitchReason.SessionUnlock || e.Reason == SessionSwitchReason.SessionLogon ||
                e.Reason == SessionSwitchReason.ConsoleConnect)
            {
                Log.Line("Windows unlocked - reconnecting.");
                SetAnchor();
                _reconnect = true;
            }
        }

        private static void OnSessionEnding(object sender, SessionEndingEventArgs e)
        {
            // Signing out or shutting down: hand the lights back cleanly while there's still time.
            Log.Line("Windows is signing out or shutting down - stopping.");
            _stop = true;
            StopEvent.Set();
            Stopped.WaitOne(4000);
        }

        private static void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume)
            {
                Log.Line("PC woke from sleep - reconnecting.");
                SetAnchor();
                _reconnect = true;
            }
        }

        private static void StartWatchdog()
        {
            Thread t = new Thread(delegate()
            {
                while (!_stop)
                {
                    Thread.Sleep(1000);
                    long started = Interlocked.Read(ref _writeStarted);
                    if (started != 0 && Stopwatch.GetTimestamp() - started > 10 * Stopwatch.Frequency)
                    {
                        Log.Line("An ASUS write has been stuck for over 10 seconds - restarting.");
                        if (!Relauncher.HasConsole()) Relauncher.TryRelaunch("an ASUS write got stuck");
                        Log.Close();
                        Environment.Exit(3);
                    }
                }
            });
            t.IsBackground = true;
            t.Start();
        }
    }
}
