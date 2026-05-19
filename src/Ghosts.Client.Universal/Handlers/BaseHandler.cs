// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Newtonsoft.Json;
using NLog;
using OpenQA.Selenium.DevTools.V135.SystemInfo;

namespace Ghosts.Client.Universal.Handlers;

public interface IHandler
{
    //Task Run();
    void Run();
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

    
    // Do not use await RunOnce() - not needed, as this is in a task already
    // Also, will prevents re-running in a loop
    public void Run()
    {
        _log.Info($"{GetType().Name} executing for timeline {this.Timeline.Id} (loop={this.Handler.Loop})");
        try
        {
            if (this.Handler.Loop)
            {
                while (!this.Token.IsCancellationRequested)
                {
                    RunOnce();
                }
            }
            else
            {
                RunOnce();
            }

            _log.Info($"{GetType().Name} finished successfully for timeline {this.Timeline.Id}");
        }
        catch (OperationCanceledException)
        {
            _log.Info($"{GetType().Name} handler cancelled for timeline {this.Timeline.Id}");
        }
        catch (Exception e)
        {
            _log.Error($"{GetType().Name} failed for timeline {this.Timeline.Id}: {e.Message}");
            _log.Debug(e);
            throw;
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

        _timelineLog.Info($"TIMELINE|{DateTime.UtcNow.ToString("MM/dd/yyyy HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture)}Z|{o}");
    }

    public bool CheckProbabilityVar(string name, int value)
    {
        if (!(value >= 0 && value <= 100))
        {
            _log.Trace($"Variable {name} with value {value} must be an int between 0 and 100, setting to 0");
            return false;
        }
        return true;
    }

    /// <summary>
    /// Select an action from a list of probabilities.
    /// It is assumed that the list of probabilities adds up to <= 100.
    /// Each probability is associated with a string name in action list
    /// If the probabilities add up to less than 100, then null can be returned
    /// which means that no action was chosen
    /// </summary>
    /// <param name="probabilityList"></param>
    /// <param name="actionList"></param>
    /// <returns></returns>
    public static string SelectActionFromProbabilities(int[] probabilityList, string[] actionList)
    {
        int choice = _random.Next(0, 101);
        int endRange;
        int startRange = 0;
        int index = 0;
        foreach (var probability in probabilityList)
        {
            if (probability > 0)
            {
                endRange = startRange + probability;
                if (choice >= startRange && choice <= endRange) return actionList[index];
                else startRange = endRange + 1;
            }
            index++;
        }

        return null;
    }
}
