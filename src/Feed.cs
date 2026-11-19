// Connects to Razer's Chroma Broadcast feed with the first app ID Synapse accepts,
// and keeps the latest colour in a FeedState for the sync loop.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace AuraChromaBridge
{
    internal sealed class Feed : IDisposable
    {
        private readonly FeedState _state = new FeedState();
        private ChromaBroadcast _broadcast;

        private Feed(KnownAppId appId, ChromaBroadcast broadcast)
        {
            AppId = appId;
            _broadcast = broadcast;
        }

        /// <summary>The app ID Synapse accepted.</summary>
        public KnownAppId AppId { get; private set; }

        public static long NowMs()
        {
            return Stopwatch.GetTimestamp() * 1000 / Stopwatch.Frequency;
        }

        /// <summary>
        /// Tries the IDs razer_app_id allows, in order, and listens with the first one Synapse accepts.
        /// Null when razer_app_id is off, the Razer DLL won't load, no ID is accepted, or registering fails;
        /// notReady then says whether Razer's feed simply wasn't up yet (see IsNotReady).
        /// </summary>
        public static Feed Connect(Settings settings, bool verbose, out bool notReady)
        {
            notReady = false;
            KnownAppId[] candidates = KnownAppIds.Candidates(settings.FeedMode, settings.FeedAppId);
            if (candidates.Length == 0) return null;

            ChromaBroadcast broadcast = new ChromaBroadcast();
            try
            {
                string error;
                if (!broadcast.Load(out error))
                {
                    if (verbose) Log.Line("Feed: {0}", error);
                    notReady = true;
                    return null;
                }

                int tried = 0;
                int result;
                Dictionary<int, int> codes = new Dictionary<int, int>();
                KnownAppId winner = KnownAppIds.FirstAccepted(candidates,
                    delegate(Guid id) { return broadcast.Init(id); },
                    delegate(KnownAppId id, int code)
                    {
                        tried++;
                        int n;
                        codes.TryGetValue(code, out n);
                        codes[code] = n + 1;
                    }, false, out result);   // another program may hold an ID: skip it and try the next

                if (winner == null)
                {
                    notReady = IsNotReady(true, codes);
                    if (verbose)
                    {
                        int common = MostCommon(codes);
                        Log.Line("Feed: none of the {0} app IDs were accepted (most common: {1} = {2}). Using the clock effect.",
                            candidates.Length, common, ChromaBroadcast.Describe(common));
                    }
                    return null;
                }

                Feed feed = new Feed(winner, broadcast);
                int r = feed.Listen(broadcast);
                if (r != 0)
                {
                    if (verbose)
                    {
                        Log.Line("Feed: Synapse accepted the '{0}' app ID but listening for colours failed: {1} = {2}. Using the clock effect.",
                            winner.Name, r, ChromaBroadcast.Describe(r));
                    }
                    broadcast.Dispose();
                    notReady = true;
                    return null;
                }

                Log.Line("Feed: Synapse accepted the '{0}' app ID ({1} tried) - following Synapse's colours.", winner.Name, tried);
                return feed;
            }
            catch (Exception ex)
            {
                if (verbose) Log.Line("Feed: could not connect: {0}. Using the clock effect.", Program.Describe(ex));
                broadcast.Dispose();
                notReady = true;
                return null;
            }
        }

        /// <summary>True, with Synapse's colour, when the feed should drive the lights.</summary>
        public bool TryGetColor(out int color)
        {
            return _state.TryGetColor(NowMs(), out color);
        }

        /// <summary>An animation stopped arriving: Synapse probably closed or restarted.</summary>
        public bool IsStalled
        {
            get { return _state.IsStalled(NowMs()); }
        }

        /// <summary>
        /// Drops the connection and connects again with the same app ID only. When that works, the last colour
        /// stays (Synapse may simply have switched to a static colour); when it fails, the clock takes over.
        /// </summary>
        public bool Reconnect()
        {
            if (_broadcast != null)
            {
                _broadcast.Dispose();
                _broadcast = null;
            }

            ChromaBroadcast broadcast = new ChromaBroadcast();
            try
            {
                string error;
                if (broadcast.Load(out error) && broadcast.Init(AppId.Id) == 0 && Listen(broadcast) == 0)
                {
                    _broadcast = broadcast;
                    _state.KeepLastColour(NowMs());
                    return true;
                }
            }
            catch (Exception)
            {
            }
            broadcast.Dispose();
            _state.Reset();
            return false;
        }

        public void Dispose()
        {
            if (_broadcast != null)
            {
                _broadcast.Dispose();
                _broadcast = null;
            }
            _state.Reset();
        }

        // Hooks the events before registering, because Synapse may send the current colour straight away.
        private int Listen(ChromaBroadcast broadcast)
        {
            FeedState state = _state;
            broadcast.ColorsReceived += delegate(int[] colors) { state.OnColors(colors, NowMs()); };
            broadcast.LiveChanged += delegate(bool live) { state.OnLive(live, NowMs()); };
            return broadcast.Register();
        }

        // Init results that mean "Synapse answered and said no to this ID": final, so not worth trying again soon.
        private static readonly int[] RefusalCodes = { 5, 12, 4309 };

        /// <summary>
        /// True when Razer's feed wasn't up yet (DLL didn't load, or not one ID got a real refusal, e.g. the service
        /// isn't running at sign-in): worth trying again in a minute. False when Synapse refused the IDs.
        /// </summary>
        public static bool IsNotReady(bool loaded, Dictionary<int, int> codes)
        {
            if (!loaded) return true;
            foreach (int refusal in RefusalCodes)
            {
                int n;
                if (codes.TryGetValue(refusal, out n) && n > 0) return false;
            }
            return true;
        }

        /// <summary>The result code that came up most often (Init results by count).</summary>
        internal static int MostCommon(Dictionary<int, int> codes)
        {
            int best = 0, bestCount = -1;
            foreach (KeyValuePair<int, int> pair in codes)
            {
                if (pair.Value > bestCount)
                {
                    best = pair.Key;
                    bestCount = pair.Value;
                }
            }
            return best;
        }
    }
}
