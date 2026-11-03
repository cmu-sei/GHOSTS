// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Diagnostics;
using System.Threading;
using NLog;

namespace Ghosts.Client.Universal.Infrastructure
{
    internal class BashExecute
    {
        public static Logger Log;
        public string filename;
        public string id;
        public string windowTitle;
        public bool needRestart = false;

        public BashExecute(Logger aLog)
        {
            Log = aLog;
        }

        public bool GetNeedRestart()
        {
            return needRestart;
        }

        private void OutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            Log.Trace($"{id}:: STDOUT from bash process: {outLine.Data}");
            return;
        }

        private static void ErrorHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            Log.Trace($"STDERR output from bash process: {outLine.Data}");
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
            Log.Trace($"{id}:: Bash command output: {Result}");
            return Result;
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

    }
}
