using System;
using System.Collections.Generic;

namespace AuraChromaBridge.Tests
{
    // "Not ready" = Razer's feed isn't up yet (worth trying again soon). "Ready" = it answered, and any refusals are final.
    internal static class FeedReadyTests
    {
        public static void AddTo(List<KeyValuePair<string, Action>> tests)
        {
            tests.Add(new KeyValuePair<string, Action>("dll_not_loaded_is_not_ready", DllNotLoadedIsNotReady));
            tests.Add(new KeyValuePair<string, Action>("all_refused_is_ready", AllRefusedIsReady));
            tests.Add(new KeyValuePair<string, Action>("service_down_is_not_ready", ServiceDownIsNotReady));
            tests.Add(new KeyValuePair<string, Action>("one_refusal_means_ready", OneRefusalMeansReady));
            tests.Add(new KeyValuePair<string, Action>("in_use_alone_is_not_ready", InUseAloneIsNotReady));
        }

        private static Dictionary<int, int> Codes(params int[] codeCountPairs)
        {
            Dictionary<int, int> d = new Dictionary<int, int>();
            for (int i = 0; i < codeCountPairs.Length; i += 2) d[codeCountPairs[i]] = codeCountPairs[i + 1];
            return d;
        }

        private static void DllNotLoadedIsNotReady()
        {
            Check.True(Feed.IsNotReady(false, Codes()), "DLL didn't load");
        }

        private static void AllRefusedIsReady()
        {
            Check.True(!Feed.IsNotReady(true, Codes(4309, 64)), "every ID refused with 4309");
            Check.True(!Feed.IsNotReady(true, Codes(12, 64)), "every ID refused with 12");
            Check.True(!Feed.IsNotReady(true, Codes(5, 64)), "every ID refused with 5");
        }

        private static void ServiceDownIsNotReady()
        {
            Check.True(Feed.IsNotReady(true, Codes(1062, 64)), "service not active");
            Check.True(Feed.IsNotReady(true, Codes(1168, 64)), "broadcast part not found");
        }

        private static void OneRefusalMeansReady()
        {
            Check.True(!Feed.IsNotReady(true, Codes(1062, 1, 4309, 63)), "the service answered at least once");
        }

        // A pinned ID still held by a copy that's exiting.
        private static void InUseAloneIsNotReady()
        {
            Check.True(Feed.IsNotReady(true, Codes(1152, 1)), "in use");
        }
    }
}
