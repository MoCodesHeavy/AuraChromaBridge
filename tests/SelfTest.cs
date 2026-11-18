// Self-test for the parts of AuraChromaBridge that don't need Razer or ASUS hardware.
// Build and run on Windows with:  build.cmd selftest   then   AuraChromaBridge-selftest.exe

using System;
using System.Collections.Generic;
using System.Globalization;

namespace AuraChromaBridge.Tests
{
    internal static class SelfTest
    {
        private static int Main()
        {
            List<KeyValuePair<string, Action>> tests = new List<KeyValuePair<string, Action>>();
            AppIdsTests.AddTo(tests);
            SettingsTests.AddTo(tests);
            FeedStateTests.AddTo(tests);
            ClockTests.AddTo(tests);
            RelaunchTests.AddTo(tests);
            NativeCallbacksTests.AddTo(tests);
            LogTests.AddTo(tests);
            FeedReadyTests.AddTo(tests);
            SettingsFileTests.AddTo(tests);
            TaskInfoTests.AddTo(tests);

            int passed = 0, failed = 0;
            foreach (KeyValuePair<string, Action> test in tests)
            {
                try
                {
                    test.Value();
                    Console.WriteLine("PASS " + test.Key);
                    passed++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("FAIL " + test.Key + ": " + ex.Message);
                    failed++;
                }
            }
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0} passed, {1} failed", passed, failed));
            return failed;
        }
    }

    internal sealed class CheckFailed : Exception
    {
        public CheckFailed(string message) : base(message)
        {
        }
    }

    internal static class Check
    {
        public static void Equal<T>(T expected, T actual, string what)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new CheckFailed(string.Format(CultureInfo.InvariantCulture,
                    "{0}: expected <{1}> but got <{2}>", what, expected, actual));
            }
        }

        public static void True(bool value, string what)
        {
            if (!value) throw new CheckFailed(what + ": expected true");
        }
    }
}
