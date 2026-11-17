using System;
using System.Collections.Generic;

namespace AuraChromaBridge.Tests
{
    internal static class SettingsTests
    {
        private static readonly Guid Asus = new Guid("{4C24AD2B-D0A6-409F-B895-EC7632F3BEE6}");

        public static void AddTo(List<KeyValuePair<string, Action>> tests)
        {
            tests.Add(new KeyValuePair<string, Action>("defaults_off_and_fixed", DefaultsOffAndFixed));
            tests.Add(new KeyValuePair<string, Action>("razer_app_id_auto", RazerAppIdAuto));
            tests.Add(new KeyValuePair<string, Action>("razer_app_id_guid_with_braces", RazerAppIdGuidWithBraces));
            tests.Add(new KeyValuePair<string, Action>("razer_app_id_guid_without_braces", RazerAppIdGuidWithoutBraces));
            tests.Add(new KeyValuePair<string, Action>("razer_app_id_garbage_keeps_previous", RazerAppIdGarbageKeepsPrevious));
            tests.Add(new KeyValuePair<string, Action>("clock_anchor_unlock", ClockAnchorUnlock));
            tests.Add(new KeyValuePair<string, Action>("shipped_settings_ini_loads_cleanly", ShippedSettingsIniLoadsCleanly));
            tests.Add(new KeyValuePair<string, Action>("feed_on_leaves_razer_to_synapse", FeedOnLeavesRazerToSynapse));
            tests.Add(new KeyValuePair<string, Action>("feed_off_keeps_razer_skip", FeedOffKeepsRazerSkip));
        }

        private static void DefaultsOffAndFixed()
        {
            Settings s = new Settings();
            Check.Equal(AppIdMode.Off, s.FeedMode, "FeedMode");
            Check.Equal("fixed", s.ClockAnchor, "ClockAnchor");
            Check.Equal("off", s.FeedKey, "FeedKey");
        }

        private static void RazerAppIdAuto()
        {
            Settings s = new Settings();
            s.Set("razer_app_id", "AUTO");
            Check.Equal(AppIdMode.Auto, s.FeedMode, "FeedMode");
            Check.Equal("auto", s.FeedKey, "FeedKey");
        }

        private static void RazerAppIdGuidWithBraces()
        {
            Settings s = new Settings();
            s.Set("razer_app_id", "{4C24AD2B-D0A6-409F-B895-EC7632F3BEE6}");
            Check.Equal(AppIdMode.Pinned, s.FeedMode, "FeedMode");
            Check.Equal(Asus, s.FeedAppId, "FeedAppId");
            Check.Equal("{4c24ad2b-d0a6-409f-b895-ec7632f3bee6}", s.FeedKey, "FeedKey");
        }

        private static void RazerAppIdGuidWithoutBraces()
        {
            Settings s = new Settings();
            s.Set("razer_app_id", "4c24ad2b-d0a6-409f-b895-ec7632f3bee6");
            Check.Equal(AppIdMode.Pinned, s.FeedMode, "FeedMode");
            Check.Equal(Asus, s.FeedAppId, "FeedAppId");
        }

        private static void RazerAppIdGarbageKeepsPrevious()
        {
            Settings s = new Settings();
            s.Set("razer_app_id", "auto");
            s.Set("razer_app_id", "banana");
            Check.Equal(AppIdMode.Auto, s.FeedMode, "FeedMode");
        }

        // Run the self-test from the project folder, so it finds the settings.ini that ships.
        private static void ShippedSettingsIniLoadsCleanly()
        {
            string path = System.IO.Path.Combine(Environment.CurrentDirectory, "settings.ini");
            Check.True(System.IO.File.Exists(path), "settings.ini in " + Environment.CurrentDirectory + " (run from the project folder)");

            System.IO.TextWriter original = Console.Out;
            System.IO.StringWriter captured = new System.IO.StringWriter();
            Settings s;
            Console.SetOut(captured);
            try
            {
                s = Settings.Load(path);
            }
            finally
            {
                Console.SetOut(original);
            }
            Check.Equal("", captured.ToString(), "messages while loading settings.ini");

            List<string> keys = new List<string>();
            foreach (string raw in System.IO.File.ReadAllLines(path))
            {
                string line = raw;
                int comment = line.IndexOf(';');
                if (comment >= 0) line = line.Substring(0, comment);
                int eq = line.IndexOf('=');
                if (eq > 0) keys.Add(line.Substring(0, eq).Trim().ToLowerInvariant());
            }
            Check.True(keys.Contains("razer_app_id"), "settings.ini has a razer_app_id line");
            Check.True(keys.Contains("clock_anchor"), "settings.ini has a clock_anchor line");
            Check.Equal(AppIdMode.Off, s.FeedMode, "razer_app_id in the shipped file");
            Check.Equal("fixed", s.ClockAnchor, "clock_anchor in the shipped file");
        }

        // Following Synapse's feed while also sending colours to the Razer gear would make the feed echo the
        // bridge's own output, so with the feed on the bridge leaves every Razer device to Synapse.
        private static void FeedOnLeavesRazerToSynapse()
        {
            Settings s = new Settings();
            s.Set("razer_skip", "keyboard");
            s.Set("razer_app_id", "auto");
            Check.True(s.RazerMask != 0, "razer_skip alone still asks for some Razer devices");
            Check.Equal(0, s.EffectiveRazerMask, "Razer devices the sync drives");
        }

        private static void FeedOffKeepsRazerSkip()
        {
            Settings s = new Settings();
            s.Set("razer_skip", "keyboard");
            Check.True(s.RazerMask != 0, "some Razer devices");
            Check.Equal(s.RazerMask, s.EffectiveRazerMask, "Razer devices the sync drives");
        }

        private static void ClockAnchorUnlock()
        {
            Settings s = new Settings();
            s.Set("clock_anchor", "unlock");
            Check.Equal("unlock", s.ClockAnchor, "ClockAnchor after unlock");
            s.Set("clock_anchor", "sometimes");
            Check.Equal("unlock", s.ClockAnchor, "ClockAnchor after a bad value");
        }
    }
}
