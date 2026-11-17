// What the bridge knows about Synapse's broadcast feed, and whether its colour should be used.
// Colours and status arrive on Razer's own thread; the sync loop reads them on its thread.

namespace AuraChromaBridge
{
    internal sealed class FeedState
    {
        /// <summary>An animation that has gone this long without a new colour has stopped (Synapse closed or crashed).</summary>
        public const long StallMs = 5000;

        private readonly object _gate = new object();
        private bool _live;
        private bool _hasColor;
        private int _color;
        private long _lastColorMs;
        private bool _hasChange;
        private long _lastChangeMs;   // when the colour last differed from the one before it

        /// <summary>colors is CL1..CL5 from the feed (0x00BBGGRR); the bridge uses CL1.</summary>
        public void OnColors(int[] colors, long nowMs)
        {
            if (colors == null || colors.Length == 0) return;
            int c = colors[0] & 0xFFFFFF;
            lock (_gate)
            {
                if (_hasColor && c != _color)
                {
                    _hasChange = true;
                    _lastChangeMs = nowMs;
                }
                _color = c;
                _hasColor = true;
                _lastColorMs = nowMs;
                _live = true;
            }
        }

        public void OnLive(bool live, long nowMs)
        {
            lock (_gate)
            {
                _live = live;
            }
        }

        /// <summary>True, with Synapse's colour, when the feed should drive the lights; false means use the clock effect.</summary>
        public bool TryGetColor(long nowMs, out int color)
        {
            lock (_gate)
            {
                color = _color;
                return _live && _hasColor && !Stalled(nowMs);
            }
        }

        public bool IsStalled(long nowMs)
        {
            lock (_gate)
            {
                return Stalled(nowMs);
            }
        }

        /// <summary>
        /// After a stall and a reconnect that worked: Synapse is there but quiet, which is what switching from
        /// an animation to a static colour looks like. Keep the last colour, as a static colour, until new frames come.
        /// </summary>
        public void KeepLastColour(long nowMs)
        {
            lock (_gate)
            {
                if (!_hasColor) return;
                _live = true;
                _hasChange = false;
                _lastColorMs = nowMs;
            }
        }

        public void Reset()
        {
            lock (_gate)
            {
                _live = false;
                _hasColor = false;
                _color = 0;
                _lastColorMs = 0;
                _hasChange = false;
                _lastChangeMs = 0;
            }
        }

        // Callers hold _gate. A static colour legitimately sends nothing for minutes, so only an
        // animation that goes quiet counts as stalled. "Animation" means the colour changed within
        // StallMs before the last frame, so one repeated frame doesn't hide a stall.
        private bool Stalled(long nowMs)
        {
            bool animating = _hasChange && _lastColorMs - _lastChangeMs <= StallMs;
            return _hasColor && animating && nowMs - _lastColorMs > StallMs;
        }
    }
}
