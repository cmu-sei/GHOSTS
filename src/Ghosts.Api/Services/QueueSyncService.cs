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
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Api.Models;
using ghosts.api.ViewModels;
using Ghosts.Domain.Messages.MesssagesForServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;

namespace Ghosts.Api.Services
{
    public class QueueSyncService : IHostedService
    {
        private static readonly Logger log = LogManager.GetCurrentClassLogger();
        private readonly IServiceScopeFactory _scopeFactory;

        public QueueSyncService(IServiceScopeFactory scopeFactory, IBackgroundQueue queue)
        {
            _scopeFactory = scopeFactory;
            Queue = queue;
        }

        private IBackgroundQueue Queue { get; }

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
                log.Trace("Beginning sync loop...");

                try
                {
                    await Sync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }

                log.Trace("Ending sync loop");

                await Task.Delay(new TimeSpan(0, 0, Program.ClientConfig.QueueSyncDelayInSeconds));
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
                        await ProcessMachine(scope, context, (MachineQueueEntry) item.Payload);
                        break;
                    case QueueEntry.Types.Notification:
                        await ProcessNotification(context, (NotificationQueueEntry) item.Payload);
                        break;
                    case QueueEntry.Types.Survey:
                        await ProcessSurvey(context, (Survey) item.Payload);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
        }

        private async Task ProcessSurvey(ApplicationDbContext context, Survey item)
        {
            try
            {
                var survey = item;
                await context.Surveys.AddAsync(survey);
                context.Entry(survey).State = EntityState.Added;
                await context.SaveChangesAsync();
                await Queue.DequeueAsync(new CancellationToken());
            }
            catch (Exception e)
            {
                log.Trace($"Error in item {e} - {item}");
            }
        }

        private async Task ProcessNotification(ApplicationDbContext context, NotificationQueueEntry item)
        {
            var webhooks = context.Webhooks.Where(o => o.Status == StatusType.Active);

            try
            {
                log.Trace($"Attempting find for {item}");

                foreach (var webhook in webhooks)
                {
                    var t = new Thread(() => { HandleWebhook(webhook, item); }) {IsBackground = true};
                    t.Start();
                }

                //if no webhooks setup, the queue simply gets flushed
                await Queue.DequeueAsync(new CancellationToken());
            }
            catch (Exception e)
            {
                log.Trace($"Error in item {e} - {item}");
            }
        }

        internal static async void HandleWebhook(Webhook webhook, NotificationQueueEntry payload)
        {
            var historyTimeline = JsonConvert.DeserializeObject<HistoryTimeline>(payload.Payload.ToString());
            // Serialize our concrete class into a JSON String

            var formattedResponse = webhook.PostbackFormat;

            var isValid = false;
            var reg = new Regex(@"\[(.*?)\]");
            foreach (Match match in reg.Matches(formattedResponse))
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
                log.Trace("Webhook has no payload, exiting");
                return;
            }

