using System;
using System.Collections.Generic;

namespace AuraChromaBridge.Tests
{
    internal static class AppIdsTests
    {
        private static readonly Guid Asus = new Guid("{4C24AD2B-D0A6-409F-B895-EC7632F3BEE6}");

        public static void AddTo(List<KeyValuePair<string, Action>> tests)
        {
            tests.Add(new KeyValuePair<string, Action>("all_has_64_distinct_ids", AllHas64DistinctIds));
            tests.Add(new KeyValuePair<string, Action>("auto_order_asus_first_sample_last", AutoOrderAsusFirstSampleLast));
            tests.Add(new KeyValuePair<string, Action>("off_has_no_candidates", OffHasNoCandidates));
            tests.Add(new KeyValuePair<string, Action>("pinned_known_uses_list_name", PinnedKnownUsesListName));
            tests.Add(new KeyValuePair<string, Action>("pinned_unknown_named_setting", PinnedUnknownNamedSetting));
            tests.Add(new KeyValuePair<string, Action>("for_scan_pinned_unknown_goes_first", ForScanPinnedUnknownGoesFirst));
            tests.Add(new KeyValuePair<string, Action>("for_scan_pinned_known_no_duplicate", ForScanPinnedKnownNoDuplicate));
            tests.Add(new KeyValuePair<string, Action>("first_accepted_stops_at_first_zero", FirstAcceptedStopsAtFirstZero));
            tests.Add(new KeyValuePair<string, Action>("first_accepted_all_refused", FirstAcceptedAllRefused));
            tests.Add(new KeyValuePair<string, Action>("first_accepted_stops_at_in_use", FirstAcceptedStopsAtInUse));
            tests.Add(new KeyValuePair<string, Action>("first_accepted_sync_skips_in_use", FirstAcceptedSyncSkipsInUse));
            tests.Add(new KeyValuePair<string, Action>("probe_all_tries_everything", ProbeAllTriesEverything));
        }

        private static void AllHas64DistinctIds()
        {
            Check.Equal(64, KnownAppIds.All.Length, "number of IDs");
            HashSet<Guid> seen = new HashSet<Guid>();
            foreach (KnownAppId id in KnownAppIds.All) seen.Add(id.Id);
            Check.Equal(64, seen.Count, "distinct IDs");
        }

        private static void AutoOrderAsusFirstSampleLast()
        {
            KnownAppId[] c = KnownAppIds.Candidates(AppIdMode.Auto, Guid.Empty);
            Check.Equal(64, c.Length, "candidates");
            Check.Equal("Chroma Control - ASUS", c[0].Name, "first name");
            Check.Equal(new Guid("{DEF05DCE-1662-4D9A-A312-A31028651915}"), c[0].Id, "first ID");
            Check.Equal(Asus, c[1].Id, "second ID");
            Check.Equal("ASUS", c[1].Name, "second name");
            Check.Equal("Chroma Broadcast Sample Application", c[63].Name, "last name");
            Check.Equal(new Guid("{8FB61199-44F5-4C53-A706-747373A21796}"), c[63].Id, "last ID");
            for (int i = 53; i <= 62; i++)
            {
                Check.True(c[i].Name.StartsWith("Chroma Broadcast Test Application ", StringComparison.Ordinal),
                    "candidate " + i + " (" + c[i].Name + ") is a test application");
            }
        }

        private static void OffHasNoCandidates()
        {
            Check.Equal(0, KnownAppIds.Candidates(AppIdMode.Off, Asus).Length, "candidates when off");
        }

        private static void PinnedKnownUsesListName()
        {
            KnownAppId[] c = KnownAppIds.Candidates(AppIdMode.Pinned, Asus);
            Check.Equal(1, c.Length, "candidates");
            Check.Equal("ASUS", c[0].Name, "name");
            Check.Equal(Asus, c[0].Id, "ID");
        }

        private static void PinnedUnknownNamedSetting()
        {
            Guid unknown = Guid.NewGuid();
            KnownAppId[] c = KnownAppIds.Candidates(AppIdMode.Pinned, unknown);
            Check.Equal(1, c.Length, "candidates");
            Check.Equal("razer_app_id", c[0].Name, "name");
            Check.Equal(unknown, c[0].Id, "ID");
        }

