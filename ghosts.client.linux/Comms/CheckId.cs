// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.IO;
using System.Net;
using ghosts.client.linux.Infrastructure;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using NLog;

namespace ghosts.client.linux.Comms
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
            var s = string.Empty;

            if (!Program.Configuration.IdEnabled)
                return s;

            var machine = new ResultMachine();

            //call home
            using (var client = WebClientBuilder.BuildNoId(machine))
            {
                try
                {
                    using (var reader =
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
                Directory.CreateDirectory(ApplicationDetails.InstanceFiles.Path);

            //save returned id
            File.WriteAllText(ConfigFile, s);
            return s;
        }
    }
}
