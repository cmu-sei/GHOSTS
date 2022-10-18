using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Renci.SshNet;
using Ghosts.Client.Infrastructure;

namespace Ghosts.Client.Infrastructure
{
    /// <summary>
    /// This class provides SSH support using Renci.SshNet
    /// 
    /// </summary>
    public class SshSupport
    {

        public string[] ValidExts { get; set; } = null;
        public int TimeBetweenCommandsMax { get; set; } = 0;
        public int TimeBetweenCommandsMin { get; set; } = 0;

        public int CommandTimeout { get; set; } = 1000;

        internal static readonly Random _random = new Random();

        private string GetRandomDirectory(ShellStream client)
        {
            client.WriteLine("ls -ld */ ");  //write command to client
            string cmdout = this.GetSshCommandOutput(client,  false);
            cmdout = cmdout.Replace("\r", "");
            string[] lines = cmdout.ToString().Split('\n');
            List<string> dirs = new List<string>();
            if (lines.Length > 2)
            {
                //must have at least three lines as first line is command, last line is prompt
                int i = 0;
                foreach (string line in lines)
                {
                    i += 1;
                    if (i == 1 || i == lines.Length)
                    {
                        continue;//skip first, last lines
                    }
                    if (System.Text.RegularExpressions.Regex.IsMatch(line, "^d"))
                    {
                        string[] words = line.Split(null);  //split on whitespace
                        //for some reason, some of the words can be null strings. WUT. So can't check exact number.
                        if (words.Length > 8)
                        {
                            var dirName = words[words.Length-1]; //get last entry
                            dirName = dirName.Replace("/", "");
                            dirs.Add(dirName);
                        }
                    }
                   

                }
            }
            if (dirs.Count > 0)
            {
                return dirs[_random.Next(0, dirs.Count)];
            }

            return null;
        }

        /// <summary>
        /// Replaces reserved words in command string with a value
        /// Reserved words are marked in command string like [reserved_word]
        /// Supported reserved words:
        ///  remotedirectory -- returns a random directory from the remote host
        ///  randomname -- generates a random ASCII lowercase string
        ///  randomext -- selects a random extension from the set of random extensions
        ///  
        /// 
        /// This may require execution and parsing of an internal SSH command before returning
        /// the new command
        /// </summary> 
        /// <param name="cmd"></param>  - string parse for reserved words
        /// <returns></returns>
        private string ParseSshCmd(ShellStream client, string cmd)
        {
            string currentcmd = cmd;
            if (currentcmd.Contains("[remotedirectory]"))
            {
                var dir = this.GetRandomDirectory(client);
                if (dir != null) currentcmd = currentcmd.Replace("[remotedirectory]", dir);
            }


            return currentcmd;
        }

        /// <summary>
        /// Method <c>GetSshCommandOutput</c> uses ShellStream to run a command because the channel model does not have any
        /// shell context, ie. if you cd to a directory, the  next command still runs in the 
        /// home directory. This  implementation using SshStream just uses long timeouts
        /// to wait for data since for a traffic generator do not care about performance
        /// </summary>
        /// <param name="client"></param>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public string GetSshCommandOutput(ShellStream client, bool skiptimeout)
        {

            
            //read data until timeout reached
            long startTimeMs = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            string CmdData = "";
            while (true)
            {
                if (client != null && client.DataAvailable)
                {
                    string strData = client.Read();
                    CmdData = CmdData + strData;
                    startTimeMs = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                }
                if ((DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond - startTimeMs) > this.CommandTimeout)
                {
                    break;  // done
                }
                Thread.Sleep(50);
            }
            //at this point command timeout reached, this.CmdData has the output data.
            //before returning, wait random time.
            if (!skiptimeout && this.TimeBetweenCommandsMin != 0 && this.TimeBetweenCommandsMax != 0 && this.TimeBetweenCommandsMin < this.TimeBetweenCommandsMax)
            {
                Thread.Sleep(_random.Next(this.TimeBetweenCommandsMin, this.TimeBetweenCommandsMax));
            }
            return CmdData;
        }

        /// <summary>
        /// Method <c>RunSshCommand</c> will replace reserved words in cmd before executing the command
        /// </summary>
        public string RunSshCommand(ShellStream client, string cmd)
        {
            string newcmd = this.ParseSshCmd(client, cmd);
            client.WriteLine(newcmd);  //write command to client
            return this.GetSshCommandOutput(client, false);
        }


    }
}
