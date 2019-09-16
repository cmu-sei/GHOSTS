// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using Ghosts.Client.Infrastructure;
using Ghosts.Domain;
using OpenQA.Selenium.Chrome;

namespace Ghosts.Client.Handlers
{
    public class BrowserChrome : BaseBrowserHandler
    {
        public BrowserChrome(TimelineHandler handler)
        {
            this.BrowserType = HandlerType.BrowserChrome;
            try
            {
                var options = new ChromeOptions();
                options.AddArguments("disable-infobars");
                options.AddArguments("disable-logging");
                options.AddArguments("--disable-logging");
                options.AddArgument("--log-level=3");
                options.AddArgument("--silent");
                if (handler.HandlerArgs != null && 
                    handler.HandlerArgs.ContainsKey("isheadless") &&
                    handler.HandlerArgs["isheadless"] == "true")
                {
                    options.AddArguments("headless");
                }

                var chromeOptions = new ChromeOptions();
                chromeOptions.AddUserProfilePreference("download.default_directory", @"%homedrive%%homepath%\\Downloads");
                chromeOptions.AddUserProfilePreference("disable-popup-blocking", "true");
                    
                if (!string.IsNullOrEmpty(Program.Configuration.ChromeExtensions))
                {
                    options.AddArguments($"--load-extension={ Program.Configuration.ChromeExtensions }");
                }

                this.Driver = new ChromeDriver(options);
                base.Driver = this.Driver;

                Driver.Navigate().GoToUrl(handler.Initial);

                if (handler.Loop)
                {
                    while (true)
                    {
                        this.ExecuteEvents(handler);
                    }
                }
                else
                {
                    this.ExecuteEvents(handler);
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

        
    }
}
