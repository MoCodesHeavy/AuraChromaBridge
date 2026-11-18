// Finds the data folder (settings.ini and logs) that an existing "AuraChromaBridge" scheduled task uses,
// so the installer keeps an earlier install's settings where they are.

using System;
using System.Diagnostics;

namespace AuraChromaBridge
{
    internal static class TaskInfo
    {
        public const string TaskName = "AuraChromaBridge";

        /// <summary>The data="..." folder from the task's &lt;Arguments&gt;, or null.</summary>
        public static string DataFolderFromTaskXml(string xml)
        {
            if (string.IsNullOrEmpty(xml)) return null;
            int open = xml.IndexOf("<Arguments>", StringComparison.OrdinalIgnoreCase);
            if (open < 0) return null;
            int start = open + "<Arguments>".Length;
            int close = xml.IndexOf("</Arguments>", start, StringComparison.OrdinalIgnoreCase);
            if (close < 0) return null;
            string args = Decode(xml.Substring(start, close - start));

            int at = args.IndexOf("data=", StringComparison.OrdinalIgnoreCase);
            if (at < 0) return null;
            string rest = args.Substring(at + 5);
            string value;
            if (rest.StartsWith("\""))
            {
                int end = rest.IndexOf('"', 1);
                value = end > 0 ? rest.Substring(1, end - 1) : rest.Substring(1);
            }
            else
            {
                int end = rest.IndexOf(' ');
                value = end >= 0 ? rest.Substring(0, end) : rest;
            }
            value = value.Trim();
            return value.Length > 0 ? value : null;
        }

        /// <summary>The data folder of the installed task, or null when there is no task (or schtasks can't be run).</summary>
        public static string ExistingDataFolder()
        {
            try
            {
                ProcessStartInfo start = new ProcessStartInfo("schtasks", "/query /tn " + TaskName + " /xml");
                start.UseShellExecute = false;
                start.RedirectStandardOutput = true;
                start.RedirectStandardError = true;
                start.CreateNoWindow = true;
                using (Process p = Process.Start(start))
                {
                    string xml = p.StandardOutput.ReadToEnd();
                    p.StandardError.ReadToEnd();
                    p.WaitForExit();
                    return p.ExitCode == 0 ? DataFolderFromTaskXml(xml) : null;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string Decode(string text)
        {
            return text.Replace("&quot;", "\"").Replace("&apos;", "'").Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&");
        }
    }
}
