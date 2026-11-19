// What setup writes to razer_app_id for the "Follow Synapse's colours" option.

using System;

namespace AuraChromaBridge
{
    internal static class FeedChoice
    {
        /// <summary>
        /// ticked/enabled: the option's state on the setup page; existing: the file's current value (null if none).
        /// Returns the value to write, or null to leave settings.ini alone (the option was greyed out on an upgrade,
        /// so the person never chose). A hand-set app ID, e.g. Razer's official one, is kept when ticked.
        /// </summary>
        public static string Value(bool ticked, bool enabled, string existing)
        {
            if (!enabled) return existing == null ? "off" : null;
            if (!ticked) return "off";
            Guid id;
            if (existing != null && Guid.TryParse(existing.Trim(), out id)) return existing.Trim();
            return "auto";
        }
    }
}
