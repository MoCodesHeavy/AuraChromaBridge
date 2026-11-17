using System;
using System.Collections.Generic;
using System.IO;

namespace AuraChromaBridge.Tests
{
    internal static class LogTests
    {
        public static void AddTo(List<KeyValuePair<string, Action>> tests)
        {
            tests.Add(new KeyValuePair<string, Action>("log_append_keeps_old_lines", LogAppendKeepsOldLines));
            tests.Add(new KeyValuePair<string, Action>("log_create_replaces_old_lines", LogCreateReplacesOldLines));
        }

        // A restarted copy must keep the lines that say why it was restarted.
        private static void LogAppendKeepsOldLines()
        {
            string text = WriteAfter("old line before the restart", true);
            Check.True(text.Contains("old line before the restart"), "old line kept");
            Check.True(text.Contains("selftest new line"), "new line written");
        }

        private static void LogCreateReplacesOldLines()
        {
            string text = WriteAfter("old line from last time", false);
            Check.True(!text.Contains("old line from last time"), "old line gone");
            Check.True(text.Contains("selftest new line"), "new line written");
        }

        private static string WriteAfter(string oldLine, bool append)
        {
            string path = Path.Combine(Path.GetTempPath(), "acb-selftest-" + Guid.NewGuid().ToString("N") + ".log");
            try
            {
                File.WriteAllText(path, oldLine + Environment.NewLine);
                Log.OpenFile(path, append);
                Log.Line("selftest new line");
                Log.Close();
                return File.ReadAllText(path);
            }
            finally
            {
                Log.Close();
                try { File.Delete(path); } catch (Exception) { }
            }
        }
    }
}
