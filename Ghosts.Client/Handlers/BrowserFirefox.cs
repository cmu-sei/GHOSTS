// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Client.Code;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using NLog;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Interactions;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

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
            {
                return path;
            }

            path = @"C:\Program Files (x86)\Mozilla Firefox\firefox.exe";
            if (File.Exists(path))
            {
                return path;
            }

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
            var hasRunSuccessfully = false;
            while (!hasRunSuccessfully)
            {
                hasRunSuccessfully = FirefoxEx(handler);
            }
        }

        private bool FirefoxEx(TimelineHandler handler)
        {
            try
            {
                var path = GetFirefoxInstallLocation();

                if (!IsSufficientVersion(path))
                {
                    _log.Warn("Firefox version is not sufficient. Exiting");
                    return true;
                }

                var options = new FirefoxOptions();
                options.AddArguments("--disable-infobars");
                options.BrowserExecutableLocation = path;
                options.Profile = new FirefoxProfile();

                Driver = new FirefoxDriver(options);

                Driver.Navigate().GoToUrl(handler.Initial);

                if (handler.Loop)
                {
                    while (true)
                    {
                        if (Driver.CurrentWindowHandle == null)
                        {
                            throw new Exception("Firefox window handle not available");
                        }

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
                return false;
            }
            finally
            {
                ProcessManager.KillProcessAndChildrenByName(ProcessManager.ProcessNames.Firefox);
                ProcessManager.KillProcessAndChildrenByName(ProcessManager.ProcessNames.GeckoDriver);
            }

            return true;
        }

        private void ExecuteEvents(TimelineHandler handler)
        {
            try
            {
                foreach (TimelineEvent timelineEvent in handler.TimeLineEvents)
                {
                    WorkingHours.Is(handler);

                    if (timelineEvent.DelayBefore > 0)
                    {
                        Thread.Sleep(timelineEvent.DelayBefore);
                    }

                    switch (timelineEvent.Command)
                    {
                        case "random":
                            while (true)
                            {
                                if (Driver.CurrentWindowHandle == null)
                                {
                                    throw new Exception("Firefox window handle not available");
                                }

                                string url = timelineEvent.CommandArgs[new Random().Next(0, timelineEvent.CommandArgs.Count)].ToString();
                                Driver.Navigate()
                                    .GoToUrl(url);
                                Report(handler.HandlerType.ToString(), timelineEvent.Command, url, timelineEvent.TrackableId);
                                Thread.Sleep(timelineEvent.DelayAfter);
                            }
                        case "browse":
                            Driver.Navigate().GoToUrl(timelineEvent.CommandArgs[0].ToString());
                            Report(handler.HandlerType.ToString(), timelineEvent.Command, string.Join(",", timelineEvent.CommandArgs), timelineEvent.TrackableId);
                            break;
                        case "download":
                            if (timelineEvent.CommandArgs.Count > 0)
                            {
                                IWebElement x = Driver.FindElement(By.XPath(timelineEvent.CommandArgs[0].ToString()));
                                x.Click();
                                Report(handler.HandlerType.ToString(), timelineEvent.Command, string.Join(",", timelineEvent.CommandArgs), timelineEvent.TrackableId);
                                Thread.Sleep(1000);
                            }
                            break;
                        case "type":
                            IWebElement e = Driver.FindElement(By.Name(timelineEvent.CommandArgs[0].ToString()));
                            e.SendKeys(timelineEvent.CommandArgs[1].ToString());
                            //this.Report(timelineEvent);
                            break;
                        case "click":
                            IWebElement element = Driver.FindElement(By.Name(timelineEvent.CommandArgs[0].ToString()));
                            Actions actions = new Actions(Driver);
                            actions.MoveToElement(element).Click().Perform();
                            //this.Report(timelineEvent);
                            break;
                    }

                    if (timelineEvent.DelayAfter > 0)
                    {
                        Thread.Sleep(timelineEvent.DelayAfter);
                    }
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
            Report(HandlerType.BrowserChrome.ToString(), "Close", string.Empty);
            Driver.Close();
        }

        public void Stop()
        {
            Report(HandlerType.BrowserChrome.ToString(), "Stop", string.Empty);
            Close();
        }
    }
}
