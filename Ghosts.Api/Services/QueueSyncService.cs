// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

//TODO: this file is a mess!

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Code;
using Ghosts.Api.Data;
using Ghosts.Api.Models;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Ghosts.Domain.Messages.MesssagesForServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;

namespace Ghosts.Api.Services
{
    public class QueueEntry
    {
        public enum Types
        {
            Notification,
            Machine,
            Survey
        }

        public object Payload { get; set; }
        public Types Type { get; set; }
    }

    public interface IBackgroundQueue
    {
        void Enqueue(QueueEntry item);
        Task<QueueEntry> DequeueAsync(CancellationToken cancellationToken);
        ConcurrentQueue<QueueEntry> Getall();
    }

    public class BackgroundQueue : IBackgroundQueue
    {
        private ConcurrentQueue<QueueEntry> _items = new ConcurrentQueue<QueueEntry>();
        private SemaphoreSlim _semaphone = new SemaphoreSlim(0);

        public void Enqueue(QueueEntry item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            this._items.Enqueue(item);
            this._semaphone.Release();
        }

        public async Task<QueueEntry> DequeueAsync(CancellationToken cancellationToken)
        {
            await _semaphone.WaitAsync(cancellationToken);
            _items.TryDequeue(out var item);

            return item;
        }

        public ConcurrentQueue<QueueEntry> Getall()
        {
            return this._items;
        }
    }

    public class QueueSyncService : IHostedService
    {
        private IBackgroundQueue Queue { get; set; }
        private static Logger log = LogManager.GetCurrentClassLogger();
        private readonly IServiceScopeFactory _scopeFactory;

        public QueueSyncService(IServiceScopeFactory scopeFactory, IBackgroundQueue queue)
        {
            this._scopeFactory = scopeFactory;
            this.Queue = queue;
        }

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
            using (var scope = this._scopeFactory.CreateScope())
            {
                using (var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>())
                {
                    foreach (var item in this.Queue.Getall())
                    {
                        switch (item.Type)
                        {
                            case QueueEntry.Types.Machine:
                                ProcessMachine(scope, context, (MachineQueueEntry)item.Payload);
                                break;
                            case QueueEntry.Types.Notification:
                                ProcessNotification(scope, context, (NotificationQueueEntry)item.Payload);
                                break;
                            case QueueEntry.Types.Survey:
                                ProcessSurvey(scope, context, (Survey)item.Payload);
                                break;
                        }
                    }
                }
            }
        }

        public class MachineQueueEntry
        {
            public Machine Machine { get; set; }
            public TransferLogDump LogDump { get; set; }
            public Machine.MachineHistoryItem.HistoryType HistoryType { get; set; }
        }

        public class NotificationQueueEntry
        {
            public enum NotificationType
            {
                Timeline = 0
            }

            public NotificationType Type { get; set; }
            public JObject Payload { get; set; }
        }

        private void ProcessSurvey(IServiceScope scope, ApplicationDbContext context, Survey item)
        {
            try
            {
                var survey = item;
                context.Surveys.Add(survey);
                context.Entry(survey).State = EntityState.Added;
                context.SaveChanges();
                var x = this.Queue.DequeueAsync(new CancellationToken()).Result;
            }
            catch (Exception e)
            {
                log.Trace($"Error in item {e} - {item}");
            }
        }

