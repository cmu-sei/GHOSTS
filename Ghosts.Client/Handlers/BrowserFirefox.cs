// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Ghosts.Client.Code;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using NLog;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Interactions;

namespace Ghosts.Client.Handlers
{
    public class BrowserFirefox : BaseHandler
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public IWebDriver Driver { get; private set; }

        private string GetFirefoxInstallLocation()
        {
            var path = @"C:\Program Files\Mozilla Firefox\firefox.exe";
            if (File.Exists(path))
                return path;

            path = @"C:\Program Files (x86)\Mozilla Firefox\firefox.exe";
            if (File.Exists(path))
                return path;

            return Program.Configuration.FirefoxInstallLocation;
        }

        private int GetFirefoxVersion(string path)
        {
            var versionInfo = FileVersionInfo.GetVersionInfo(path);
            return versionInfo.FileMajorPart;
        }

        private bool IsSufficientVersion(string path)
        {
            var currentVersion = GetFirefoxVersion(path);
            var minimumVersion = Program.Configuration.FirefoxMajorVersionMinimum;
            if (currentVersion < minimumVersion)
            {
                _log.Debug($"Firefox version ({currentVersion}) is incompatible - requires at least {minimumVersion}");
                return false;
            }
            return true;
        }

        public BrowserFirefox(TimelineHandler handler)
        {
            try
            {
                var path = GetFirefoxInstallLocation();
                
                if (!IsSufficientVersion(path))
                {
                    return;
                }

                var options = new FirefoxOptions();
                options.AddArguments("--disable-infobars");
                options.BrowserExecutableLocation = path;
                options.Profile = new FirefoxProfile();

                this.Driver = new FirefoxDriver(options);
                
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
                ProcessManager.KillProcessAndChildrenByName(ProcessManager.ProcessNames.Firefox);
                ProcessManager.KillProcessAndChildrenByName(ProcessManager.ProcessNames.GeckoDriver);
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
                                var url = timelineEvent.CommandArgs[new Random().Next(0, timelineEvent.CommandArgs.Count)];
                                Driver.Navigate()
                                    .GoToUrl(url);
                                this.Report(handler.HandlerType.ToString(), timelineEvent.Command, url, timelineEvent.TrackableId);
                                Thread.Sleep(timelineEvent.DelayAfter);
                            }
                        case "browse":
                            Driver.Navigate().GoToUrl(timelineEvent.CommandArgs[0]);
                            this.Report(handler.HandlerType.ToString(), timelineEvent.Command, string.Join(",", timelineEvent.CommandArgs), timelineEvent.TrackableId);
                            break;
                        case "download":
                            if (timelineEvent.CommandArgs.Count > 0)
                            {
                                var x = this.Driver.FindElement(By.XPath(timelineEvent.CommandArgs[0]));
                                x.Click();
                                this.Report(handler.HandlerType.ToString(), timelineEvent.Command, string.Join(",", timelineEvent.CommandArgs), timelineEvent.TrackableId);
                                Thread.Sleep(1000);
                            }
                            break;
                        case "type":
                            var e = Driver.FindElement(By.Name(timelineEvent.CommandArgs[0]));
                            e.SendKeys(timelineEvent.CommandArgs[1]);
                            //this.Report(timelineEvent);
                            break;
                        case "click":
                            var element = Driver.FindElement(By.Name(timelineEvent.CommandArgs[0]));
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
