// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using ghosts.client.linux.Infrastructure;
using ghosts.client.linux.timelineManager;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Ghosts.Domain.Messages.MesssagesForServer;
using Newtonsoft.Json;
using NLog;

namespace ghosts.client.linux.Comms
{
    /// <summary>
    /// Get updates from the C2 server - could be timeline, health, etc.
    /// </summary>
    public static class Updates
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Threaded calls to C2 for updates and to post this client's results of activity
        /// </summary>
        public static void Run()
        {
            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                GetServerUpdates();
            }).Start();

            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                PostClientResults();
            }).Start();
        }

        private static void GetServerUpdates()
        {
            if (!Program.Configuration.ClientUpdates.IsEnabled)
                return;

            var machine = new ResultMachine();

            Thread.Sleep(Program.Configuration.ClientUpdates.CycleSleep);

            while (true)
            {
                try
                {
                    string s = string.Empty;
                    using (var client = WebClientBuilder.Build(machine))
                    {
                        try
                        {
                            using (var reader =
                                new StreamReader(client.OpenRead(Program.Configuration.ClientUpdates.PostUrl)))
                            {
                                s = reader.ReadToEnd();
                                _log.Debug($"{DateTime.Now} - Received new configuration");
                            }
                        }
                        catch (WebException wex)
                        {
                            if (((HttpWebResponse)wex.Response).StatusCode == HttpStatusCode.NotFound)
                            {
                                _log.Debug($"{DateTime.Now} - No new configuration found");
                            }
                        }
                        catch (Exception e)
                        {
                            _log.Error(e);
                        }
                    }

                    if (!string.IsNullOrEmpty(s))
                    {
                        var update = JsonConvert.DeserializeObject<UpdateClientConfig>(s);

                        switch (update.Type)
                        {
                            case UpdateClientConfig.UpdateType.Timeline:
                                TimelineBuilder.SetLocalTimeline(update.Update.ToString());
                                break;
                            case UpdateClientConfig.UpdateType.TimelinePartial:
                                try
                                {
                                    var timeline = JsonConvert.DeserializeObject<Timeline>(update.Update.ToString());

                                    foreach (var timelineHandler in timeline.TimeLineHandlers)
                                    {
                                        _log.Trace($"PartialTimeline found: {timelineHandler.HandlerType}");

                                        foreach (var timelineEvent in timelineHandler.TimeLineEvents)
                                        {
                                            if (string.IsNullOrEmpty(timelineEvent.TrackableId))
                                            {
                                                timelineEvent.TrackableId = Guid.NewGuid().ToString();
                                            }
                                        }

                                        var orchestrator = new Orchestrator();
                                        orchestrator.RunCommand(timelineHandler);
                                    }
                                }
                                catch (Exception exc)
                                {
                                    _log.Debug(exc);
                                }

                                break;
                            case UpdateClientConfig.UpdateType.Health:
                            {
                                var newTimeline = JsonConvert.DeserializeObject<Ghosts.Domain.ResultHealth>(update.Update.ToString());
                                //save to local disk
                                using (var file = File.CreateText(ApplicationDetails.ConfigurationFiles.Health))
                                {
                                    var serializer = new JsonSerializer();
                                    serializer.Formatting = Formatting.Indented;
                                    serializer.Serialize(file, newTimeline);
                                }

                                break;
                            }
                            default:
                            {
                                _log.Debug($"Update {update.Type} has no handler, ignoring...");
                                break;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    _log.Debug("Problem polling for new configuration");
                    _log.Error(e);
                }

                Thread.Sleep(Program.Configuration.ClientUpdates.CycleSleep);
            }
        }

        private static void PostClientResults()
        {
            if (!Program.Configuration.ClientResults.IsEnabled)
                return;

            var fileName = ApplicationDetails.LogFiles.ClientUpdates;
            var cyclesleep = Program.Configuration.ClientResults.CycleSleep;
            var posturl = Program.Configuration.ClientResults.PostUrl;

            var machine = new ResultMachine();

            Thread.Sleep(cyclesleep);

            while (true)
            {
                try
                {
                    if(File.Exists(fileName))
                    {
                        PostResults(fileName, machine, posturl);
                    }
                    else
                    {
                        _log.Trace($"{DateTime.Now} - {fileName} not found - sleeping...");
                    }
                }
                catch (Exception e)
                {
                    _log.Error($"Problem posting logs to server {e}");
                }

                // look for other result files that have not been posted
                try
                {
                    foreach(var file in Directory.GetFiles(Path.GetDirectoryName(fileName)))
                    {
                        if (!file.EndsWith("app.log") && file != fileName)
                        {
                            PostResults(file, machine, posturl, true);
                        }
                    }
                }
                catch (Exception e)
                {
                    _log.Debug($"Problem posting overflow logs from {fileName} to server {posturl}: {e}");
                }

                Thread.Sleep(cyclesleep);
            }
        }

        private static void PostResults(string fileName, ResultMachine machine, string postUrl, bool isDeletable = false)
        {
            var sb = new StringBuilder();
            var data = File.ReadLines(fileName);
            foreach (var d in data)
            {
                sb.AppendLine(d);
            }

            var r = new TransferLogDump();
            r.Log = sb.ToString();

            var payload = JsonConvert.SerializeObject(r);

            if (Program.Configuration.ClientResults.IsSecure)
            {
                payload = Crypto.EncryptStringAes(payload, machine.Name);
                payload = Crypto.Base64Encode(payload);

                var p = new EncryptedPayload();
                p.Payload = payload;

                payload = JsonConvert.SerializeObject(p);
            }

            using (var client = WebClientBuilder.Build(machine))
            {
                client.Headers[HttpRequestHeader.ContentType] = "application/json";
                client.UploadString(postUrl, payload);
            }

            if (isDeletable)
            {
                File.Delete(fileName);
            }
            else
            {
                File.WriteAllText(fileName, string.Empty);
            }

            _log.Trace($"{DateTime.Now} - {fileName} posted to server successfully");
        }

        internal static void PostSurvey()
        {
            try
            {
                _log.Trace("posting survey");

                var posturl = Program.Configuration.Survey.PostUrl;

                if (!File.Exists(ApplicationDetails.InstanceFiles.SurveyResults))
                    return;

                var survey = JsonConvert.DeserializeObject<Ghosts.Domain.Messages.MesssagesForServer.Survey>(File.ReadAllText(ApplicationDetails.InstanceFiles.SurveyResults));

                var payload = JsonConvert.SerializeObject(survey);

                var machine = new ResultMachine();

                if (Program.Configuration.Survey.IsSecure)
                {
                    payload = Crypto.EncryptStringAes(payload, machine.Name);
                    payload = Crypto.Base64Encode(payload);

                    var p = new EncryptedPayload();
                    p.Payload = payload;

                    payload = JsonConvert.SerializeObject(p);
                }

                using (var client = WebClientBuilder.Build(machine))
                {
                    client.Headers[HttpRequestHeader.ContentType] = "application/json";
                    client.UploadString(posturl, payload);
                }

                _log.Trace($"{DateTime.Now} - survey posted to server successfully");

                File.Delete(ApplicationDetails.InstanceFiles.SurveyResults);
            }
            catch (Exception e)
            {
                _log.Trace("Problem posting logs to server");
                _log.Error(e);
            }
        }
    }
}
