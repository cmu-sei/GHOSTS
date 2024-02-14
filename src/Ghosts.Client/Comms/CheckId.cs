// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Client.Infrastructure;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using NLog;
using System;
using System.IO;
using System.Net;

namespace Ghosts.Client.Comms;

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

    private string _id = string.Empty;

    public CheckId(bool checkInitialTimeline = false)
    {
        _log.Trace($"CheckId instantiated with ID: {Id}");

        try
        {
            if (checkInitialTimeline)
            {
                using var client = GetClient();
                TimelineBuilder.CheckForUrlTimeline(client, Program.Configuration.Timeline.Location);
            }
        }
        catch
        {
            _log.Error("configuration doesn't have timeline location, update your application.json to latest please");
        }
    }

    private WebClient GetClient()
    {
        var machine = new ResultMachine();
        GuestInfoVars.Load(machine);
        return WebClientBuilder.Build(machine, !string.IsNullOrEmpty(_id));
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
                    if (DateTime.Now < Program.LastChecked.AddMinutes(5))
                    {
                        _log.Error("Skipping Check for ID from server, too many requests in a short amount of time...");
                        return string.Empty;
                    }

                    Program.LastChecked = DateTime.Now;
                    return Run();
                }
                Id = File.ReadAllText(IdFile);
                return _id;
            }
            catch
            {
                _log.Error("No ID file");
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
        // ignore all certs
        ServicePointManager.ServerCertificateValidationCallback += (_, _, _, _) => true;

        var s = string.Empty;

        if (!Program.Configuration.Id.IsEnabled)
        {
            return s;
        }

        using var client = GetClient();

        try
        {
            //call home
            try
            {
                using var reader = new StreamReader(client.OpenRead(Program.ConfigurationUrls.Id) ?? throw new InvalidOperationException("CheckID client is null"));
                s = reader.ReadToEnd();
                _log.Debug("ID Received");
            }
            catch (WebException wex)
            {
                if (wex.Message.StartsWith("The remote name could not be resolved:"))
                {
                    _log.Debug($"API not reachable: {wex.Message}");
                }
                else if (((HttpWebResponse)wex.Response).StatusCode == HttpStatusCode.NotFound)
                {
                    _log.Debug($"No ID returned! {wex.Message}");
                }
            }
            catch (Exception e)
            {
                _log.Error($"General comms exception: {e.Message}");
            }
        }
        catch (Exception e)
        {
            _log.Error($"Cannot connect to API: {e.Message}");
        }

        WriteId(s);

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

            //save returned id
            File.WriteAllText(ApplicationDetails.InstanceFiles.Id, id);
        }
    }
}