        private void ProcessNotification(IServiceScope scope, ApplicationDbContext context, NotificationQueueEntry item)
        {
            var webhooks = context.Webhooks.Where(o => o.Status == StatusType.Active);

            try
            {
                log.Trace($"Attempting find for {item}");

                foreach (var webhook in webhooks)
                {
                    var t = new Thread(() =>
                    {
                        HandleWebhook(webhook, item);
                    });
                    t.IsBackground = true;
                    t.Start();
                }

                //if no webhooks setup, the queue simply gets flushed
                var x = this.Queue.DequeueAsync(new CancellationToken()).Result;
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

            var reg = new Regex(@"\[(.*?)\]");
            foreach (Match match in reg.Matches(formattedResponse))
            {
                
                switch (match.Value.ToLower())
                {
                    case "[machinename]":
                        formattedResponse = formattedResponse.Replace(match.Value, historyTimeline.MachineId.ToString());
                        break;
                    case "[datetime.utcnow]":
                        formattedResponse = formattedResponse.Replace(match.Value, historyTimeline.CreatedUtc.ToString(CultureInfo.InvariantCulture));
                        break;
                    case "[messagepayload]":
                        if(payload.Payload["Result"] != null && !string.IsNullOrEmpty(payload.Payload["Result"].ToString()))
                            formattedResponse = formattedResponse.Replace(match.Value, payload.Payload["Result"].ToString());
                        break;
                    case "[messagetype]":
                        formattedResponse = formattedResponse.Replace(match.Value, "Binary");
                        break;
                }

                Console.WriteLine(match.Value);
                //TODO replace
            }

            try
            {
                // Wrap our JSON inside a StringContent which then can be used by the HttpClient class
                var httpContent = new StringContent(formattedResponse, Encoding.UTF8, "application/json");
                
                using (var httpClient = new HttpClient())
                {
                    // Do the actual request and await the response
                    var httpResponse = await httpClient.PostAsync(webhook.PostbackUrl, httpContent);

                    // If the response contains content we want to read it!
                    if (httpResponse.Content != null)
                    {
                        var responseContent = await httpResponse.Content.ReadAsStringAsync();
                        // From here on you could deserialize the ResponseContent back again to a concrete C# type using Json.Net
                    }
                }
            }
            catch (Exception e)
            {
                log.Error(e);
            }
        }

        private void ProcessMachine(IServiceScope scope, ApplicationDbContext context, MachineQueueEntry item)
        {
            var service = scope.ServiceProvider.GetRequiredService<IMachineService>();

            log.Trace("Scope and context created");

            var machines = new List<Machine>();
            var histories = new List<Machine.MachineHistoryItem>();
            var timelines = new List<HistoryTimeline>();
            var health = new List<HistoryHealth>();
            var trackables = new List<HistoryTrackable>();

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
                    log.Trace($"Machine is still null, so attempting another create");
                    if (item.Machine.Id == Guid.Empty)
                        item.Machine.Id = Guid.NewGuid();
                    item.Machine.LastReportedUtc = DateTime.UtcNow;
                    item.Machine.StatusUp = Machine.UpDownStatus.Up;
                    item.Machine.History.Add(new Machine.MachineHistoryItem
                    {
                        Type = Machine.MachineHistoryItem.HistoryType.Created
                    });
                    var x = service.CreateAsync(item.Machine, new CancellationToken()).Result;
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

                var lines = item.LogDump.Log.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                foreach (var line in lines)
                {
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

                        if (isReady)
                        {
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

                                    if (data.Result != null)
                                        timeline.Result = data.Result;

                                    timelines.Add(timeline);

                                    //add notification
                                    this.Queue.Enqueue(
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
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        log.Trace($"Bad line: {e} - {line}");
                    }
                }
            } //endif posted results
            var x2 = this.Queue.DequeueAsync(new CancellationToken()).Result;

            if (machines.Count > 0)
            {
                context.Machines.UpdateRange(machines);
                try
                {
                    var i = context.SaveChanges();
                    if (i > 0)
                        log.Info(
                            $"Queue: {i} (machines: {machines.Count}");
                }
                catch (Exception e)
                {
                    log.Error(e);
                }
            }
            if (trackables.Count > 0)
            {
                context.HistoryTrackables.AddRange(trackables);
                try
                {
                    var i = context.SaveChanges();
                    if (i > 0)
                        log.Info(
                            $"Queue: {i} (Trackables: {trackables.Count})");
                }
                catch (Exception e)
                {
                    log.Error(e);
                }
            }
            if (health.Count > 0)
            {
                context.HistoryHealth.AddRange(health);
                try
                {
                    var i = context.SaveChanges();
                    if (i > 0)
                        log.Info(
                            $"Queue: {i} (Health: {health.Count})");
                }
                catch (Exception e)
                {
                    log.Error(e);
                }
            }
            if (timelines.Count > 0)
            {
                context.HistoryTimeline.AddRange(timelines);
                try
                {
                    var i = context.SaveChanges();
                    if (i > 0)
                        log.Info(
                            $"Queue: {i} (Timeline: {timelines.Count})");
                }
                catch (Exception e)
                {
                    log.Error(e);
                }
            }
            if (histories.Count > 0)
            {
                context.HistoryMachine.AddRange(histories);
                try
                {
                    var i = context.SaveChanges();
                    if (i > 0)
                        log.Info(
                            $"Queue: {i} (History: {histories.Count}");
                }
                catch (Exception e)
                {
                    log.Error(e);
                }
            }
        }
    }
}
