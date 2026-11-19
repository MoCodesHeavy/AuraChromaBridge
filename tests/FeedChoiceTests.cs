using System;
using System.Collections.Generic;

namespace AuraChromaBridge.Tests
{
    // What setup writes to razer_app_id; null = leave the file alone.
    internal static class FeedChoiceTests
    {
        private const string Asus = "{4c24ad2b-d0a6-409f-b895-ec7632f3bee6}";

        public static void AddTo(List<KeyValuePair<string, Action>> tests)
        {
            tests.Add(new KeyValuePair<string, Action>("feed_ticked_fresh_is_auto", TickedFreshIsAuto));
            tests.Add(new KeyValuePair<string, Action>("feed_ticked_keeps_hand_set_id", TickedKeepsHandSetId));
            tests.Add(new KeyValuePair<string, Action>("feed_ticked_auto_stays_auto", TickedAutoStaysAuto));
            tests.Add(new KeyValuePair<string, Action>("feed_unticked_is_off", UntickedIsOff));
            tests.Add(new KeyValuePair<string, Action>("feed_disabled_leaves_existing_value", DisabledLeavesExistingValue));
            tests.Add(new KeyValuePair<string, Action>("feed_disabled_fresh_is_off", DisabledFreshIsOff));
        }

        private static void TickedFreshIsAuto()
        {
            Check.Equal("auto", FeedChoice.Value(true, true, null), "value");
        }

        private static void TickedKeepsHandSetId()
        {
            Check.Equal(Asus, FeedChoice.Value(true, true, Asus), "value");
        }

        private static void TickedAutoStaysAuto()
        {
            Check.Equal("auto", FeedChoice.Value(true, true, "auto"), "value");
        }

        private static void UntickedIsOff()
        {
            Check.Equal("off", FeedChoice.Value(false, true, Asus), "value");
        }

        // Upgrade on a PC where setup didn't find Razer: the option was greyed out, so the person never chose.
        private static void DisabledLeavesExistingValue()
        {
            Check.True(FeedChoice.Value(false, false, Asus) == null, "a hand-set ID is left alone");
            Check.True(FeedChoice.Value(false, false, "auto") == null, "auto is left alone");
        }

        private static void DisabledFreshIsOff()
        {
            Check.Equal("off", FeedChoice.Value(false, false, null), "value");
        }
    }
}
