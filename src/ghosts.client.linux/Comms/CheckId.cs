﻿// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

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
    /// The client ID is used in the header to save having to send hostname/user/fqdn/etc. information with every request
    /// </summary>
    public class CheckId
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// The actual path to the client id file, specified in application config
        /// </summary>
        public string IdFile = ApplicationDetails.InstanceFiles.Id;

        private DateTime _lastChecked = DateTime.Now;
        private string _id = string.Empty;

        public CheckId()
        {
            _log.Trace($"CheckId instantiated with ID: {Id}");
        }

        /// <summary>
        /// Gets the agent's current id from local instance, and if it does not exist, gets an id from the server and saves it locally
        /// </summary>
        public string Id
        {
            get
            {
                if (!string.IsNullOrEmpty(_id))
                    return _id;

                try
                {
                    if (!File.Exists(IdFile))
                    {
                        if (DateTime.Now > _lastChecked.AddMinutes(5))
                        {
                            _log.Error(
                                "Skipping Check for ID from server, too many requests in a short amount of time...");
                            return string.Empty;
                        }

                        _lastChecked = DateTime.Now;
                        return Run();
                    }

                    _id = File.ReadAllText(IdFile);
                    return _id;
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Failed to read ID file.");
                    return string.Empty;
                }
            }
            set => _id = value;
        }

        /// <summary>
        /// API call to get client ID (probably based on hostname, but configurable) and saves it locally
        /// </summary>
        /// <returns></returns>
        private string Run()
        {
            // Ignore all certs
            ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;

            var s = string.Empty;

            if (!Program.Configuration.Id.IsEnabled)
            {
                return s;
            }

            var machine = new ResultMachine();

            try
            {
                // Call home
                using (var client = WebClientBuilder.Build(machine))
                {
                    try
                    {
                        using (var reader = new StreamReader(client.OpenRead(Program.ConfigurationUrls.Id)
                                                             ?? throw new InvalidOperationException(
                                                                 "CheckID client is null")))
                        {
                            s = reader.ReadToEnd();
                            _log.Debug("ID Received");
                        }
                    }
                    catch (WebException wex)
                    {
                        if (wex.Message.StartsWith("The remote name could not be resolved:"))
                        {
                            _log.Debug($"API not reachable: {wex.Message}");
                        }
                        else if (wex.Response is HttpWebResponse response &&
                                 response.StatusCode == HttpStatusCode.NotFound)
                        {
                            _log.Debug($"No ID returned! {wex.Message}");
                        }
                    }
                    catch (Exception e)
                    {
                        _log.Error($"General communication exception: {e.Message}");
                    }
                }
            }
            catch (Exception e)
            {
                _log.Error($"Cannot connect to API: {e.Message}");
                return string.Empty;
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

            // Save returned ID
            File.WriteAllText(IdFile, s);
            return s;
        }

        public static void WriteId(string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                id = id.Replace("\"", "");

                if (!Directory.Exists(ApplicationDetails.InstanceFiles.Path))
                {
                    Directory.CreateDirectory(ApplicationDetails.InstanceFiles.Path);
                }

                // Save returned ID
                File.WriteAllText(ApplicationDetails.InstanceFiles.Id, id);
            }
        }
    }
}
