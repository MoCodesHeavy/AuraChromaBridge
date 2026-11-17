using System;
using System.Collections.Generic;

namespace AuraChromaBridge.Tests
{
    internal static class RelaunchTests
    {
        public static void AddTo(List<KeyValuePair<string, Action>> tests)
        {
            tests.Add(new KeyValuePair<string, Action>("plain_args", PlainArgs));
            tests.Add(new KeyValuePair<string, Action>("quotes_spaces", QuotesSpaces));
            tests.Add(new KeyValuePair<string, Action>("trailing_backslash_doubled", TrailingBackslashDoubled));
            tests.Add(new KeyValuePair<string, Action>("replaces_old_count", ReplacesOldCount));
        }

        private static void PlainArgs()
        {
            Check.Equal("run relaunch=1", Relauncher.BuildArguments(new[] { "run" }, 1), "arguments");
        }

        private static void QuotesSpaces()
        {
            Check.Equal(@"run ""data=C:\My Folder"" relaunch=1",
                Relauncher.BuildArguments(new[] { "run", @"data=C:\My Folder" }, 1), "arguments");
        }

        private static void TrailingBackslashDoubled()
        {
            Check.Equal(@"run ""data=C:\My Folder\\"" relaunch=1",
                Relauncher.BuildArguments(new[] { "run", @"data=C:\My Folder\" }, 1), "arguments");
        }

        private static void ReplacesOldCount()
        {
            Check.Equal("run relaunch=3", Relauncher.BuildArguments(new[] { "run", "relaunch=2" }, 3), "arguments");
        }
    }
}
