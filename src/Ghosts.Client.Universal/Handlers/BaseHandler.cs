// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Newtonsoft.Json;
using NLog;

namespace Ghosts.Client.Universal.Handlers;

public interface IHandler
{
    Task Run();
}

public abstract class BaseHandler : IHandler
{
    private static readonly Logger _timelineLog = LogManager.GetLogger("TIMELINE");
    internal static readonly Logger _log = LogManager.GetCurrentClassLogger();
    internal static readonly Random _random = new();

    public int JitterFactor = 0;
    protected readonly TimelineHandler Handler;
    protected readonly Timeline Timeline;
    protected readonly CancellationToken Token;
    protected string Result { get; set; }
    protected string Command { get; set; }
    protected int ExecutionProbability = 100;
    /// <summary>
    /// Used with Jitter.JitterFactorDelay
    /// </summary>

    public string Arg { get; set; }
    public string TrackableId { get; set; }

    protected BaseHandler(Timeline timeline, TimelineHandler handler, CancellationToken token)
    {
        this.Handler = handler;
        this.Timeline = timeline;
        this.Token = token;

        WorkingHours.Is(handler);
    }

    public async Task Run()
    {
        try
        {
            if (this.Handler.Loop)
            {
                while (!this.Token.IsCancellationRequested)
                {
                    await RunOnce();
                }
            }
            else
            {
                await RunOnce();
            }
        }
        catch (OperationCanceledException)
        {
            _log.Trace($"{GetType().Name} handler cancelled.");
        }
        catch (Exception e)
        {
            _log.Error(e);
        }
    }

    protected abstract Task RunOnce();

    public static void Report(ReportItem reportItem)
    {
        var result = new TimeLineRecord
        {
            Handler = reportItem.Handler,
            Command = reportItem.Command,
            CommandArg = reportItem.Arg,
            Result = reportItem.Result,
            TrackableId = reportItem.Trackable
        };

        var o = JsonConvert.SerializeObject(result,
            Formatting.None,
            new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });

        _timelineLog.Info($"TIMELINE|{DateTime.UtcNow:MM/dd/yyyy HH:mm:ss.fff}Z|{o}");
    }
}
