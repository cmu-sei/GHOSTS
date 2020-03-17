// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading;
using Ghosts.Client.Infrastructure.Browser;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using NLog;
using OpenQA.Selenium;
using OpenQA.Selenium.Interactions;

namespace Ghosts.Client.Handlers
{
    public abstract class BaseBrowserHandler : BaseHandler
    {
        public static readonly Logger _log = LogManager.GetCurrentClassLogger();
        public IWebDriver Driver { get; set; }
        public HandlerType BrowserType { get; set; }

        public void ExecuteEvents(TimelineHandler handler)
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

                    RequestConfiguration config;

                    IWebElement element;
                    Actions actions;

                    switch (timelineEvent.Command)
                    {
                        case "random":

                            var stickiness = 0;
                            var depthMin = 1;
                            var depthMax = 10;
                            if (handler.HandlerArgs.ContainsKey("stickiness"))
                            {
                                int.TryParse(handler.HandlerArgs["stickiness"], out stickiness);
                            }
                            if (handler.HandlerArgs.ContainsKey("stickiness-depth-min"))
                            {
                                int.TryParse(handler.HandlerArgs["stickiness-depth-min"], out depthMin);
                            }
                            if (handler.HandlerArgs.ContainsKey("stickiness-depth-max"))
                            {
                                int.TryParse(handler.HandlerArgs["stickiness-depth-max"], out depthMax);
                            }

                            while (true)
                            {
                                if (Driver.CurrentWindowHandle == null)
                                {
                                    throw new Exception("Browser window handle not available");
                                }

                                config = RequestConfiguration.Load(timelineEvent.CommandArgs[new Random().Next(0, timelineEvent.CommandArgs.Count)]);
                                if (config.Uri.IsWellFormedOriginalString())
                                {
                                    MakeRequest(config);
                                    Report(handler.HandlerType.ToString(), timelineEvent.Command, config.ToString(), timelineEvent.TrackableId);

                                    if (stickiness > 0)
                                    {
                                        var random = new Random();
                                        //now some percentage of the time should stay on this site
                                        if (random.Next(100) < stickiness)
                                        {
                                            var loops = random.Next(depthMin, depthMax);
                                            for (var loopNumber = 0; loopNumber < loops; loopNumber++)
                                            {
                                                try
                                                {
                                                    //get all links
                                                    var links = Driver.FindElements(By.TagName("a"));
                                                    if (links.Count > 0)
                                                    {
                                                        var linkSelected = random.Next(links.Count);
                                                        var href = links[linkSelected].GetAttribute("href");
                                                        while (!href.StartsWith("http"))
                                                        {
                                                            foreach (var l in links)
                                                            {
                                                                href = l.GetAttribute("href");
                                                            }
                                                        }

                                                        if (!string.IsNullOrEmpty(href))
                                                        {
                                                            if (!href.StartsWith("http"))
                                                            {
                                                                href = $"http://{href}";
                                                            }
                                                            config.Method = "GET";
                                                            config.Uri = new Uri(href);

                                                            MakeRequest(config);
                                                            Report(handler.HandlerType.ToString(), timelineEvent.Command,config.ToString(), timelineEvent.TrackableId);
                                                        }
                                                    }
                                                }
                                                catch (Exception e)
                                                {
                                                    _log.Error(e);
                                                }
                                                Thread.Sleep(timelineEvent.DelayAfter);
                                            }
                                        }
                                    }
                                }
                                Thread.Sleep(timelineEvent.DelayAfter);
                            }
                        case "browse":
                            config = RequestConfiguration.Load(timelineEvent.CommandArgs[0]);
                            if (config.Uri.IsWellFormedOriginalString())
                            {
                                MakeRequest(config);
                                Report(handler.HandlerType.ToString(), timelineEvent.Command, config.ToString(), timelineEvent.TrackableId);
                            }
                            break;
                        case "download":
                            if (timelineEvent.CommandArgs.Count > 0)
                            {
                                element = Driver.FindElement(By.XPath(timelineEvent.CommandArgs[0].ToString()));
                                element.Click();
                                Report(handler.HandlerType.ToString(), timelineEvent.Command, string.Join(",", timelineEvent.CommandArgs), timelineEvent.TrackableId);
                                Thread.Sleep(1000);
                            }
                            break;
                        case "type":
                            element = Driver.FindElement(By.Name(timelineEvent.CommandArgs[0].ToString()));
                            actions = new Actions(Driver);
                            actions.SendKeys(element, timelineEvent.CommandArgs[1].ToString()).Build().Perform();
                            break;
                        case "typebyid":
                            element = Driver.FindElement(By.Id(timelineEvent.CommandArgs[0].ToString()));
                            actions = new Actions(Driver);
                            actions.SendKeys(element, timelineEvent.CommandArgs[1].ToString()).Build().Perform();
                            break;
                        case "click":
                            element = Driver.FindElement(By.Name(timelineEvent.CommandArgs[0].ToString()));
                            actions = new Actions(Driver);
                            actions.MoveToElement(element).Click().Perform();
                            break;
                        case "clickbyid":
                            element = Driver.FindElement(By.Id(timelineEvent.CommandArgs[0].ToString()));
                            actions = new Actions(Driver);
                            actions.MoveToElement(element).Click().Perform();
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

        private void MakeRequest(RequestConfiguration config)
        {
            switch (config.Method.ToUpper())
            {
                case "GET":
                    Driver.Navigate().GoToUrl(config.Uri);
                    break;
                case "POST":
                case "PUT":
                case "DELETE":
                    Driver.Navigate().GoToUrl("about:blank");
                    var script = "var xhr = new XMLHttpRequest();";
                    script += $"xhr.open('{config.Method.ToUpper()}', '{config.Uri}', true);";
                    script += "xhr.setRequestHeader('Content-type', 'application/x-www-form-urlencoded');";
                    script += "xhr.onload = function() {";
                    script += "document.write(this.responseText);";
                    script += "};";
                    script += $"xhr.send('{config.FormValues.ToFormValueString()}');";

                    var javaScriptExecutor = (IJavaScriptExecutor)Driver;
                    javaScriptExecutor.ExecuteScript(script);
                    break;
            }
        }

        /// <summary>
        /// Close browser
        /// </summary>
        public void Close()
        {
            Report(BrowserType.ToString(), "Close", string.Empty);
            Driver.Close();
        }

        public void Stop()
        {
            Report(BrowserType.ToString(), "Stop", string.Empty);
            Close();
        }
    }
}