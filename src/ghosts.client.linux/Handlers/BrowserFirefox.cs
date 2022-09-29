// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Domain;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Ghosts.Domain.Code.Helpers;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;
using Newtonsoft.Json.Linq;

namespace ghosts.client.linux.handlers
{
    public class BrowserFirefox : BaseBrowserHandler
    {
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

        private static int GetFirefoxVersion(string path)
        {
            var versionInfo = FileVersionInfo.GetVersionInfo(path);
            return versionInfo.FileMajorPart;
        }

        private static bool IsSufficientVersion(string path)
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

        internal static string GetInstallLocation()
        {
            var path = "/bin/firefox";
            if (File.Exists(path))
            {
                return path;
            }

            path = "/usr/bin/firefox";
            var retVal = File.Exists(path) ? path : Program.Configuration.FirefoxInstallLocation;
            _log.Trace($"Using install location of [{retVal}]");
            return retVal;
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
                _log.Debug(e);
                return false;
            }

            return true;
        }

        internal static IWebDriver GetDriver(TimelineHandler handler)
        {
            var path = GetInstallLocation();

            var options = new FirefoxOptions();
            options.BrowserExecutableLocation = path;
            options.AddArguments("--disable-infobars");
            options.AddArguments("--disable-extensions");
            options.AddArguments("--disable-notifications");
            if (handler.HandlerArgs.ContainsKey("command-line-args"))
            {
                foreach (var option in (JArray)handler.HandlerArgs["command-line-args"])
                {
                    options.AddArgument(option.Value<string>());
                }
            }

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

            _log.Trace("Browser preferences set successfully, getting driver...");

            CodePagesEncodingProvider.Instance.GetEncoding(437);
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            FirefoxDriver driver;

            try
            {
                driver = new FirefoxDriver(options);
            }
            catch
            {
                _log.Trace("Driver could not be instantiated. Does the proper driver exist? Are you running as a user and not root? Sometimes running the driver directly will uncover the underlaying issue.");
                throw;
            }

            _log.Trace("Driver instantiated successfully, setting timeouts...");
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
            _log.Trace("Driver timeouts set successfully, continuing...");
            return driver;
        }
    }
}