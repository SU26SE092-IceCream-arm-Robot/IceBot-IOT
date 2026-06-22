using System;
using System.IO;

namespace IceBot.Config
{
    internal static class AppConfig
    {
        public const string DefaultRobotIp = "192.168.58.2";

        /// <summary>
        /// Scripts to run in order when user presses ENTER (phase 1 test).
        /// </summary>
        public static readonly string[] TestScriptQueue =
        {
            "lay_coc.lua"
        };

        public static string GetWorkflowDirectory()
        {
            var workflowNextToExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "workflow");
            if (Directory.Exists(workflowNextToExe))
            {
                return workflowNextToExe;
            }

            var repoWorkflow = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "workflow"));
            if (Directory.Exists(repoWorkflow))
            {
                return repoWorkflow;
            }

            return workflowNextToExe;
        }
    }
}
