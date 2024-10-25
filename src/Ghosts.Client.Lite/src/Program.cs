// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Drawing;
using System.Net;
using System.Reflection;
using Ghosts.Client.Lite.Infrastructure.Comms;
using Ghosts.Client.Lite.Infrastructure.Comms.ClientSocket;
using Ghosts.Client.Lite.Infrastructure.Handlers;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Newtonsoft.Json;
using NLog;
using Quartz;
using Quartz.Impl;

namespace Ghosts.Client.Lite;

internal static class Program
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    internal static ClientConfiguration Configuration { get; private set; }
    internal static ApplicationDetails.ConfigurationUrls ConfigurationUrls { get; set; }
    internal static BackgroundTaskQueue Queue;
    public static CheckId CheckId { get; set; }

    public static async Task Main(string[] args)
    {
        try
        {
            await Run(args);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Fatal exception in {ApplicationDetails.Name} {ApplicationDetails.Version}: {e}",
                Color.Red);
            _log.Fatal(e);
            Console.ReadLine();
        }
    }

    private static async Task Run(string[] args)
    {
        var rand = new Random();

        // Ignore all certs
        ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;

        // Load configuration
        try
        {
            Configuration = ClientConfigurationLoader.Config;
            ConfigurationUrls = new ApplicationDetails.ConfigurationUrls(Configuration.ApiRootUrl);
        }
        catch (Exception e)
        {
            var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var o =
                $"Exec path: {path} - configuration 404: {ApplicationDetails.ConfigurationFiles.Application} - exiting. Exception: {e}";
            _log.Fatal(o);
            Console.WriteLine(o, Color.Red);
            Console.ReadLine();
            return;
        }

        _log.Info(ApplicationDetails.Header);
        _log.Info(
            $"Initiating {ApplicationDetails.Name} startup - Local: {DateTime.Now.TimeOfDay} UTC: {DateTime.UtcNow.TimeOfDay}");

        // Get default timeline from config/timeline.json
        var timeline = TimelineBuilder.GetTimeline();

        // Does this instance of the application have an Id?
        CheckId = new CheckId();
        _log.Trace($"CheckID: {CheckId.Id}");

        // Connect to ghosts API via websockets on a separate thread
        _log.Trace("Sockets enabled. Connecting...");
        var c = new Connection(Configuration.Sockets);
        Queue = c.Queue;

        // Start the connection in a new task
        _ = Task.Run(async () => await c.Run());

        // Connect to command server over "old style polling" as the fallback for updates and sending logs
        // This is a separate thread also
        _ = Task.Run(() => Updates.Run());

        // Fall into executing activity
        var schedulerFactory = new StdSchedulerFactory();
        var scheduler = await schedulerFactory.GetScheduler();
        await scheduler.Start();

        // Schedule timeline
        await ScheduleTimeline(scheduler, timeline);

        // Schedule memory cleanup job
        await ScheduleMemoryCleanup(scheduler);

        await Task.Delay(-1); // run forever
    }

    private static async Task ScheduleTimeline(IScheduler scheduler, Timeline timeline)
    {
        var rand = new Random();
        foreach (var handler in timeline.TimeLineHandlers)
        {
            switch (handler.HandlerType)
            {
                case HandlerType.BrowserChrome:
                case HandlerType.BrowserFirefox:
                    var job = JobBuilder.Create<WebBrowsingJob>()
                        .UsingJobData("handler", JsonConvert.SerializeObject(handler))
                        .Build();
                    foreach (var timelineEvent in handler.TimeLineEvents)
                    {
                        // Trigger the job to run after a random short delay
                        var trigger = TriggerBuilder.Create()
                            .StartNow() // Start immediately
                            .WithSimpleSchedule(x => x
                                .WithIntervalInSeconds(GetJitteredDelay(timelineEvent.DelayAfterActual, rand))
                                .RepeatForever())
                            .Build();
                        // Schedule the job with the trigger
                        await scheduler.ScheduleJob(job, trigger);
                    }
                    break;

                case HandlerType.Excel:
                case HandlerType.PowerPoint:
                case HandlerType.Word:
                    job = JobBuilder.Create<FileCreatorJob>()
                        .UsingJobData("handler", JsonConvert.SerializeObject(handler))
                        .Build();
                    foreach (var timelineEvent in handler.TimeLineEvents)
                    {
                        // Trigger the job to run after a random short delay
                        var trigger = TriggerBuilder.Create()
                            .StartNow() // Start immediately
                            .WithSimpleSchedule(x => x
                                .WithIntervalInSeconds(GetJitteredDelay(timelineEvent.DelayAfterActual, rand))
                                .RepeatForever())
                            .Build();
                        // Schedule the job with the trigger
                        await scheduler.ScheduleJob(job, trigger);
                    }
                    break;
            }
        }
    }

    private static async Task ScheduleMemoryCleanup(IScheduler scheduler)
    {
        var job = JobBuilder.Create<MemoryCleanupJob>()
            .Build();

        // Schedule the job to run every hour
        var trigger = TriggerBuilder.Create()
            .StartNow()
            .WithSimpleSchedule(x => x
                .WithIntervalInHours(1)
                .RepeatForever())
            .Build();

        await scheduler.ScheduleJob(job, trigger);
    }

    private static int GetJitteredDelay(int baseDelay, Random rand)
    {
        var jitterFactor = 1 + (rand.NextDouble() * 0.2 - 0.1); // Random value between -0.1 and +0.1
        return (int)(baseDelay * jitterFactor / 10); // seconds to milliseconds, since ghosts timelines are in ms
    }
}
