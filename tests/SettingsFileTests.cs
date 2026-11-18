using System;
using System.Collections.Generic;
using System.IO;

namespace AuraChromaBridge.Tests
{
    internal static class SettingsFileTests
    {
        private const string Asus = "{4C24AD2B-D0A6-409F-B895-EC7632F3BEE6}";

        public static void AddTo(List<KeyValuePair<string, Action>> tests)
        {
            tests.Add(new KeyValuePair<string, Action>("get_value_reads_key", GetValueReadsKey));
            tests.Add(new KeyValuePair<string, Action>("get_value_ignores_comments_and_case", GetValueIgnoresCommentsAndCase));
            tests.Add(new KeyValuePair<string, Action>("get_value_strips_inline_comment", GetValueStripsInlineComment));
            tests.Add(new KeyValuePair<string, Action>("get_value_missing_is_null", GetValueMissingIsNull));
            tests.Add(new KeyValuePair<string, Action>("set_value_replaces_line_keeps_others", SetValueReplacesLineKeepsOthers));
            tests.Add(new KeyValuePair<string, Action>("set_value_appends_when_missing", SetValueAppendsWhenMissing));
            tests.Add(new KeyValuePair<string, Action>("set_value_replaces_only_first", SetValueReplacesOnlyFirst));
            tests.Add(new KeyValuePair<string, Action>("file_set_then_get_round_trips", FileSetThenGetRoundTrips));
        }

        private static void GetValueReadsKey()
        {
            string[] lines = { "; comment", "razer_app_id = auto", "dsx = on" };
            Check.Equal("auto", SettingsFile.GetValue(lines, "razer_app_id"), "value");
        }

        private static void GetValueIgnoresCommentsAndCase()
        {
            string[] lines = { "; razer_app_id = off", "RAZER_APP_ID = " + Asus };
            Check.Equal(Asus, SettingsFile.GetValue(lines, "razer_app_id"), "value");
        }

        private static void GetValueStripsInlineComment()
        {
            string[] lines = { "razer_app_id = auto ; tried" };
            Check.Equal("auto", SettingsFile.GetValue(lines, "razer_app_id"), "value");
        }

        private static void GetValueMissingIsNull()
        {
            string[] lines = { "dsx = on" };
            Check.True(SettingsFile.GetValue(lines, "razer_app_id") == null, "missing key gives null");
        }

        private static void SetValueReplacesLineKeepsOthers()
        {
            string[] lines = { "; Follow Synapse", "razer_app_id = auto", "", "dsx = on" };
            string[] result = SettingsFile.SetValue(lines, "razer_app_id", "off");
            Check.Equal(4, result.Length, "line count");
            Check.Equal("; Follow Synapse", result[0], "line 1");
            Check.Equal("razer_app_id = off", result[1], "line 2");
            Check.Equal("", result[2], "line 3");
            Check.Equal("dsx = on", result[3], "line 4");
        }

        private static void SetValueAppendsWhenMissing()
        {
            string[] result = SettingsFile.SetValue(new[] { "dsx = on" }, "razer_app_id", "auto");
            Check.Equal(2, result.Length, "line count");
            Check.Equal("dsx = on", result[0], "line 1");
            Check.Equal("razer_app_id = auto", result[1], "line 2");
        }

        private static void SetValueReplacesOnlyFirst()
        {
            string[] lines = { "razer_app_id = auto", "razer_app_id = " + Asus };
            string[] result = SettingsFile.SetValue(lines, "razer_app_id", "off");
            Check.Equal("razer_app_id = off", result[0], "first");
            Check.Equal("razer_app_id = " + Asus, result[1], "second unchanged");
        }

        private static void FileSetThenGetRoundTrips()
        {
            string path = Path.Combine(Path.GetTempPath(), "acb-selftest-" + Guid.NewGuid().ToString("N") + ".ini");
            try
            {
                File.WriteAllText(path, "dsx = on\r\n");
                SettingsFile.Set(path, "razer_app_id", "auto");
                Check.Equal("auto", SettingsFile.Get(path, "razer_app_id"), "value read back");
                string text = File.ReadAllText(path);
                Check.True(text.Contains("dsx = on\r\nrazer_app_id = auto"), "CRLF line endings kept");
            }
            finally
            {
                try { File.Delete(path); } catch (Exception) { }
            }
        }
    }
}
