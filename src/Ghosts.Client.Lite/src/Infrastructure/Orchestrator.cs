// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Client.Lite.Infrastructure.Handlers;
using Ghosts.Domain;
using NLog;

namespace Ghosts.Client.Lite.Infrastructure;

public class Orchestrator
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();

    public static async Task RunCommand(Timeline timeline, TimelineHandler handler)
    {
        await ThreadLaunch(timeline, handler);
    }

    private static async Task ThreadLaunch(Timeline timeline, TimelineHandler handler)
    {
        var http = new HttpHandler();
        // foreach thing in the timeline determine which handler you need 
        switch (handler.HandlerType)
        {
            case HandlerType.BrowserChrome:
            case HandlerType.BrowserFirefox:
                foreach (var timelineEvent in handler.TimeLineEvents)
                {
                    foreach (var site in timelineEvent.CommandArgs)
                    {
                        await http.Run(handler.HandlerType, timelineEvent, site.ToString());
                    }
                }
                break;
            case HandlerType.Excel:
            case HandlerType.PowerPoint:
            case HandlerType.Word:
                foreach (var timelineEvent in handler.TimeLineEvents)
                {
                    foreach (var file in timelineEvent.CommandArgs)
                    {
                        await FileHandler.Run(handler.HandlerType, timelineEvent);
                    }
                }

                break;
        }
    }
}
