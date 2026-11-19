using System;
using System.Collections.Generic;

namespace AuraChromaBridge.Tests
{
    internal static class ScanTests
    {
        public static void AddTo(List<KeyValuePair<string, Action>> tests)
        {
            tests.Add(new KeyValuePair<string, Action>("verdict_no_colours", VerdictNoColours));
            tests.Add(new KeyValuePair<string, Action>("verdict_only_black", VerdictOnlyBlack));
            tests.Add(new KeyValuePair<string, Action>("verdict_works", VerdictWorks));
        }

        private static void VerdictNoColours()
        {
            Check.Equal(ScanVerdict.NoColours, Scan.Verdict(0, false), "verdict");
        }

        // Synapse's Connect effect didn't light Connect devices: 260 updates, all black.
        private static void VerdictOnlyBlack()
        {
            Check.Equal(ScanVerdict.OnlyBlack, Scan.Verdict(260, false), "verdict");
        }

        private static void VerdictWorks()
        {
            Check.Equal(ScanVerdict.Works, Scan.Verdict(280, true), "verdict");
        }
    }
}
