// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Domain;
using OpenQA.Selenium.Chrome;
using System;
using Ghosts.Domain.Code.Helpers;
using OpenQA.Selenium;
// ReSharper disable StringLiteralTypo

namespace ghosts.client.linux.handlers
{
    public class BrowserChrome : BaseBrowserHandler
    {
        public new IJavaScriptExecutor JS { get; private set; }

        public BrowserChrome(TimelineHandler handler)
        {
            BrowserType = HandlerType.BrowserChrome;
            try
            {
                Driver = GetDriver(handler);
                base.Driver = Driver;
                
                JS = (IJavaScriptExecutor)Driver;
                base.JS = JS;

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
                _log.Error(e);
            }
        }

        internal static IWebDriver GetDriver(TimelineHandler handler)
        {
            var options = new ChromeOptions();
            options.AddArguments("disable-infobars");
            options.AddArguments("disable-logging");
            options.AddArguments("--disable-logging");
            options.AddArgument("--log-level=3");
            options.AddArgument("--silent");

            options.AddUserProfilePreference("download.default_directory", @"%homedrive%%homepath%\\Downloads");
            options.AddUserProfilePreference("disable-popup-blocking", "true");

            if (handler.HandlerArgs != null)
            {
                if (handler.HandlerArgs.ContainsKey("executable-location") &&
                    !string.IsNullOrEmpty(handler.HandlerArgs["executable-location"]))
                {
                    options.BinaryLocation = handler.HandlerArgs["executable-location"];
                }

                if (handler.HandlerArgs.ContainsKeyWithOption("isheadless", "true"))
                {
                    options.AddArguments("headless");
                }

                if (handler.HandlerArgs.ContainsKeyWithOption("incognito", "true"))
                {
                    options.AddArguments("--incognito");
                }

                if (handler.HandlerArgs.ContainsKeyWithOption("blockstyles", "true"))
                {
                    options.AddUserProfilePreference("profile.managed_default_content_settings.stylesheets", 2);
                }

                if (handler.HandlerArgs.ContainsKeyWithOption("blockimages", "true"))
                {
                    options.AddUserProfilePreference("profile.managed_default_content_settings.images", 2);
                }

                if (handler.HandlerArgs.ContainsKeyWithOption("blockflash", "true"))
                {
                    // ?
                }

                if (handler.HandlerArgs.ContainsKeyWithOption("blockscripts", "true"))
                {
                    options.AddUserProfilePreference("profile.managed_default_content_settings.javascript", 1);
                }
            }

            options.AddUserProfilePreference("profile.default_content_setting_values.notifications", 2);
            options.AddUserProfilePreference("profile.default_content_setting_values.geolocation", 2);
            options.AddUserProfilePreference("profile.managed_default_content_settings.cookies", 2);
            options.AddUserProfilePreference("profile.managed_default_content_settings.plugins", 2);
            options.AddUserProfilePreference("profile.managed_default_content_settings.popups", 2);
            options.AddUserProfilePreference("profile.managed_default_content_settings.geolocation", 2);
            options.AddUserProfilePreference("profile.managed_default_content_settings.media_stream", 2);

            if (!string.IsNullOrEmpty(Program.Configuration.ChromeExtensions))
            {
                options.AddArguments($"--load-extension={Program.Configuration.ChromeExtensions}");
            }
            
            _log.Trace("Browser preferences set successfully, getting driver...");
            
            ChromeDriver driver;
            
            try
            {
                driver = new ChromeDriver(options);
            }
            catch (Exception e)
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