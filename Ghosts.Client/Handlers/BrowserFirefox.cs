// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Client.Infrastructure;
using Ghosts.Domain;
using NLog;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;
using System;
using System.Diagnostics;
using System.IO;

namespace Ghosts.Client.Handlers
{
    public class BrowserFirefox : BaseBrowserHandler
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public IWebDriver Driver { get; private set; }

        public BrowserFirefox(TimelineHandler handler)
        {
            this.BrowserType = HandlerType.BrowserFirefox;
            var hasRunSuccessfully = false;
            while (!hasRunSuccessfully)
            {
                hasRunSuccessfully = FirefoxEx(handler);
            }
        }

        private string GetFirefoxInstallLocation()
        {
            string path = @"C:\Program Files\Mozilla Firefox\firefox.exe";
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
            FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(path);
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
        
        private bool FirefoxEx(TimelineHandler handler)
        {
            try
            {
                string path = GetFirefoxInstallLocation();

                if (!IsSufficientVersion(path))
                {
                    _log.Warn("Firefox version is not sufficient. Exiting");
                    return true;
                }

                FirefoxOptions options = new FirefoxOptions();
                options.AddArguments("--disable-infobars");
                if (handler.HandlerArgs != null &&
                    handler.HandlerArgs.ContainsKey("isheadless") && 
                    handler.HandlerArgs["isheadless"] == "true")
                {
                    options.AddArguments("--headless");
                }
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
    }
}
