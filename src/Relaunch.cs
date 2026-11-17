// Lets the background copy (no window) start a fresh copy of itself when the watchdog has to stop it.
// Windows' scheduled task won't do this: a program that started and then exited with an error code
// counts as a successful run, so "restart on failure" never fires. The windowed copy doesn't need
// this, because 3-run-bridge.cmd restarts it when it exits with code 3.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace AuraChromaBridge
{
    internal static class Relauncher
    {
        public const int MaxRelaunches = 5;
        private const string CountKey = "relaunch=";

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        /// <summary>True for the windowed copy, false for the background copy.</summary>
        public static bool HasConsole()
        {
            try
            {
                return GetConsoleWindow() != IntPtr.Zero;
            }
            catch (Exception)
            {
                return true;   // can't tell: don't relaunch, the safer side
            }
        }

        /// <summary>The same arguments with relaunch=nextCount, quoted the way Windows splits them again.</summary>
        public static string BuildArguments(string[] args, int nextCount)
        {
            List<string> parts = new List<string>();
            foreach (string arg in args)
            {
                if (arg.StartsWith(CountKey, StringComparison.OrdinalIgnoreCase)) continue;
                parts.Add(Quote(arg));
            }
            parts.Add(CountKey + nextCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return string.Join(" ", parts.ToArray());
        }

        /// <summary>Starts a fresh copy of this exe with the same arguments. False if it won't or can't.</summary>
        public static bool TryRelaunch(string reason)
        {
            if (Program.RelaunchCount >= MaxRelaunches)
            {
                Log.Line("Restarted {0} times already - not restarting again.", MaxRelaunches);
                return false;
            }
            try
            {
                ProcessStartInfo start = new ProcessStartInfo(Assembly.GetEntryAssembly().Location,
                    BuildArguments(Program.Args, Program.RelaunchCount + 1));
                start.UseShellExecute = false;
                start.WorkingDirectory = Environment.CurrentDirectory;
                Log.Line("Starting a fresh copy because {0}.", reason);   // before it starts, so the new copy appends after it
                Process.Start(start);
                return true;
            }
            catch (Exception ex)
            {
                Log.Line("Could not start a fresh copy: {0}", Program.Describe(ex));
                return false;
            }
        }

        // CommandLineToArgvW rules: quote when needed; backslashes are literal except right before a quote,
        // where each one must be doubled (and the quote itself escaped).
        private static string Quote(string arg)
        {
            if (arg.Length > 0 && arg.IndexOfAny(new[] { ' ', '\t', '"' }) < 0) return arg;

            StringBuilder sb = new StringBuilder("\"");
            int backslashes = 0;
            foreach (char c in arg)
            {
                if (c == '\\')
                {
                    backslashes++;
                    continue;
                }
                if (c == '"')
                {
                    sb.Append('\\', backslashes * 2 + 1);
                }
                else
                {
                    sb.Append('\\', backslashes);
                }
                backslashes = 0;
                sb.Append(c);
            }
            sb.Append('\\', backslashes * 2);
            sb.Append('"');
            return sb.ToString();
        }
    }
}
