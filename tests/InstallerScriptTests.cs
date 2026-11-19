using System;
using System.Collections.Generic;
using System.IO;

namespace AuraChromaBridge.Tests
{
    // Checks on installer/AuraChromaBridge.iss, which can't run here. Run the self-test from the project folder.
    internal static class InstallerScriptTests
    {
        public static void AddTo(List<KeyValuePair<string, Action>> tests)
        {
            tests.Add(new KeyValuePair<string, Action>("installer_scan_skips_silent", InstallerScanSkipsSilent));
            tests.Add(new KeyValuePair<string, Action>("installer_runs_powershell_by_full_path", InstallerRunsPowershellByFullPath));
        }

        private static string[] Script()
        {
            string path = Path.Combine(Environment.CurrentDirectory, Path.Combine("installer", "AuraChromaBridge.iss"));
            Check.True(File.Exists(path), path + " exists (run from the project folder)");
            return File.ReadAllLines(path);
        }

        // A silent install has nobody to press the scan's "any key", so it must not run the scan.
        private static void InstallerScanSkipsSilent()
        {
            int found = 0;
            foreach (string line in Script())
            {
                if (line.StartsWith("Filename:") && line.Contains("scan pause"))
                {
                    found++;
                    Check.True(line.Contains("skipifsilent"), "the finish-page scan has skipifsilent");
                }
            }
            Check.Equal(1, found, "finish-page scan entries");
        }

        // Setup runs as administrator; a bare name would be looked up in setup's own folder (often Downloads) first.
        private static void InstallerRunsPowershellByFullPath()
        {
            bool full = false;
            foreach (string line in Script())
            {
                Check.True(!line.Contains("Exec('powershell.exe'"), "no Exec of powershell.exe by bare name");
                if (line.Contains("{sys}\\WindowsPowerShell\\v1.0\\powershell.exe")) full = true;
            }
            Check.True(full, "powershell.exe started from System32 by full path");
        }
    }
}
