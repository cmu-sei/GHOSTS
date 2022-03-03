// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Client.Infrastructure;
using Ghosts.Domain;
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
        public BrowserFirefox(TimelineHandler handler)
        {
            BrowserType = HandlerType.BrowserFirefox;
            var hasRunSuccessfully = false;
            while (!hasRunSuccessfully)
            {
                hasRunSuccessfully = FirefoxEx(handler);
            }
        }

        private static string GetInstallLocation()
        {
            var path = @"C:\Program Files\Mozilla Firefox\firefox.exe";
            if (File.Exists(path))
            {
                return path;
            }

            path = @"C:\Program Files (x86)\Mozilla Firefox\firefox.exe";
            return File.Exists(path) ? path : Program.Configuration.FirefoxInstallLocation;
        }

        private static int GetFirefoxVersion(string path)
        {
            var versionInfo = FileVersionInfo.GetVersionInfo(path);
            return versionInfo.FileMajorPart;
        }

        private static bool IsSufficientVersion(string path)
        {
            var currentVersion = GetFirefoxVersion(path);
            var minimumVersion = Program.Configuration.FirefoxMajorVersionMinimum;
            if (currentVersion < minimumVersion)
            {
                Log.Debug($"Firefox version ({currentVersion}) is incompatible - requires at least {minimumVersion}");
                return false;
            }
            return true;
        }

        private bool FirefoxEx(TimelineHandler handler)
        {
            try
            {
                Driver = GetDriver(handler);
                base.Driver = Driver;

                if (handler.HandlerArgs.ContainsKey("javascript-enable"))
                {
                    JS = (IJavaScriptExecutor)Driver;
                    base.JS = JS;
                }

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
                Log.Debug(e);
                return false;
            }
            finally
            {
                ProcessManager.KillProcessAndChildrenByName(ProcessManager.ProcessNames.Firefox);
                ProcessManager.KillProcessAndChildrenByName(ProcessManager.ProcessNames.GeckoDriver);
            }

            return true;
        }

        internal static IWebDriver GetDriver(TimelineHandler handler)
        {
            var path = GetInstallLocation();

            if (!IsSufficientVersion(path))
            {
                Log.Warn("Firefox version is not sufficient. Exiting");
                return null;
            }

            var options = new FirefoxOptions();
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
            var driver = new FirefoxDriver(options);
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
            return driver;
        }
    }
}
