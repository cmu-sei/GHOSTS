using System;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading;
using NLog;

namespace ghosts.client.linux.Infrastructure
{

    internal class BashExecute
    {

        public static Logger Log;
        public string filename;
        public string id;
        public string windowTitle;


        public BashExecute(Logger aLog)
        {
            Log = aLog;
        }

        private void OutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            Log.Trace($"Social:: STDOUT from bash process: {outLine.Data}");
            return;
        }

        private static void ErrorHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            Log.Trace($"Social:: STDERR output from bash process: {outLine.Data}");
            return;
        }

        private string ExecuteBashCommand(string id, string command)
        {
            var escapedArgs = command.Replace("\"", "\\\"");


            var p = new Process();
            //p.EnableRaisingEvents = false;
            p.StartInfo.FileName = "bash";
            p.StartInfo.Arguments = $"-c \"{escapedArgs}\"";
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            //* Set your output and error (asynchronous) handlers
            p.OutputDataReceived += OutputHandler;
            p.ErrorDataReceived += ErrorHandler;
            p.StartInfo.CreateNoWindow = true;
            Log.Trace($"{id}:: Spawning {p.StartInfo.FileName} with command {escapedArgs}");
            p.Start();

            var Result = "";
            while (!p.StandardOutput.EndOfStream)
            {
                Result += p.StandardOutput.ReadToEnd();
            }

            p.WaitForExit();
            Log.Trace($"Social:: Bash command output: {Result}");
            return Result;
        }

        public void AttachFile()
        {
            try
            {
                var cmd = $"xdotool search -name '{windowTitle}' windowfocus type '{filename}' ";
                ExecuteBashCommand(id, cmd);
                Thread.Sleep(2000);
                cmd = $"xdotool search -name '{windowTitle}' windowfocus key KP_Enter";
                ExecuteBashCommand(id, cmd);
                Thread.Sleep(2000);
                // Check if the window has closed
                cmd = $"xdotool search -name '{windowTitle}'";
                var result = ExecuteBashCommand(id, cmd);
                if (result != "")
                {
                    // close the window
                    cmd = $"xdotool search -name '{windowTitle}' windowfocus key alt+c";
                    ExecuteBashCommand(id, cmd);
                    Thread.Sleep(500);
                }
                return;
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }


    }


    public class LinuxSupport
    {


        public static Logger Log;
        private BashExecute runner = null;


        public LinuxSupport(Logger aLog)
        {
            Log = aLog;
        }

        public bool AttachFileUsingThread(string id, string filename, string windowTitle, int timeoutSeconds, int retries)
        {
            // Use a thread in case the xdotool execution hangs

            runner ??= new BashExecute(Log);
            runner.id = id;
            runner.windowTitle = windowTitle;
            runner.filename = filename;
            var count = 0;
            while (count < retries + 1)
            {
                Thread t = new Thread(new ThreadStart(runner.AttachFile));
                t.Start();
                var totalTime = 0;
                while (totalTime < timeoutSeconds)
                {
                    Thread.Sleep(10000);
                    if (!t.IsAlive) break;
                    totalTime += 10;
                }
                if (t.IsAlive)
                {
                    t.Abort();
                    Thread.Sleep(5000);
                    retries += 1;
                }
                else
                {
                    break;
                }
            }

            return (count < retries + 1);

        }


    }


}
