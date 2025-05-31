// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Client.Universal.Infrastructure;
using Ghosts.Client.Universal.TimelineManager;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Ghosts.Domain.Messages.MesssagesForServer;
using Newtonsoft.Json;
using NLog;

namespace Ghosts.Client.Universal.Comms
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
            Task.Run(GetServerUpdates);
            Task.Run(PostClientResults);
        }

        private static async Task GetServerUpdates()
        {
            if (!Program.Configuration.ClientUpdates.IsEnabled)
                return;

            var machine = new ResultMachine();
            while (true)
            {
                try
                {
                    var s = string.Empty;
                    using (var client = HttpClientBuilder.Build(machine))
                    {
                        try
                        {
                            await using var stream = await client.GetStreamAsync(Program.ConfigurationUrls.Updates);
                            using var reader = new StreamReader(stream);
                            s = await reader.ReadToEndAsync();
                        }
                        catch (WebException wex)
                        {
                            if (wex?.Response == null)
                            {
                                _log.Debug($"{DateTime.Now} - API Server appears to be not responding");
                            }
                            else if (((HttpWebResponse)wex.Response).StatusCode == HttpStatusCode.NotFound)
                            {
                                _log.Debug($"{DateTime.Now} - No new configuration found");
                            }
                        }
                        catch (Exception e)
                        {
                            _log.Error($"Exception in connecting to server: {e.Message}");
                        }
                    }

                    if (!string.IsNullOrEmpty(s))
                    {
                        var update = JsonConvert.DeserializeObject<UpdateClientConfig>(s);

                        switch (update.Type)
                        {
                            case UpdateClientConfig.UpdateType.RequestForTimeline:
                                await PostCurrentTimeline(update);
                                break;
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
                                        orchestrator.RunCommand(timeline, timelineHandler);
                                    }
                                }
                                catch (Exception exc)
                                {
                                    _log.Debug(exc);
                                }

                                break;
                            case UpdateClientConfig.UpdateType.Health:
                                {
                                    var newTimeline = JsonConvert.DeserializeObject<ResultHealth>(update.Update.ToString());
                                    //save to local disk
                                    await using var file = File.CreateText(ApplicationDetails.ConfigurationFiles.Health);
                                    var serializer = new JsonSerializer { Formatting = Formatting.Indented };
                                    serializer.Serialize(file, newTimeline);

                                    break;
                                }
                            default:
                                _log.Debug($"Update {update.Type} has no handler, ignoring...");
                                break;
                        }
                    }
                }
                catch (Exception e)
                {
                    _log.Debug("Problem polling for new configuration");
                    _log.Error(e);
                }

                Thread.Sleep(Jitter.Basic(Program.Configuration.ClientUpdates.CycleSleep));
            }
        }

        private static async Task PostCurrentTimeline(UpdateClientConfig update)
        {
            // is the config for a specific timeline id?
            var timelineId = TimelineUpdateClientConfigManager.GetConfigUpdateTimelineId(update);


            // get all timelines
            var localTimelines = Ghosts.Domain.Code.TimelineManager.GetLocalTimelines();

            var timelines = localTimelines as Timeline[] ?? localTimelines.ToArray();
            if (timelineId != Guid.Empty)
            {
                foreach (var timeline in timelines)
                {
                    if (timeline.Id == timelineId)
                    {
                        timelines = new List<Timeline>()
                        {
                            timeline
                        }.ToArray();
                        break;
                    }
                }
            }

            string postUrl;

            try
            {
                postUrl = Program.ConfigurationUrls.Timeline;
            }
            catch
            {
                _log.Error("Can't get timeline posturl!");
                return;
            }

            foreach (var timeline in timelines)
            {
                try
                {
                    _log.Trace("posting timeline");

                    var payload = TimelineBuilder.TimelineToJsonPayload(timeline);
                    var machine = new ResultMachine();

                    using (var client = HttpClientBuilder.Build(machine))
                    {
                        var content = new StringContent(JsonConvert.SerializeObject(payload));
                        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

                        var response = await client.PostAsync(postUrl, content);
                        response.EnsureSuccessStatusCode(); // throws if not 2xx
                    }

                    _log.Trace($"{DateTime.Now} - timeline posted to server successfully");
                }
                catch (Exception e)
                {
                    _log.Debug(
                        $"Problem posting timeline to server from {ApplicationDetails.ConfigurationFiles.Timeline} to {postUrl}");
                    _log.Error(e);
                }
            }
        }

        private static async Task PostClientResults()
        {
            if (!Program.Configuration.ClientResults.IsEnabled)
                return;

            var fileName = ApplicationDetails.LogFiles.ClientUpdates;
            var postUrl = Program.ConfigurationUrls.Results;

            var machine = new ResultMachine();

            Thread.Sleep(Jitter.Basic(Program.Configuration.ClientResults.CycleSleep));

            while (true)
            {
                try
                {
                    if (File.Exists(fileName))
                    {
                        await PostResults(fileName, machine, postUrl);
                        _log.Trace($"{fileName} posted successfully...");
                    }
                    else
                    {
                        _log.Trace($"{DateTime.Now} - {fileName} not found - sleeping...");
                    }
                }
                catch (Exception e)
                {
                    _log.Error($"Problem posting logs to server: {e.Message}");
                }
                finally
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }

                // look for other result files that have not been posted
                try
                {
                    var files = Directory.GetFiles(
                        Path.GetDirectoryName(fileName) ??
                        throw new InvalidOperationException("Path declaration failed"), "*.log");
                    foreach (var file in files)
                    {
                        if (!file.EndsWith("app.log") && file != fileName)
                        {
                            await PostResults(file, machine, postUrl);
                            _log.Trace($"{fileName} posted successfully...");
                        }
                    }
                }
                catch (Exception e)
                {
                    _log.Debug($"Problem posting overflow logs from {fileName} to server {postUrl} : {e.Message}");
                }
                finally
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }

                Thread.Sleep(Jitter.Basic(Program.Configuration.ClientResults.CycleSleep));
            }
        }

        private static async Task PostResults(string fileName, ResultMachine machine, string postUrl)
        {
            var tempFile = ($"{fileName.Replace("clientupdates.log", Guid.NewGuid().ToString())}.log");
            if (fileName.EndsWith("clientupdates_not_posted.log"))
            {
                tempFile = ($"{fileName.Replace("clientupdates_not_posted.log", Guid.NewGuid().ToString())}.log");
            }

            var isCopied = false;
            var i = 0;
            while (!isCopied)
            {
                try
                {
                    File.Move(fileName, tempFile);
                    isCopied = true;
                    break;
                }
                catch
                {
                    if (i > 50)
                    {
                        throw;
                    }

                    Thread.Sleep(1000);
                }

                i++;
            }

            Thread.Sleep(2000);

            string rawLogContents = null;

            try
            {
                await using (var s = new FileStream(tempFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (var tr = new StreamReader(s))
                    {
                        rawLogContents = await tr.ReadToEndAsync();
                    }
                }

                var r = new TransferLogDump { Log = rawLogContents };
                var payload = JsonConvert.SerializeObject(r);
                if (Program.Configuration.ClientResults.IsSecure)
                {
                    payload = Crypto.EncryptStringAes(payload, machine.Name);
                    payload = Base64Encoder.Base64Encode(payload);

                    var p = new EncryptedPayload { Payload = payload };

                    payload = JsonConvert.SerializeObject(p);
                }

                using (var client = HttpClientBuilder.Build(machine))
                {
                    var content = new StringContent(payload);
                    content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

                    var response = await client.PostAsync(postUrl, content);
                    response.EnsureSuccessStatusCode();
                }

            }
            catch (Exception e)
            {
                _log.Trace($"Client results report failed: {e}");
                if (!string.IsNullOrEmpty(rawLogContents))
                {
                    try
                    {
                        //put the temp file contents back
                        var backupFile =
                            ApplicationDetails.LogFiles.ClientUpdates.Replace("clientupdates.log",
                                "clientupdates_not_posted.log");
                        await File.AppendAllTextAsync(backupFile, rawLogContents);
                        File.Delete(tempFile);
                    }
                    catch
                    {
                        _log.Trace($"Log post failure cleanup also failed: {e}");
                    }
                }

                throw;
            }

            //delete the temp file we used for reading
            File.Delete(tempFile);
            _log.Trace($"{DateTime.Now} - {fileName} posted to server successfully");
        }

        internal static async Task PostSurvey()
        {
            string postUrl;

            try
            {
                postUrl = Program.ConfigurationUrls.Survey;
            }
            catch
            {
                _log.Error("Can't get survey posturl!");
                return;
            }

            try
            {
                _log.Trace("posting survey");

                Thread.Sleep(Jitter.Basic(100));

                if (!File.Exists(ApplicationDetails.InstanceFiles.SurveyResults))
                    return;

                var survey =
                    JsonConvert.DeserializeObject<Ghosts.Domain.Messages.MesssagesForServer.Survey>(
                        await File.ReadAllTextAsync(ApplicationDetails.InstanceFiles.SurveyResults));

                var payload = JsonConvert.SerializeObject(survey);

                var machine = new ResultMachine();

                if (Program.Configuration.Survey.IsSecure)
                {
                    payload = Crypto.EncryptStringAes(payload, machine.Name);
                    payload = Base64Encoder.Base64Encode(payload);

                    var p = new EncryptedPayload { Payload = payload };

                    payload = JsonConvert.SerializeObject(p);
                }

                using (var client = HttpClientBuilder.Build(machine))
                {
                    using var content = new StringContent(payload);
                    content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

                    var response = await client.PostAsync(postUrl, content);
                    response.EnsureSuccessStatusCode(); // Optional: throws if not 2xx
                }

                _log.Trace($"{DateTime.Now} - survey posted to server successfully");

                File.Delete(ApplicationDetails.InstanceFiles.SurveyResults);
            }
            catch (Exception e)
            {
                _log.Debug(
                    $"Problem posting logs to server from {ApplicationDetails.InstanceFiles.SurveyResults} to {Program.ConfigurationUrls.Survey}");
                _log.Error(e);
            }
        }
    }
}
