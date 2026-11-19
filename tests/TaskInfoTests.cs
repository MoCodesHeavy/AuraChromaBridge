using System;
using System.Collections.Generic;

namespace AuraChromaBridge.Tests
{
    internal static class TaskInfoTests
    {
        public static void AddTo(List<KeyValuePair<string, Action>> tests)
        {
            tests.Add(new KeyValuePair<string, Action>("task_xml_quoted_data", TaskXmlQuotedData));
            tests.Add(new KeyValuePair<string, Action>("task_xml_unquoted_data", TaskXmlUnquotedData));
            tests.Add(new KeyValuePair<string, Action>("task_xml_path_with_spaces", TaskXmlPathWithSpaces));
            tests.Add(new KeyValuePair<string, Action>("task_xml_without_data_is_null", TaskXmlWithoutDataIsNull));
        }

        // The shape schtasks /query /xml prints for a task made by install-task.ps1.
        private static void TaskXmlQuotedData()
        {
            string xml = "<Exec><Command>C:\\Program Files\\AuraChromaBridge\\AuraChromaBridge-bg.exe</Command>"
                + "<Arguments>run data=&quot;C:\\Users\\Mo\\Documents\\AuraChromaBridge&quot;</Arguments></Exec>";
            Check.Equal(@"C:\Users\Mo\Documents\AuraChromaBridge", TaskInfo.DataFolderFromTaskXml(xml), "data folder");
        }

        private static void TaskXmlUnquotedData()
        {
            Check.Equal(@"C:\Data", TaskInfo.DataFolderFromTaskXml(@"<Arguments>run data=C:\Data</Arguments>"), "data folder");
        }

        private static void TaskXmlPathWithSpaces()
        {
            string xml = @"<Arguments>run data=&quot;C:\My Data\ACB&quot; relaunch=1</Arguments>";
            Check.Equal(@"C:\My Data\ACB", TaskInfo.DataFolderFromTaskXml(xml), "data folder");
        }

        private static void TaskXmlWithoutDataIsNull()
        {
            Check.True(TaskInfo.DataFolderFromTaskXml("<Arguments>run</Arguments>") == null, "no data= gives null");
            Check.True(TaskInfo.DataFolderFromTaskXml("") == null, "empty gives null");
        }
    }
}
