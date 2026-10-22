using System;
using System.Diagnostics;
using System.IO;

namespace MIPVizsga
{
    public static class TraceLogger
    {
        private static bool initialized;

        public static void Initialize()
        {
            if (initialized)
            {
                return;
            }

            string logPath = GetSubmissionFilePath("trace.txt");
            Trace.Listeners.Add(new TextWriterTraceListener(logPath));
            Trace.AutoFlush = true;
            initialized = true;
        }

        public static void LogDatabaseInsert(string tableName, int recordId)
        {
            Initialize();
            Trace.WriteLine(DateTime.Now.ToString("o") + " DB_INSERT table=" + tableName + " id=" + recordId);
        }

        public static void LogProjectAccepted(Project project)
        {
            Initialize();
            string assignments = string.Join(",", project.Assignments.ConvertAll(
                assignment => assignment.Programmer.Id + ":" + assignment.AllocatedHours));
            Trace.WriteLine(DateTime.Now.ToString("o") + " PROJECT_ACCEPTED ProjectId=" + project.Id
                + " Name=" + project.Name.Replace("\r", " ").Replace("\n", " ")
                + " EstimatedBudget=" + project.EstimatedBudget
                + " CalculatedCost=" + project.CalculatedCost
                + " PmEmployeeId=" + project.ProjectManager.Id
                + " PmHours=" + project.PmHours
                + " Developers=" + assignments);
        }

        public static string GetSubmissionFilePath(string fileName)
        {
            DirectoryInfo directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "MIPVizsga.slnx")))
                {
                    string submissionDirectory = Path.Combine(directory.FullName, "submission");
                    Directory.CreateDirectory(submissionDirectory);

                    if (directory.Parent != null)
                    {
                        string previousFile = Path.Combine(directory.Parent.FullName, "submission", fileName);
                        string submissionFile = Path.Combine(submissionDirectory, fileName);
                        if (!File.Exists(submissionFile) && File.Exists(previousFile))
                        {
                            File.Copy(previousFile, submissionFile);
                        }
                    }

                    return Path.Combine(submissionDirectory, fileName);
                }

                directory = directory.Parent;
            }

            string fallbackDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "submission");
            Directory.CreateDirectory(fallbackDirectory);
            return Path.Combine(fallbackDirectory, fileName);
        }
    }
}
