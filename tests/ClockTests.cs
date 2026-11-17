using System;
using System.Collections.Generic;

namespace AuraChromaBridge.Tests
{
    internal static class ClockTests
    {
        public static void AddTo(List<KeyValuePair<string, Action>> tests)
        {
            tests.Add(new KeyValuePair<string, Action>("clock_fixed_counts_from_epoch", ClockFixedCountsFromEpoch));
            tests.Add(new KeyValuePair<string, Action>("clock_unlock_counts_from_anchor", ClockUnlockCountsFromAnchor));
        }

        private static void ClockFixedCountsFromEpoch()
        {
            Settings s = new Settings();
            s.Set("phase", "-17.8");
            DateTime now = Effects.Epoch.AddSeconds(100);
            DateTime anchor = now.AddSeconds(-5);   // ignored when clock_anchor = fixed
            Check.Equal(82.2, Math.Round(Effects.ClockSeconds(s, anchor, now), 6), "seconds");
        }

        private static void ClockUnlockCountsFromAnchor()
        {
            Settings s = new Settings();
            s.Set("phase", "-2.5");
            s.Set("clock_anchor", "unlock");
            DateTime anchor = new DateTime(2026, 11, 17, 15, 0, 0, DateTimeKind.Utc);
            DateTime now = anchor.AddSeconds(10);
            Check.Equal(7.5, Math.Round(Effects.ClockSeconds(s, anchor, now), 6), "seconds");
        }
    }
}
