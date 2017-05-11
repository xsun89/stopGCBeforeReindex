using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace StopGCBeforReindex
{
    class Program
    {
        private static readonly bool IsDebug = bool.Parse(ConfigurationManager.AppSettings["isDebug"]);

        private static string womDirStr = @"F:\Logs\Click Portal";
        //private static string womDirStr = @"C:\Temp\stopGC";

        private static string Keyword =
                @"^\d{4}.\d{2}.\d{2}\s\d{2}:\d{2}:\d{2}\s\(\w+\):\s\w+:\sGarbageCollection\s\(\w+\)\s-\sDeleted\s(?<current>\d+)\sof\s(?<total>\d+)\srows.$"
            ;

        private static string stopGCFile = @"C:\Schedulled Tasks\stopGC.bat";
        private static readonly int Interval = int.Parse(ConfigurationManager.AppSettings["interval"]);
        private static readonly int ObjectsLeft = int.Parse((ConfigurationManager.AppSettings["obectsLeft"]));
        private static readonly string stopDate = ConfigurationManager.AppSettings["stopDate"];
        private static readonly int stopTime = int.Parse((ConfigurationManager.AppSettings["stopTime"]));
        private static readonly int IntervalForCheck = Interval * 1000;


        static void Main(string[] args)
        {
            try
            {
                LogMessageToFile("Start Check GC Status");
                FileInfo result = null;
                result = GetLatestWomLogFile(womDirStr);
                LogMessageToFile("processing log file " + result.FullName);
                var dayofToday = DateTime.Now.DayOfWeek;
                if ((dayofToday.ToString() == stopDate && System.DateTime.Now.Hour >= stopTime) ||
                    IsDebug)
                {
                    while (true)
                    {
                        LogMessageToFile("Check GC Status");
                        bool ret = GetKewwordLines(result.FullName);
                        if (ret == true)
                        {
                            LogMessageToFile("Preparing Stop GC at day of hour " + System.DateTime.Now.Hour);
                            break;
                        }
                        Thread.Sleep(IntervalForCheck);
                    }
                    ProcessStopGc();
                }

                LogMessageToFile("End Check GC Status");
            }
            catch (Exception e)
            {
                LogMessageToFile(e.Message);
            }
        }

        private static bool GetKewwordLines(string fullName)
        {
            try
            {
                List<string> allLines = new List<string>();
                using (var filestream = new FileStream(fullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (var file = new StreamReader(filestream))
                    {
                        string oneLine;
                        while ((oneLine = file.ReadLine()) != null)
                        {
                            allLines.Add(oneLine);
                        }
                    }
                }


                var regex = new Regex(Keyword);

                foreach (string line in allLines)
                {
                    Match match = regex.Match(line);
                    if (match.Success)
                    {
                        string current = match.Groups["current"].Value;
                        string total = match.Groups["total"].Value;
                        int currentVal = int.Parse(current);
                        int totalVal = int.Parse(total);
                        if (IsDebug)
                        {
                            LogMessageToFile(currentVal + " " + totalVal);
                        }
                        if (totalVal - currentVal <= ObjectsLeft)
                        {
                            LogMessageToFile(currentVal + " " + totalVal);
                            return true;
                        }
                    }
                }
                LogMessageToFile("Keep processing");
                return false;
            }
            catch (Exception e)
            {
                LogMessageToFile(e.Message);
                throw;
            }
        }

        private static void ProcessStopGc()
        {
            try
            {
                LogMessageToFile("start stop GC");

                using (Process stopGc = Process.Start(stopGCFile))
                {
                    if (stopGc != null) stopGc.WaitForExit();
                }
                LogMessageToFile("end stop GC");
            }
            catch (Exception e)
            {
                LogMessageToFile(e.Message);
                throw;
            }
        }

        private static DateTime GetTimeStamp(List<string> lines)
        {
            var lastItem = lines.LastOrDefault();
            var lastDateTime = DateTime.MinValue;

            if (lastItem != null)
            {
                LogMessageToFile(lastItem);
                var year = int.Parse(lastItem.Substring(0, 4));
                var month = int.Parse(lastItem.Substring(5, 2));
                var day = int.Parse(lastItem.Substring(8, 2));
                var hour = int.Parse(lastItem.Substring(11, 2));
                var mim = int.Parse(lastItem.Substring(14, 2));
                var sec = int.Parse(lastItem.Substring(17, 2));
                lastDateTime = new DateTime(year, month, day, hour, mim, sec);
            }
            return lastDateTime;
        }

        private static FileInfo GetLatestWomLogFile(string womdir)
        {
            FileInfo result = null;
            var directory = new DirectoryInfo(womdir);
            var list = directory.GetFiles("wom*.log");
            if (list.Any())
            {
                result = list.OrderByDescending(f => f.LastWriteTime).First();
            }

            return result;
        }

        private static void LogMessageToFile(string msg)
        {
            string logFilePath = Directory.GetCurrentDirectory() + "\\LogFile.txt";
            var sw = File.AppendText(logFilePath);
            try
            {
                string logLine = string.Format(
                    "{0:G}: {1}.", DateTime.Now, msg);
                sw.WriteLine(logLine);
            }
            finally
            {
                sw.Close();
            }
        }
    }
}