        private static void ForScanPinnedUnknownGoesFirst()
        {
            Guid unknown = Guid.NewGuid();
            KnownAppId[] c = KnownAppIds.ForScan(AppIdMode.Pinned, unknown);
            Check.Equal(65, c.Length, "candidates");
            Check.Equal("razer_app_id", c[0].Name, "first name");
            Check.Equal(unknown, c[0].Id, "first ID");
            Check.Equal("Chroma Control - ASUS", c[1].Name, "second name");
        }

        private static void ForScanPinnedKnownNoDuplicate()
        {
            KnownAppId[] c = KnownAppIds.ForScan(AppIdMode.Pinned, Asus);
            Check.Equal(64, c.Length, "candidates");
            Check.Equal("ASUS", c[0].Name, "first name");
            int asusCount = 0;
            foreach (KnownAppId id in c) if (id.Id == Asus) asusCount++;
            Check.Equal(1, asusCount, "times ASUS appears");
        }

        private static void FirstAcceptedStopsAtFirstZero()
        {
            KnownAppId[] c = KnownAppIds.Candidates(AppIdMode.Auto, Guid.Empty);
            Guid third = c[2].Id;
            int reports = 0;
            int result;
            KnownAppId winner = KnownAppIds.FirstAccepted(c,
                delegate(Guid id) { return id == third ? 0 : 4309; },
                delegate(KnownAppId id, int code) { reports++; }, out result);
            Check.True(winner != null, "a winner");
            Check.Equal(third, winner.Id, "winner");
            Check.Equal(0, result, "code");
            Check.Equal(3, reports, "reports");
        }

        private static void FirstAcceptedAllRefused()
        {
            KnownAppId[] c = KnownAppIds.Candidates(AppIdMode.Auto, Guid.Empty);
            int reports = 0;
            int result;
            KnownAppId winner = KnownAppIds.FirstAccepted(c,
                delegate(Guid id) { return 4309; },
                delegate(KnownAppId id, int code) { reports++; }, out result);
            Check.True(winner == null, "no winner");
            Check.Equal(4309, result, "code");
            Check.Equal(64, reports, "reports");
        }

        // The sync moves past an ID another program holds (say Chroma Control with its own ID) to one that works.
        private static void FirstAcceptedSyncSkipsInUse()
        {
            KnownAppId[] c = KnownAppIds.Candidates(AppIdMode.Auto, Guid.Empty);
            Guid first = c[0].Id, third = c[2].Id;
            int result;
            KnownAppId winner = KnownAppIds.FirstAccepted(c,
                delegate(Guid id) { return id == first ? 1152 : (id == third ? 0 : 4309); },
                null, false, out result);
            Check.True(winner != null, "a winner");
            Check.Equal(third, winner.Id, "the first ID that works");
            Check.Equal(0, result, "code");
        }

        // The running sync already holds this ID: stop there instead of connecting a second brand's ID.
        private static void FirstAcceptedStopsAtInUse()
        {
            KnownAppId[] c = KnownAppIds.Candidates(AppIdMode.Auto, Guid.Empty);
            Guid second = c[1].Id;
            int reports = 0;
            int result;
            KnownAppId stopped = KnownAppIds.FirstAccepted(c,
                delegate(Guid id) { return id == second ? 1152 : 4309; },
                delegate(KnownAppId id, int code) { reports++; }, out result);
            Check.True(stopped != null, "stopped at an ID");
            Check.Equal(second, stopped.Id, "the ID in use");
            Check.Equal(1152, result, "code");
            Check.Equal(2, reports, "reports");
        }

        private static void ProbeAllTriesEverything()
        {
            KnownAppId[] c = KnownAppIds.Candidates(AppIdMode.Auto, Guid.Empty);
            Guid second = c[1].Id, fifth = c[4].Id;
            int uninits = 0;
            List<KeyValuePair<KnownAppId, int>> results = KnownAppIds.ProbeAll(c,
                delegate(Guid id) { return id == second || id == fifth ? 0 : 4309; },
                delegate { uninits++; });
            Check.Equal(64, results.Count, "results");
            for (int i = 0; i < results.Count; i++)
            {
                Check.Equal(c[i].Id, results[i].Key.Id, "result " + i + " in order");
                Check.Equal(i == 1 || i == 4 ? 0 : 4309, results[i].Value, "result " + i + " code");
            }
            Check.Equal(2, uninits, "uninit calls");
        }
    }
}
