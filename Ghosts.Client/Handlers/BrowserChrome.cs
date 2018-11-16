// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading;
using Ghosts.Client.Code;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using NLog;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Interactions;

namespace Ghosts.Client.Handlers
{
    public class BrowserChrome : BaseHandler
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public IWebDriver Driver { get; private set; }

        public BrowserChrome(TimelineHandler handler)
        {
            try
            {
                var options = new ChromeOptions();
                options.AddArguments("disable-infobars");
                options.AddArguments("disable-logging");
                options.AddArguments("--disable-logging");

                var chromeOptions = new ChromeOptions();
                chromeOptions.AddUserProfilePreference("download.default_directory", @"%homedrive%%homepath%\\Downloads");
                chromeOptions.AddUserProfilePreference("disable-popup-blocking", "true");
                    
                if (!string.IsNullOrEmpty(Program.Configuration.ChromeExtensions))
                {
                    options.AddArguments($"--load-extension={ Program.Configuration.ChromeExtensions }");
                }

                this.Driver = new ChromeDriver(options);

                Driver.Navigate().GoToUrl(handler.Initial);

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
            finally
            {
                ProcessManager.KillProcessAndChildrenByName(ProcessManager.ProcessNames.Chrome);
                ProcessManager.KillProcessAndChildrenByName(ProcessManager.ProcessNames.ChromeDriver);
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
                                var url = timelineEvent.CommandArgs[new Random().Next(0, timelineEvent.CommandArgs.Count)].ToString();
                                Driver.Navigate()
                                    .GoToUrl(url);
                                this.Report(handler.HandlerType.ToString(), timelineEvent.Command, url, timelineEvent.TrackableId);
                                Thread.Sleep(timelineEvent.DelayAfter);
                            }
                        case "browse":
                            Driver.Navigate().GoToUrl(timelineEvent.CommandArgs[0].ToString());
                            this.Report(handler.HandlerType.ToString(), timelineEvent.Command, string.Join(",", timelineEvent.CommandArgs), timelineEvent.TrackableId);
                            break;
                        case "download":
                            if (timelineEvent.CommandArgs.Count > 0)
                            {
                                var x = this.Driver.FindElement(By.XPath(timelineEvent.CommandArgs[0].ToString()));
                                x.Click();
                                this.Report(handler.HandlerType.ToString(), timelineEvent.Command, string.Join(",", timelineEvent.CommandArgs), timelineEvent.TrackableId);
                                Thread.Sleep(1000);
                            }
                            break;
                        case "type":
                            var e = Driver.FindElement(By.Name(timelineEvent.CommandArgs[0].ToString()));
                            e.SendKeys(timelineEvent.CommandArgs[1].ToString());
                            //this.Report(timelineEvent);
                            break;
                        case "click":
                            var element = Driver.FindElement(By.Name(timelineEvent.CommandArgs[0].ToString()));
                            var actions = new Actions(Driver);
                            actions.MoveToElement(element).Click().Perform();
                            //this.Report(timelineEvent);
                            break;
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
