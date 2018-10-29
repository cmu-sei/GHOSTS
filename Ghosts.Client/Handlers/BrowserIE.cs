// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading;
using Ghosts.Client.Code;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using NLog;
using WatiN.Core;

namespace Ghosts.Client.Handlers
{
    public class BrowserIE : BaseHandler
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

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
                _log.Debug(e);
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
                                    var url = timelineEvent.CommandArgs[new Random().Next(0, timelineEvent.CommandArgs.Count)];

                                    if (Driver == null)
                                        this.Driver = new IE(url);
                                    else
                                        Driver.GoTo(url);

                                    this.Report(handler.HandlerType.ToString(), timelineEvent.Command, url, timelineEvent.TrackableId);
                                }
                                catch (Exception e)
                                {
                                    _log.Trace(e);
                                }
                                Thread.Sleep(timelineEvent.DelayAfter);
                            }
                        case "browse":
                            Driver.GoTo(timelineEvent.CommandArgs[0]);
                            this.Report(handler.HandlerType.ToString(), timelineEvent.Command, string.Join(",", timelineEvent.CommandArgs), timelineEvent.TrackableId);
                            break;
                        //case "download":
                        //    if (timelineEvent.CommandArgs.Count > 0)
                        //    {
                        //        var x = this.Driver.FindElement(By.XPath(timelineEvent.CommandArgs[0]));
                        //        x.Click();
                        //        this.Report(handler.HandlerType.ToString(), timelineEvent.Command, string.Join(",", timelineEvent.CommandArgs), timelineEvent.TrackableId);
                        //        Thread.Sleep(1000);
                        //    }
                        //    break;
                        //case "type":
                        //    var e = Driver.FindElement(By.Name(timelineEvent.CommandArgs[0]));
                        //    e.SendKeys(timelineEvent.CommandArgs[1]);
                        //    //this.Report(timelineEvent);
                        //    break;
                        //case "click":
                        //    var element = Driver.FindElement(By.Name(timelineEvent.CommandArgs[0]));
                        //    var actions = new Actions(Driver);
                        //    actions.MoveToElement(element).Click().Perform();
                        //    //this.Report(timelineEvent);
                        //    break;
                    }

                    if (timelineEvent.DelayAfter > 0)
                        Thread.Sleep(timelineEvent.DelayAfter);
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
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
