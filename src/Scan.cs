// "scan": finds out which Chroma Broadcast app IDs Synapse accepts on this PC, then listens
// for 15 seconds with the first one to check that colours really arrive.
// "scan all" tries every known ID instead of stopping at the first one Synapse accepts.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace AuraChromaBridge
{
    internal static class Scan
    {
        private const int ListenMs = 15000;
        private const int InUse = 1152;

        private static readonly object Gate = new object();
        private static int[] _latest;
        private static int _events;

        public static int Run(Settings settings, bool all)
        {
            Log.Line("== Scan: which Razer app IDs does Synapse accept on this PC? ==");
            ChromaBroadcast.LogRegistry();
            Log.Line("");

            ChromaBroadcast broadcast = new ChromaBroadcast();
            string error;
            if (!broadcast.Load(out error))
            {
                Log.Line(error);
                return 1;
            }
            Log.Line("Loaded {0}", broadcast.DllPath);
            Log.Line("");

            try
            {
                KnownAppId[] candidates = KnownAppIds.ForScan(settings.FeedMode, settings.FeedAppId);
                Dictionary<int, int> codes = new Dictionary<int, int>();
                KnownAppId winner;

                if (all)
                {
                    Log.Line("Each accepted ID may be added to the app list in the Razer Chroma app's Chroma Connect page.");
                    Log.Line("Trying all {0} app IDs...", candidates.Length);
                    List<KeyValuePair<KnownAppId, int>> results = KnownAppIds.ProbeAll(candidates,
                        delegate(Guid id) { return broadcast.Init(id); },
                        delegate { broadcast.Dispose(); Thread.Sleep(200); });

                    List<string> accepted = new List<string>();
                    winner = null;
                    foreach (KeyValuePair<KnownAppId, int> result in results)
                    {
                        Report(result.Key, result.Value, codes);
                        if (result.Value == 0)
                        {
                            accepted.Add(result.Key.Name);
                            if (winner == null) winner = result.Key;
                        }
                    }
                    Log.Line("");
                    Log.Line("Accepted on this PC: {0}", accepted.Count > 0 ? string.Join(", ", accepted.ToArray()) : "none");

                    if (winner != null)
                    {
                        int again = broadcast.Init(winner.Id);
                        if (again != 0)
                        {
                            Log.Line("Could not connect to '{0}' again to listen: {1} = {2}.", winner.Name, again, ChromaBroadcast.Describe(again));
                            return 1;
                        }
                    }
                }
                else
                {
                    Log.Line("Trying app IDs until Synapse accepts one...");
                    winner = KnownAppIds.FirstAccepted(candidates,
                        delegate(Guid id) { return broadcast.Init(id); },
                        delegate(KnownAppId id, int code) { Report(id, code, codes); });
                }

                Log.Line("");
                if (winner == null)
                {
                    int common = Feed.MostCommon(codes);
                    Log.Line("Most common result: {0} = {1}", common, ChromaBroadcast.Describe(common));
                    ChromaBroadcast.LogRazerLogTail(10);
                    Log.Line("No app ID was accepted on this PC. The bridge will keep using the clock effect.");
                    return 1;
                }

                return Listen(broadcast, winner);
            }
            finally
            {
                broadcast.Dispose();
            }
        }

        private static void Report(KnownAppId id, int code, Dictionary<int, int> codes)
        {
            int n;
            codes.TryGetValue(code, out n);
            codes[code] = n + 1;

            if (code == 0)
            {
                Log.Line("  ACCEPTED         {0}  {1}", id.Name, id.Id.ToString("B"));
            }
            else if (code == InUse)
            {
                Log.Line("  in use    {0,5}  {1}  {2}  (probably by the running sync)", code, id.Name, id.Id.ToString("B"));
            }
            else
            {
                Log.Line("  refused   {0,5}  {1}  {2}", code, id.Name, id.Id.ToString("B"));
            }
        }

        private static int Listen(ChromaBroadcast broadcast, KnownAppId winner)
        {
            broadcast.ColorsReceived += delegate(int[] colors)
            {
                lock (Gate)
                {
                    _latest = colors;
                    _events++;
                }
            };
            int r = broadcast.Register();
            if (r != 0)
            {
                Log.Line("Synapse accepted '{0}' but listening for colours failed: {1} = {2}.", winner.Name, r, ChromaBroadcast.Describe(r));
                return 1;
            }

            Log.Line("Listening with '{0}' for {1} seconds...", winner.Name, ListenMs / 1000);
            Stopwatch sw = Stopwatch.StartNew();
            string lastLine = null;
            while (sw.ElapsedMilliseconds < ListenMs)
            {
                Thread.Sleep(250);
                int[] colors;
                lock (Gate) colors = _latest;
                if (colors == null) continue;
                StringBuilder sb = new StringBuilder();
                foreach (int c in colors) sb.Append(Rgb.Hex(c)).Append("  ");
                string line = sb.ToString();
                if (line != lastLine)
                {
                    Log.Line("  {0,5:0.0}s  {1}", sw.ElapsedMilliseconds / 1000.0, line);
                    lastLine = line;
                }
            }

            int total;
            lock (Gate) total = _events;
            Log.Line("{0} colour updates received.", total);
            Log.Line("");
            if (total == 0)
            {
                Log.Line("Synapse accepted '{0}' but sent no colours. Open the Razer Chroma app > Chroma Connect, switch '{0}' on, then run the scan again.", winner.Name);
                return 1;
            }
            Log.Line("The feed works with '{0}'. In settings.ini set razer_app_id = auto (or razer_app_id = {1}).", winner.Name, winner.Id.ToString("B"));
            return 0;
        }
    }
}
