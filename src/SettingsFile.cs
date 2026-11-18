// Reads or changes one key in settings.ini and leaves every other line - comments included - as it was.
// Used by the installer, which must never replace someone's settings file.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AuraChromaBridge
{
    internal static class SettingsFile
    {
        /// <summary>The value of the first line setting this key (same rules as Settings.Load: ';' starts a comment), or null.</summary>
        public static string GetValue(string[] lines, string key)
        {
            int i = Find(lines, key);
            if (i < 0) return null;
            string line = StripComment(lines[i]);
            return line.Substring(line.IndexOf('=') + 1).Trim();
        }

        /// <summary>The same lines with the first one setting this key replaced by "key = value", or "key = value" added at the end.</summary>
        public static string[] SetValue(string[] lines, string key, string value)
        {
            List<string> result = new List<string>(lines);
            string line = key + " = " + value;
            int i = Find(lines, key);
            if (i >= 0) result[i] = line;
            else result.Add(line);
            return result.ToArray();
        }

        public static string Get(string path, string key)
        {
            if (!File.Exists(path)) return null;
            return GetValue(File.ReadAllLines(path), key);
        }

        /// <summary>Changes one key in the file (created if missing), with Windows line endings.</summary>
        public static void Set(string path, string key, string value)
        {
            string[] lines = File.Exists(path) ? File.ReadAllLines(path) : new string[0];
            string[] updated = SetValue(lines, key, value);
            StringBuilder sb = new StringBuilder();
            foreach (string l in updated) sb.Append(l).Append("\r\n");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }

        private static int Find(string[] lines, string key)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                string line = StripComment(lines[i]);
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                if (line.Substring(0, eq).Trim().Equals(key, StringComparison.OrdinalIgnoreCase)) return i;
            }
            return -1;
        }

        private static string StripComment(string line)
        {
            int comment = line.IndexOf(';');
            return comment >= 0 ? line.Substring(0, comment) : line;
        }
    }
}
