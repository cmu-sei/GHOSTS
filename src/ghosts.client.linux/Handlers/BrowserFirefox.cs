// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Domain;
using NLog;
using System;
using System.IO;
using System.Text;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;

namespace ghosts.client.linux.handlers
{
    public class BrowserFirefox : BaseBrowserHandler
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public IWebDriver Driver { get; private set; }

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
            var path = "/bin/firefox";
            if (File.Exists(path))
            {
                return path;
            }

            path = "/usr/bin/firefox";
            return File.Exists(path) ? path : Program.Configuration.FirefoxInstallLocation;
        }

        private bool FirefoxEx(TimelineHandler handler)
        {
            try
            {
                var path = GetInstallLocation();

                FirefoxOptions options = new FirefoxOptions();
                options.AddArguments("--disable-infobars");
                options.AddArguments("--disable-extensions");
                options.AddArguments("--disable-notifications");

                //options.BrowserExecutableLocation = path;
                options.Profile = new FirefoxProfile();

                if (handler.HandlerArgs != null)
                {
                    if (handler.HandlerArgs.ContainsKey("isheadless") && handler.HandlerArgs["isheadless"] == "true")
                    {
                        options.AddArguments("--headless");
                    }
                    if (handler.HandlerArgs.ContainsKey("incognito") && handler.HandlerArgs["incognito"] == "true")
                    {
                        options.AddArguments("--incognito");
                    }
                    if (handler.HandlerArgs.ContainsKey("blockstyles") && handler.HandlerArgs["blockstyles"] == "true")
                    {
                        options.Profile.SetPreference("permissions.default.stylesheet", 2);
                    }
                    if (handler.HandlerArgs.ContainsKey("blockimages") && handler.HandlerArgs["blockimages"] == "true")
                    {
                        options.Profile.SetPreference("permissions.default.image", 2);
                    }
                    if (handler.HandlerArgs.ContainsKey("blockflash") && handler.HandlerArgs["blockflash"] == "true")
                    {
                        options.Profile.SetPreference("dom.ipc.plugins.enabled.libflashplayer.so", false);
                    }
                    if (handler.HandlerArgs.ContainsKey("blockscripts") && handler.HandlerArgs["blockscripts"] == "true")
                    {
                        options.Profile.SetPreference("permissions.default.script", 2);
                    }
                }

                options.Profile.SetPreference("permissions.default.cookies", 2);
                options.Profile.SetPreference("permissions.default.popups", 2);
                options.Profile.SetPreference("permissions.default.geolocation", 2);
                options.Profile.SetPreference("permissions.default.media_stream", 2);

                CodePagesEncodingProvider.Instance.GetEncoding(437);
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                Driver = new FirefoxDriver(options);
                base.Driver = Driver;

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
                
            }

            return true;
        }
    }
}
