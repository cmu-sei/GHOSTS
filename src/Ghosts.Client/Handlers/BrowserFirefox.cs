// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Client.Infrastructure;
using Ghosts.Domain;
using NLog;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;
using System;
using System.Diagnostics;
using System.IO;
using Ghosts.Domain.Code.Helpers;

namespace Ghosts.Client.Handlers
{
    public class BrowserFirefox : BaseBrowserHandler
    {
        private new static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public new IWebDriver Driver { get; private set; }
        public new IJavaScriptExecutor JS { get; private set; }

        public BrowserFirefox(TimelineHandler handler)
        {
            BrowserType = HandlerType.BrowserFirefox;
            bool hasRunSuccessfully = false;
            while (!hasRunSuccessfully)
            {
                hasRunSuccessfully = FirefoxEx(handler);
            }
        }

        private string GetInstallLocation()
        {
            var path = @"C:\Program Files\Mozilla Firefox\firefox.exe";
            if (File.Exists(path))
            {
                return path;
            }

            path = @"C:\Program Files (x86)\Mozilla Firefox\firefox.exe";
            return File.Exists(path) ? path : Program.Configuration.FirefoxInstallLocation;
        }

        private int GetFirefoxVersion(string path)
        {
            var versionInfo = FileVersionInfo.GetVersionInfo(path);
            return versionInfo.FileMajorPart;
        }

        private bool IsSufficientVersion(string path)
        {
            int currentVersion = GetFirefoxVersion(path);
            int minimumVersion = Program.Configuration.FirefoxMajorVersionMinimum;
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
                var path = GetInstallLocation();

                if (!IsSufficientVersion(path))
                {
                    _log.Warn("Firefox version is not sufficient. Exiting");
                    return true;
                }

                FirefoxOptions options = new FirefoxOptions();
                options.AddArguments("--disable-infobars");
                options.AddArguments("--disable-extensions");
                options.AddArguments("--disable-notifications");

                options.BrowserExecutableLocation = path;
                options.Profile = new FirefoxProfile();

                if (handler.HandlerArgs != null)
                {
                    if (handler.HandlerArgs.ContainsKeyWithOption("isheadless", "true"))
                    {
                        options.AddArguments("--headless");
                    }
                    if (handler.HandlerArgs.ContainsKeyWithOption("incognito", "true"))
                    {
                        options.AddArguments("--incognito");
                    }
                    if (handler.HandlerArgs.ContainsKeyWithOption("blockstyles", "true"))
                    {
                        options.Profile.SetPreference("permissions.default.stylesheet", 2);
                    }
                    if (handler.HandlerArgs.ContainsKeyWithOption("blockimages", "true"))
                    {
                        options.Profile.SetPreference("permissions.default.image", 2);
                    }
                    if (handler.HandlerArgs.ContainsKeyWithOption("blockflash", "true"))
                    {
                        options.Profile.SetPreference("dom.ipc.plugins.enabled.libflashplayer.so", false);
                    }
                    if (handler.HandlerArgs.ContainsKeyWithOption("blockscripts", "true"))
                    {
                        options.Profile.SetPreference("permissions.default.script", 2);
                    }
                }

                options.Profile.SetPreference("permissions.default.cookies", 2);
                options.Profile.SetPreference("permissions.default.popups", 2);
                options.Profile.SetPreference("permissions.default.geolocation", 2);
                options.Profile.SetPreference("permissions.default.media_stream", 2);

                options.Profile.SetPreference("geo.enabled", false);
                options.Profile.SetPreference("geo.prompt.testing", false);
                options.Profile.SetPreference("geo.prompt.testing.allow", false);

                Driver = new FirefoxDriver(options);
                base.Driver = Driver;

                JS = (IJavaScriptExecutor)Driver;
                base.JS = JS;

                //hack: bad urls used in the past...
                if (handler.Initial.Equals("") ||
                    handler.Initial.Equals("about:internal", StringComparison.InvariantCultureIgnoreCase) ||
                    handler.Initial.Equals("about:external", StringComparison.InvariantCultureIgnoreCase))
                {
                    handler.Initial = "about:blank";
                }

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
