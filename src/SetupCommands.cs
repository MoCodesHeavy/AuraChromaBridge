// Small commands the installer runs. They print or write a result and exit; no log file, no banner.
//   get-setting KEY [data=DIR] [out=FILE]   the value of KEY in settings.ini (empty if absent)
//   set-setting KEY VALUE [data=DIR]        sets KEY in settings.ini, leaving every other line alone
//   task-data [out=FILE]                    the data folder of the existing AuraChromaBridge task (empty if none)

using System;
using System.IO;
using System.Text;

namespace AuraChromaBridge
{
    internal static class SetupCommands
    {
        public static bool Handles(string mode)
        {
            return mode == "get-setting" || mode == "set-setting" || mode == "task-data";
        }

        /// <summary>Runs one command. out = file to write the result to (null = the console). 0 = success, 1 = error, 2 = usage.</summary>
        public static int Run(string mode, string[] args, string outFile)
        {
            try
            {
                if (mode == "task-data")
                {
                    return Write(TaskInfo.ExistingDataFolder(), outFile);
                }

                string key = Positional(args, 1);
                if (key == null) return Usage();

                if (mode == "get-setting")
                {
                    return Write(SettingsFile.Get(Program.SettingsPath, key), outFile);
                }

                string value = Positional(args, 2);
                if (value == null) return Usage();
                Directory.CreateDirectory(Program.DataDir);
                SettingsFile.Set(Program.SettingsPath, key, value);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("{0}: {1}", mode, ex.Message);
                return 1;
            }
        }

        // The n-th argument that isn't key=value (args[0] is the command itself).
        private static string Positional(string[] args, int n)
        {
            int seen = 0;
            foreach (string arg in args)
            {
                if (arg.IndexOf('=') > 0) continue;
                if (seen == n) return arg.Trim();
                seen++;
            }
            return null;
        }

        private static int Write(string value, string outFile)
        {
            string text = value ?? "";
            if (outFile == null)
            {
                Console.WriteLine(text);
            }
            else
            {
                File.WriteAllText(outFile, text, new UTF8Encoding(false));
            }
            return 0;
        }

        private static int Usage()
        {
            Console.Error.WriteLine("Usage: AuraChromaBridge.exe get-setting KEY [data=DIR] [out=FILE] | set-setting KEY VALUE [data=DIR] | task-data [out=FILE]");
            return 2;
        }
    }
}
