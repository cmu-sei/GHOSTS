// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using WatiN.Core;

namespace Ghosts.Client.Handlers
{
    [Obsolete("Unsupported going forward (as of v6)", false)]
    public class BrowserIE : BaseHandler
    {
        public IE Driver;

        public BrowserIE(TimelineHandler handler)
        {
            try
            {

                this.Driver = new IE(handler.Initial);

                if (handler.Loop)
                {
                    while (true)
                    {
                        ExecuteEvents(handler);
                    }
                }
                else
                {
                    ExecuteEvents(handler);
                }
            }
            catch (Exception e)
            {
                Log.Debug(e);
            }
        }

        private void ExecuteEvents(TimelineHandler handler)
        {
            try
            {
                foreach (var timelineEvent in handler.TimeLineEvents)
                {
                    WorkingHours.Is(handler);

                    if (timelineEvent.DelayBefore > 0)
                        Thread.Sleep(timelineEvent.DelayBefore);

                    switch (timelineEvent.Command)
                    {
                        case "random":
                            while (true)
                            {
                                try
                                {
                                    var url = timelineEvent.CommandArgs[_random.Next(0, timelineEvent.CommandArgs.Count)].ToString();

                                    if (Driver == null)
                                        this.Driver = new IE(url);
                                    else
                                        Driver.GoTo(url);

                                    this.Report(handler.HandlerType.ToString(), timelineEvent.Command, url, timelineEvent.TrackableId);
                                }
                                catch (Exception e)
                                {
                                    Log.Trace(e);
                                }
                                Thread.Sleep(timelineEvent.DelayAfter);
                            }
                        case "browse":
                            Driver.GoTo(timelineEvent.CommandArgs[0].ToString());
                            this.Report(handler.HandlerType.ToString(), timelineEvent.Command, string.Join(",", timelineEvent.CommandArgs), timelineEvent.TrackableId);
                            break;
                    }

                    if (timelineEvent.DelayAfter > 0)
                        Thread.Sleep(timelineEvent.DelayAfter);
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        /// <summary>
        /// Close browser
        /// </summary>
        public void Close()
        {
            this.Report(HandlerType.BrowserChrome.ToString(), "Close", string.Empty);
            this.Driver.Close();
        }

        public void Stop()
        {
            this.Report(HandlerType.BrowserChrome.ToString(), "Stop", string.Empty);
            this.Close();
        }
    }
}