            try
            {
                // Wrap our JSON inside a StringContent which then can be used by the HttpClient class
                var httpContent = new StringContent(formattedResponse, Encoding.UTF8, "application/json");

                using var httpClient = new HttpClient();
                // Do the actual request and await the response
                var httpResponse = await httpClient.PostAsync(webhook.PostbackUrl, httpContent);

                log.Trace($"Webhook response {webhook.PostbackUrl} {webhook.PostbackMethod} {httpResponse.StatusCode}");

                // If the response contains content we want to read it!
                if (httpResponse.Content != null)
                {
                    var responseContent = await httpResponse.Content.ReadAsStringAsync();
                    log.Trace($"Webhook notification sent with {responseContent}");
                    // From here on you could deserialize the ResponseContent back again to a concrete C# type using Json.Net
                }
            }
            catch (Exception e)
            {
                log.Trace($"Webhook failed response {webhook.PostbackUrl} {webhook.PostbackMethod} - {e}");
            }
        }

        private async Task ProcessMachine(IServiceScope scope, ApplicationDbContext context, MachineQueueEntry item)
        {
            var service = scope.ServiceProvider.GetRequiredService<IMachineService>();

            log.Trace("Scope and context created");

            var machines = new List<Machine>();
            var histories = new List<Machine.MachineHistoryItem>();
            var timelines = new List<HistoryTimeline>();
            var health = new List<HistoryHealth>();
            var trackables = new List<HistoryTrackable>();

            //clients can send up a "create webhook" payload
            var webhooks = new List<Webhook>();

            log.Trace("Beginning item processing...");

            log.Trace($"Attempting find for {item.Machine.Id}");
            Machine machine = null;

            if (item.Machine.Id != Guid.Empty)
                machine = context.Machines.FirstOrDefault(o => o.Id == item.Machine.Id);

            if (machine == null)
            {
                log.Trace("Machine not found by id");
                if (!string.IsNullOrEmpty(item.Machine.Name))
                {
                    log.Trace($"Searching for machine by name {item.Machine.Name}");
                    machine = context.Machines.FirstOrDefault(o => o.Name == item.Machine.Name);
                }

                if (machine == null)
                {
                    log.Trace("Machine is still null, so attempting another create");
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

            log.Trace($"Proc history type: {item.HistoryType}");

            if (item.HistoryType == Machine.MachineHistoryItem.HistoryType.PostedResults)
            {
                if (item.LogDump.Log.Length > 0)
                    log.Trace(item.LogDump.Log);

                var lines = item.LogDump.Log.Split(new[] {Environment.NewLine}, StringSplitOptions.None);
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

                        log.Trace($"Processing {type} with {data}");

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
                                                    Payload = (JObject) JToken.FromObject(timeline)
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
                                        log.Info("processing webhookcreate...");
                                        var hook = JsonConvert.DeserializeObject<WebhookViewModel>(data.ToString());
                                        var h = new Webhook(hook);
                                        webhooks.Add(h);
                                    }
                                    catch (Exception e)
                                    {
                                        log.Info($"serializing hook failed: {data} : {e})");
                                    }

                                    break;
                            }
                    }
                    catch (Exception e)
                    {
                        log.Trace($"Bad line: {e} - {line}");
                    }
            } //endif posted results

            var _ = Queue.DequeueAsync(new CancellationToken()).Result;

            if (machines.Count > 0)
            {
                context.Machines.UpdateRange(machines);
                try
                {
                    var i = await context.SaveChangesAsync();
                    if (i > 0)
                        log.Info(
                            $"Queue: {i} (machines: {machines.Count()}");
                }
                catch (Exception e)
                {
                    log.Error(e);
                }
            }

            if (trackables.Count > 0)
            {
                await context.HistoryTrackables.AddRangeAsync(trackables);
                try
                {
                    var i = await context.SaveChangesAsync();
                    if (i > 0)
                        log.Info(
                            $"Queue: {i} (Trackables: {trackables.Count()})");
                }
                catch (Exception e)
                {
                    log.Error(e);
                }
            }

            if (health.Count > 0)
            {
                await context.HistoryHealth.AddRangeAsync(health);
                try
                {
                    var i = await context.SaveChangesAsync();
                    if (i > 0)
                        log.Info(
                            $"Queue: {i} (Health: {health.Count()})");
                }
                catch (Exception e)
                {
                    log.Error(e);
                }
            }

            if (timelines.Count > 0)
            {
                await context.HistoryTimeline.AddRangeAsync(timelines);
                try
                {
                    var i = await context.SaveChangesAsync();
                    if (i > 0)
                        log.Info(
                            $"Queue: {i} (Timeline: {timelines.Count()})");
                }
                catch (Exception e)
                {
                    log.Error(e);
                }
            }

            if (histories.Count > 0)
            {
                await context.HistoryMachine.AddRangeAsync(histories);
                try
                {
                    var i = await context.SaveChangesAsync();
                    if (i > 0)
                        log.Info(
                            $"Queue: {i} (History: {histories.Count()}");
                }
                catch (Exception e)
                {
                    log.Error(e);
                }
            }

            if (webhooks.Count > 0)
            {
                await context.Webhooks.AddRangeAsync(webhooks);
                try
                {
                    var i = await context.SaveChangesAsync();
                    if (i > 0)
                        log.Info(
                            $"Queue: {i} (Webhooks: {webhooks.Count()}");
                }
                catch (Exception e)
                {
                    log.Error(e);
                }
            }
        }
    }
}