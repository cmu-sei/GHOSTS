using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Renci.SshNet;
using Ghosts.Client.Infrastructure;
using NLog;
using System.IO;

namespace Ghosts.Client.Infrastructure
{
    /// <summary>
    /// This class provides SSH/SFTP support using Renci.SshNet
    /// 
    /// </summary>
    public class SshSupport : SshSftpSupport
    {

        public string[] ValidExts { get; set; } = { "txt", "py", "log", "c", "o", "jpg", "cs", "dll", "so", "zip", "gz", "jar" };
       
        public string uploadDirectory { get; set; } = null;



        private string GetRandomDirectory(ShellStream client)
        {
            client.WriteLine("ls -ld */ ");  //write command to client
            string cmdout = this.GetSshCommandOutput(client, false);
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
                            var dirName = words[words.Length - 1]; //get last entry
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
        ///  randomextension -- selects a random extension from the set of random extensions
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
                else return null;  //this  translation failed, return null
            }
            if (currentcmd.Contains("[randomextension]"))
            {
                currentcmd = currentcmd.Replace("[randomextension]", this.ValidExts[_random.Next(0, this.ValidExts.Length - 1)]);
            }
            if (currentcmd.Contains("[randomname]"))
            {
                currentcmd = currentcmd.Replace("[randomname]", RandomString(3, 15, true));
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
            if (newcmd != null)
            {
                client.WriteLine(newcmd);  //write command to client
                string result = this.GetSshCommandOutput(client, false);
                Log.Trace($"SSH: Success, executed command: {newcmd} on remote host: {HostIp}");
                return result;
            }
            else
            {
                return null;  //can return null if translation of keyword fails
            }
        }

    }


    public abstract class SshSftpSupport
    {
        public static readonly Logger Log = LogManager.GetCurrentClassLogger();
        internal static readonly Random _random = new Random();
        public int TimeBetweenCommandsMax { get; set; } = 0;
        public int TimeBetweenCommandsMin { get; set; } = 0;

        public int CommandTimeout { get; set; } = 1000;

        public string HostIp { get; set; } = null;

        public string GetUploadFilenameBase(string targetDirectory, string searchPattern)
        {
            try
            {
                string[] filelist = Directory.GetFiles(targetDirectory, searchPattern);
                if (filelist.Length > 0) return filelist[_random.Next(0, filelist.Length)];
                else return null;
            }
            catch (ThreadAbortException)
            {
                throw;  //pass up
            }
            catch { } //ignore any errors
            return null;
        }


        public static string RandomString(int min, int max, bool lowercase = false)
        {
            var size = _random.Next(min, max);
            var builder = new StringBuilder(size);
            const int lettersOffset = 26; // A...Z or a..z: length=26  
            char[] others = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' };

            for (var i = 0; i < size; i++)
            {
                var choice = _random.Next(2);
                char offset = choice == 0 ? 'a' : 'A';

                if (i == 0 || _random.Next(10) < 7)
                {
                    var @char = (char)_random.Next(offset, offset + lettersOffset);
                    builder.Append(@char);
                }
                else
                {
                    var @char = others[_random.Next(others.Length)];
                    builder.Append(@char);
                }

            }
            if (lowercase)
            {
                return builder.ToString().ToLower();
            }
            else
            {
                return builder.ToString();
            }
        }

    }

}
