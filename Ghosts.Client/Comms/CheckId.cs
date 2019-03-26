// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Client.Code;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using NLog;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;

namespace Ghosts.Client.Comms
{
    /// <summary>
    /// The client ID is used in the header to save having to send hostname/user/fqdn/etc. inforamtion with every request
    /// </summary>
    public static class CheckId
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// The actual path to the client id file, specified in application config
        /// </summary>
        public static string ConfigFile = ApplicationDetails.InstanceFiles.Id;

        /// <summary>
        /// Gets the agent's current id from local instance, and if it does not exist, gets an id from the server and saves it locally
        /// </summary>
        public static string Id
        {
            get
            {
                try
                {
                    if (!File.Exists(ConfigFile))
                    {
                        return Run();
                    }
                    return File.ReadAllText(ConfigFile);
                }
                catch
                {
                    _log.Error("config file could not be opened");
                    return string.Empty;
                }
            }
        }

        /// <summary>
        /// API call to get client ID (probably based on hostname, but configurable) and saves it locally
        /// </summary>
        /// <returns></returns>
        private static string Run()
        {
            // ignore all certs
            ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;

            var s = string.Empty;

            if (!Program.Configuration.IdEnabled)
            {
                return s;
            }

            ResultMachine machine = new ResultMachine();

            //if confgigured, try to set machine.name based on some vmtools.exe value
            try
            {
                if (Program.Configuration.IdFormat.Equals("guestinfo", StringComparison.InvariantCultureIgnoreCase))
                {
                    var p = new Process();
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.RedirectStandardOutput = true;
                
                    p.StartInfo.FileName = $"{Program.Configuration.VMWareToolsLocation}";
                    p.StartInfo.Arguments = $"--cmd \"info-get {Program.Configuration.IdFormatKey}\"";

                    p.Start();
                
                    var output = p.StandardOutput.ReadToEnd().Trim();
                    p.WaitForExit();

                    if (!string.IsNullOrEmpty(output))
                    {
                        var o = Program.Configuration.IdFormatValue;
                        o = o.Replace("$formatkeyvalue$", output);
                        o = o.Replace("$machinename$", machine.Name);

                        machine.SetName(o);
                    }
                }
            }
            catch (Exception e)
            {
                _log.Debug(e);
            }

            //call home
            using (WebClient client = WebClientBuilder.BuildNoId(machine))
            {
                try
                {
                    using (StreamReader reader =
                        new StreamReader(client.OpenRead(Program.Configuration.IdUrl)))
                    {
                        s = reader.ReadToEnd();
                        _log.Debug($"{DateTime.Now} - Received client ID");
                    }
                }
                catch (WebException wex)
                {
                    if (((HttpWebResponse)wex.Response).StatusCode == HttpStatusCode.NotFound)
                    {
                        _log.Debug("No ID returned!", wex);
                    }
                }
                catch (Exception e)
                {
                    _log.Error(e);
                }
            }

            s = s.Replace("\"", "");

            if (!Directory.Exists(ApplicationDetails.InstanceFiles.Path))
            {
                Directory.CreateDirectory(ApplicationDetails.InstanceFiles.Path);
            }

            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }

            //save returned id
            File.WriteAllText(ConfigFile, s);
            return s;
        }
    }
}
