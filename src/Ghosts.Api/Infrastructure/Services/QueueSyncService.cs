// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using ghosts.api.Infrastructure.Models;
using ghosts.api.ViewModels;
using Ghosts.Api;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Domain.Messages.MesssagesForServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;

namespace ghosts.api.Infrastructure.Services
{
    public partial class QueueSyncService(IServiceScopeFactory scopeFactory, IBackgroundQueue queue) : IHostedService
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

        private IBackgroundQueue Queue { get; } = queue;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Run();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private async void Run()
        {
            while (true)
            {
                try
                {
                    await Sync();
                }
                catch (Exception ex)
                {
                    _log.Error(ex);
                }

                await Task.Delay(new TimeSpan(0, 0, Program.ApplicationSettings.QueueSyncDelayInSeconds));
            }
        }

        private async Task Sync()
        {
            using var scope = _scopeFactory.CreateScope();
            await using var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            foreach (var item in Queue.GetAll())
                switch (item.Type)
                {
                    case QueueEntry.Types.Machine:
                        await ProcessMachine(scope, context, (MachineQueueEntry)item.Payload);
                        break;
                    case QueueEntry.Types.Notification:
                        await ProcessNotification(context, (NotificationQueueEntry)item.Payload);
                        break;
                    case QueueEntry.Types.Survey:
                        await ProcessSurvey(context, (Survey)item.Payload);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
        }

        private async Task ProcessSurvey(ApplicationDbContext context, Survey item)
        {
            try
            {
                context.Surveys.Add(item);
                context.Entry(item).State = EntityState.Added;
                await context.SaveChangesAsync();
                await Queue.DequeueAsync(new CancellationToken());
            }
            catch (Exception e)
            {
                _log.Trace($"Error in item {e} - {item}");
            }
        }

        private async Task ProcessNotification(ApplicationDbContext context, NotificationQueueEntry item)
        {
            var webhooks = context.Webhooks.Where(o => o.Status == StatusType.Active);

            try
            {
                foreach (var webhook in webhooks)
                {
                    var t = new Thread(() => { HandleWebhook(webhook, item); }) { IsBackground = true };
                    t.Start();
                }

                //if no webhooks setup, the queue simply gets flushed
                await Queue.DequeueAsync(new CancellationToken());
            }
            catch (Exception e)
            {
                _log.Trace($"Error in item {e} - {item}");
            }
        }

        private async Task ProcessMachine(IServiceScope scope, ApplicationDbContext context, MachineQueueEntry item)
        {
            var service = scope.ServiceProvider.GetRequiredService<IMachineService>();

            var machines = new List<Machine>();
            var histories = new List<Machine.MachineHistoryItem>();
            var timelines = new List<HistoryTimeline>();
            var health = new List<HistoryHealth>();
            var trackables = new List<HistoryTrackable>();

            //clients can send up a "create webhook" payload
            var webhooks = new List<Webhook>();
            Machine machine = null;

            if (item.Machine.Id != Guid.Empty)
                machine = context.Machines.FirstOrDefault(o => o.Id == item.Machine.Id);

            if (machine == null)
            {
                if (!string.IsNullOrEmpty(item.Machine.Name))
                {
                    machine = context.Machines.FirstOrDefault(o => o.Name == item.Machine.Name);
                }

                if (machine == null)
                {
                    if (item.Machine.Id == Guid.Empty)
                        item.Machine.Id = Guid.NewGuid();
                    item.Machine.LastReportedUtc = DateTime.UtcNow;
                    item.Machine.StatusUp = Machine.UpDownStatus.Up;
                    item.Machine.History.Add(new Machine.MachineHistoryItem
                    {
                        Type = Machine.MachineHistoryItem.HistoryType.Created
                    });
                    await service.CreateAsync(item.Machine, new CancellationToken());
                    machine = item.Machine;
                }
            }

            machine.LastReportedUtc = DateTime.UtcNow;
            machine.StatusUp = Machine.UpDownStatus.Up;
            machines.Add(machine);

            histories.Add(new Machine.MachineHistoryItem
            {
                MachineId = machine.Id,
                Type = item.HistoryType,
                CreatedUtc = DateTime.UtcNow
            });

            if (item.HistoryType == Machine.MachineHistoryItem.HistoryType.PostedResults)
            {
                if (item.LogDump.Log.Length > 0)
                    _log.Trace(item.LogDump.Log);

                var lines = item.LogDump.Log.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                foreach (var line in lines)
                    try
                    {
                        var array = line.Split(Convert.ToChar("|"));

                        var isReady = false;
                        var time = DateTime.UtcNow;
                        string type = null;
                        dynamic data = null;

                        var arrayPartCount = array.GetUpperBound(0);

                        //new with time
                        if (arrayPartCount >= 2)
                        {
                            isReady = true;
                            type = array[0];
                            time = Convert.ToDateTime(array[1]);
                            data = JsonConvert.DeserializeObject(array[2]);
                        }

                        //old no time [Obsolete]
                        else if (arrayPartCount == 1)
                        {
                            isReady = true;
                            type = array[0];
                            data = JsonConvert.DeserializeObject(array[1]);
                        }

                        // log.Trace($"Processing {type} with {data}");

                        if (isReady)
                            switch (type)
                            {
                                case "TIMELINE":
                                    var timeline = new HistoryTimeline
                                    {
                                        Command = data.Command,
                                        MachineId = machine.Id,
                                        Handler = data.Handler,
                                        CommandArg = data.CommandArg,
                                        CreatedUtc = time
                                    };

                                    if (data.Tags != null)
                                        timeline.Tags = data.Tags;

                                    if (data.Result != null)
                                        timeline.Result = data.Result;

                                    timelines.Add(timeline);

                                    //add notification
                                    Queue.Enqueue(
                                        new QueueEntry
                                        {
                                            Type = QueueEntry.Types.Notification,
                                            Payload =
                                                new NotificationQueueEntry
                                                {
                                                    Type = NotificationQueueEntry.NotificationType.Timeline,
                                                    Payload = (JObject)JToken.FromObject(timeline)
                                                }
                                        });

                                    if (data["TrackableId"] != null)
                                    {
                                        var trackable = new HistoryTrackable
                                        {
                                            TrackableId = data["TrackableId"],
                                            Command = data.Command,
                                            MachineId = machine.Id,
                                            Handler = data.Handler,
                                            CommandArg = data.CommandArg,
                                            CreatedUtc = time
                                        };

                                        if (data.Result != null)
                                            trackable.Result = data.Result;

                                        trackables.Add(trackable);
                                    }

                                    break;
                                case "HEALTH":
                                    var users = string.Join(",", data.LoggedOnUsers);
                                    var errors = string.Join(",", data.Errors);
                                    health.Add(new HistoryHealth
                                    {
                                        Permissions = data.Permssions,
                                        MachineId = machine.Id,
                                        LoggedOnUsers = users,
                                        Internet = data.Internet,
                                        ExecutionTime = data.ExecutionTime,
                                        Errors = errors,
                                        Stats = data.Stats.ToString(),
                                        CreatedUtc = time
                                    });
                                    break;
                                case "WEBHOOKCREATE":
                                    try
                                    {
                                        var hook = JsonConvert.DeserializeObject<WebhookViewModel>(data.ToString());
                                        var h = new Webhook(hook);
                                        webhooks.Add(h);
                                    }
                                    catch (Exception e)
                                    {
                                        _log.Info($"serializing hook failed: {data} : {e})");
                                    }

                                    break;
                            }
                    }
                    catch (Exception e)
                    {
                        _log.Trace($"Bad line: {e} - {line}");
                    }
            } //endif posted results

            var _ = Queue.DequeueAsync(new CancellationToken()).Result;

            if (machines.Count > 0)
            {
                context.Machines.UpdateRange(machines);
                try
                {
                    await context.SaveChangesAsync();
                }
                catch (Exception e)
                {
                    _log.Error(e);
                }
            }

            if (trackables.Count > 0)
            {
                await context.HistoryTrackables.AddRangeAsync(trackables);
                try
                {
                    await context.SaveChangesAsync();
                }
                catch (Exception e)
                {
                    _log.Error(e);
                }
            }

            if (health.Count > 0)
            {
                await context.HistoryHealth.AddRangeAsync(health);
                try
                {
                    await context.SaveChangesAsync();
                }
                catch (Exception e)
                {
                    _log.Error(e);
                }
            }

            if (timelines.Count > 0)
            {
                await context.HistoryTimeline.AddRangeAsync(timelines);
                try
                {
                    await context.SaveChangesAsync();
                }
                catch (Exception e)
                {
                    _log.Error(e);
                }
            }

            if (histories.Count > 0)
            {
                await context.HistoryMachine.AddRangeAsync(histories);
                try
                {
                    await context.SaveChangesAsync();
                }
                catch (Exception e)
                {
                    _log.Error(e);
                }
            }

            if (webhooks.Count > 0)
            {
                await context.Webhooks.AddRangeAsync(webhooks);
                try
                {
                    await context.SaveChangesAsync();
                }
                catch (Exception e)
                {
                    _log.Error(e);
                }
            }

            _log.Trace($"Queue Processed — Machines: {machines.Count}, Histories: {histories.Count}, Timeline: {timelines.Count}, Health: {health.Count}, Trackables: {trackables.Count}, Webhooks: {webhooks.Count}");
        }

        internal static async void HandleWebhook(Webhook webhook, NotificationQueueEntry payload)
        {
            if (webhook == null || payload == null) return;

            string formattedResponse;
            if (payload.Type == NotificationQueueEntry.NotificationType.TimelineDelivered)
            {
                formattedResponse = payload.Payload.ToString();
            }
            else
            {
                var historyTimeline = JsonConvert.DeserializeObject<HistoryTimeline>(payload.Payload.ToString());
                // Serialize our concrete class into a JSON String

                formattedResponse = webhook.PostbackFormat;
                if (string.IsNullOrEmpty(formattedResponse)) return;

                var isValid = false;
                var reg = MyRegex();
                foreach (Match match in reg.Matches(formattedResponse).Cast<Match>())
                    switch (match.Value.ToLower())
                    {
                        case "[machinename]":
                            formattedResponse = formattedResponse.Replace(match.Value, historyTimeline.MachineId.ToString());
                            break;
                        case "[datetime.utcnow]":
                            //json formatted date!
                            formattedResponse = formattedResponse.Replace(match.Value, historyTimeline.CreatedUtc.ToString("s"));
                            break;
                        case "[messagetype]":
                            formattedResponse = formattedResponse.Replace(match.Value, "Binary");
                            break;
                        case "[messagepayload]":
                            if (payload.Payload["Result"] != null && !string.IsNullOrEmpty(payload.Payload["Result"].ToString()))
                            {
                                var p = payload.Payload["Result"].ToString().Trim().Trim('"').Trim().Trim('"');

                                p = $"\"{HttpUtility.JavaScriptStringEncode(p)}\"";

                                formattedResponse = formattedResponse.Replace(match.Value, p);
                                isValid = true;
                            }

                            break;
                    }

                if (!isValid)
                {
                    _log.Trace("Webhook has no payload, exiting");
                    return;
                }
            }

            try
            {
                // Wrap our JSON inside a StringContent which then can be used by the HttpClient class
                var httpContent = new StringContent(formattedResponse, Encoding.UTF8, "application/json");

                using var httpClient = new HttpClient();
                // Do the actual request and await the response
                var httpResponse = webhook.PostbackMethod switch
                {
                    Webhook.WebhookMethod.POST => await httpClient.PostAsync(webhook.PostbackUrl, httpContent),
                    Webhook.WebhookMethod.GET => await httpClient.GetAsync($"{webhook.PostbackUrl}?message={formattedResponse}"),
                    _ => throw new ArgumentException("webhook configuration encountered unspecified postback method")
                };

                _log.Trace($"Webhook response {webhook.PostbackUrl} {webhook.PostbackMethod} {httpResponse.StatusCode}");

                // If the response contains content we want to read it!
                if (httpResponse.Content != null)
                {
                    var responseContent = await httpResponse.Content.ReadAsStringAsync();
                    _log.Trace($"Webhook notification sent with {responseContent}");
                    // From here on you could deserialize the ResponseContent back again to a concrete C# type using Json.Net
                }
            }
            catch (Exception e)
            {
                _log.Trace($"Webhook failed response {webhook.PostbackUrl} {webhook.PostbackMethod} - {e}");
            }
        }

        [GeneratedRegex(@"\[(.*?)\]")]
        private static partial Regex MyRegex();
    }
}
