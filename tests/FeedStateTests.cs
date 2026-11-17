using System;
using System.Collections.Generic;

namespace AuraChromaBridge.Tests
{
    internal static class FeedStateTests
    {
        // Colours are 0x00BBGGRR.
        private const int Red = 0x0000FF;
        private const int Green = 0x00FF00;
        private const int Blue = 0xFF0000;

        public static void AddTo(List<KeyValuePair<string, Action>> tests)
        {
            tests.Add(new KeyValuePair<string, Action>("new_state_has_no_colour", NewStateHasNoColour));
            tests.Add(new KeyValuePair<string, Action>("colour_is_cl1", ColourIsCl1));
            tests.Add(new KeyValuePair<string, Action>("colour_masked", ColourMasked));
            tests.Add(new KeyValuePair<string, Action>("static_colour_never_stalls", StaticColourNeverStalls));
            tests.Add(new KeyValuePair<string, Action>("animation_stall_falls_back", AnimationStallFallsBack));
            tests.Add(new KeyValuePair<string, Action>("duplicate_frame_still_stalls", DuplicateFrameStillStalls));
            tests.Add(new KeyValuePair<string, Action>("not_live_uses_clock_until_next_colour", NotLiveUsesClockUntilNextColour));
            tests.Add(new KeyValuePair<string, Action>("live_without_colours_uses_clock", LiveWithoutColoursUsesClock));
            tests.Add(new KeyValuePair<string, Action>("null_and_empty_ignored", NullAndEmptyIgnored));
            tests.Add(new KeyValuePair<string, Action>("reset_clears", ResetClears));
            tests.Add(new KeyValuePair<string, Action>("reconnect_after_stall_keeps_last_colour", ReconnectAfterStallKeepsLastColour));
        }

        private static void NewStateHasNoColour()
        {
            FeedState f = new FeedState();
            int c;
            Check.True(!f.TryGetColor(0, out c), "no colour before anything arrives");
        }

        private static void ColourIsCl1()
        {
            FeedState f = new FeedState();
            f.OnColors(new[] { Red, Green, Blue, Green, Blue }, 1000);
            int c;
            Check.True(f.TryGetColor(1500, out c), "has a colour");
            Check.Equal(Red, c, "colour");
        }

        private static void ColourMasked()
        {
            FeedState f = new FeedState();
            f.OnColors(new[] { 0x7F0000FF }, 0);
            int c;
            Check.True(f.TryGetColor(0, out c), "has a colour");
            Check.Equal(0x0000FF, c, "colour");
        }

        private static void StaticColourNeverStalls()
        {
            FeedState f = new FeedState();
            f.OnColors(new[] { Red }, 1000);
            f.OnColors(new[] { Red }, 1100);
            int c;
            Check.True(f.TryGetColor(61100, out c), "still has a colour after a minute of silence");
            Check.Equal(Red, c, "colour");
            Check.True(!f.IsStalled(61100), "not stalled");
        }

        private static void AnimationStallFallsBack()
        {
            FeedState f = new FeedState();
            f.OnColors(new[] { Red }, 1000);
            f.OnColors(new[] { Green }, 1100);
            int c;
            Check.True(f.TryGetColor(6100, out c), "has a colour exactly 5 s after the last one");
            Check.True(!f.TryGetColor(6101, out c), "no colour once more than 5 s have passed");
            Check.True(f.IsStalled(6101), "stalled");
        }

        private static void DuplicateFrameStillStalls()
        {
            // An animation that repeats one frame and then goes quiet has still stopped.
            FeedState f = new FeedState();
            f.OnColors(new[] { Red }, 1000);
            f.OnColors(new[] { Green }, 1100);
            f.OnColors(new[] { Green }, 1150);
            int c;
            Check.True(f.TryGetColor(6150, out c), "has a colour 5 s after the last one");
            Check.True(f.IsStalled(6151), "stalled once more than 5 s have passed");
        }

        private static void NotLiveUsesClockUntilNextColour()
        {
            FeedState f = new FeedState();
            f.OnColors(new[] { Red }, 1000);
            f.OnLive(false, 1200);
            int c;
            Check.True(!f.TryGetColor(1300, out c), "no colour while not live");
            f.OnColors(new[] { Blue }, 1400);
            Check.True(f.TryGetColor(1500, out c), "a new colour means live again");
            Check.Equal(Blue, c, "colour");
        }

        private static void LiveWithoutColoursUsesClock()
        {
            FeedState f = new FeedState();
            f.OnLive(true, 0);
            int c;
            Check.True(!f.TryGetColor(100, out c), "live but nothing to show yet");
        }

        private static void NullAndEmptyIgnored()
        {
            FeedState f = new FeedState();
            f.OnColors(new[] { Red }, 1000);
            f.OnColors(null, 1100);
            f.OnColors(new int[0], 1200);
            int c;
            Check.True(f.TryGetColor(1300, out c), "still has a colour");
            Check.Equal(Red, c, "colour");
        }

        // Synapse switched from an animation to a static colour: it sends one last frame, then nothing.
        // After a reconnect that works, the last colour stays, as a static colour that never stalls.
        private static void ReconnectAfterStallKeepsLastColour()
        {
            FeedState f = new FeedState();
            f.OnColors(new[] { Red }, 1000);
            f.OnColors(new[] { Green }, 1100);
            Check.True(f.IsStalled(6101), "stalled first");
            f.KeepLastColour(6101);
            int c;
            Check.True(f.TryGetColor(6102, out c), "has a colour straight after the reconnect");
            Check.Equal(Green, c, "the last colour");
            Check.True(!f.IsStalled(600000), "never stalls again without new frames");
            Check.True(f.TryGetColor(600000, out c), "still has the colour ten minutes later");
            f.OnColors(new[] { Blue }, 600100);
            Check.True(f.TryGetColor(600200, out c), "new frames take over");
            Check.Equal(Blue, c, "the new colour");
        }

        private static void ResetClears()
        {
            FeedState f = new FeedState();
            f.OnColors(new[] { Red }, 1000);
            f.Reset();
            int c;
            Check.True(!f.TryGetColor(1100, out c), "no colour after a reset");
        }
    }
}
