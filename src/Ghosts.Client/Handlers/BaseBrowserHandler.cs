// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Client.Infrastructure;

using Ghosts.Domain;
using Ghosts.Domain.Code;
using Ghosts.Domain.Code.Helpers;
using OpenQA.Selenium;
using OpenQA.Selenium.Interactions;
using static System.Net.WebRequestMethods;
using Newtonsoft.Json;
using System.IO;
using System.Xml.Linq;
using OpenQA.Selenium.Support.UI;
using Microsoft.Office.Interop.Outlook;
using Actions = OpenQA.Selenium.Interactions.Actions;
using Exception = System.Exception;
using System.Diagnostics;

namespace Ghosts.Client.Handlers
{
    public abstract class BaseBrowserHandler : BaseHandler
    {
        public IWebDriver Driver { get; set; }
        public IJavaScriptExecutor JS { get; set; }
        public HandlerType BrowserType { get; set; }
        internal bool Restart { get; set; }
        private int _stickiness;
        private int _depthMin = 1;
        private int _depthMax = 10;
        private int _visitedRemember = 5;
        private int _actionsBeforeRestart = -1;
        private LinkManager _linkManager;
        private int _actionsCount = 0;

        public bool sharepointAbort { get; set; } = false;  //will be set to True if unable to proceed with Handler execution
        SharepointHelper _sharepointhelper = null;

        public bool blogAbort { get; set; } = false;  //will be set to True if unable to proceed with Handler execution
        BlogHelper _bloghelper = null;




        private Task LaunchThread(TimelineHandler handler, TimelineEvent timelineEvent, string site)
        {
            var o = new BrowserCrawl();
            return o.Crawl(handler, timelineEvent, site);
        }

        public void ExecuteEvents(TimelineHandler handler)
        {
            try
            {
                foreach (var timelineEvent in handler.TimeLineEvents)
                {
                    Infrastructure.WorkingHours.Is(handler);

                    if (timelineEvent.DelayBefore > 0)
                    {
                        Thread.Sleep(timelineEvent.DelayBefore);
                    }

                    RequestConfiguration config;

                    IWebElement element;
                    Actions actions;

                    switch (timelineEvent.Command)
                    {
                        case "crawl":
                            var taskMax = 1;
                            if (handler.HandlerArgs.ContainsKey("crawl-tasks-maximum"))
                            {
                                int.TryParse(handler.HandlerArgs["crawl-tasks-maximum"].ToString(), out taskMax);
                            }

                            var i = 0;
                            foreach (var site in timelineEvent.CommandArgs)
                            {
                                Task.Factory.StartNew(() => LaunchThread(handler, timelineEvent, site.ToString()));
                                Thread.Sleep(5000);
                                i++;

                                if (i >= taskMax)
                                {
                                    Task.WaitAll();
                                    i = 0;
                                }
                            }
                            break;
                       case "sharepoint":
                            if (!sharepointAbort)
                            {
                                if (_sharepointhelper == null)
                                {
                                    _sharepointhelper = SharepointHelper.MakeHelper(this, Driver, handler, Log);
                                    if (_sharepointhelper == null) sharepointAbort = true;
                                }

                                if (_sharepointhelper != null) _sharepointhelper.Execute(handler, timelineEvent);
                            }
                            break;
                       case "blog":
                            if (!blogAbort)
                            {
                                if (_bloghelper == null)
                                {
                                    _bloghelper = BlogHelper.MakeHelper(this, Driver, handler, Log);
                                    if  (_bloghelper == null) blogAbort = true;  //failed to create a helper
                                }
                                if (_bloghelper != null) _bloghelper.Execute(handler, timelineEvent);
                            }
                            break;
                        case "random":

                            // setup
                            if (handler.HandlerArgs.ContainsKey("stickiness"))
                            {
                                int.TryParse(handler.HandlerArgs["stickiness"].ToString(), out _stickiness);
                            }
                            if (handler.HandlerArgs.ContainsKey("stickiness-depth-min"))
                            {
                                int.TryParse(handler.HandlerArgs["stickiness-depth-min"].ToString(), out _depthMin);
                            }
                            if (handler.HandlerArgs.ContainsKey("stickiness-depth-max"))
                            {
                                int.TryParse(handler.HandlerArgs["stickiness-depth-max"].ToString(), out _depthMax);
                            }
                            if (handler.HandlerArgs.ContainsKey("visited-remember"))
                            {
                                int.TryParse(handler.HandlerArgs["visited-remember"].ToString(), out _visitedRemember);
                            }
                            if (handler.HandlerArgs.ContainsKey("actions-before-restart"))
                            {
                                int.TryParse(handler.HandlerArgs["actions-before-restart"].ToString(), out _actionsBeforeRestart);
                            }

                            this._linkManager = new LinkManager(_visitedRemember);

                            while (true)
                            {
                                if (Driver.CurrentWindowHandle == null)
                                {
                                    throw new Exception("Browser window handle not available");
                                }

                                config = RequestConfiguration.Load(handler, timelineEvent.CommandArgs[_random.Next(0, timelineEvent.CommandArgs.Count)]);
                                if (config.Uri != null && config.Uri.IsWellFormedOriginalString())
                                {
                                    this._linkManager.SetCurrent(config.Uri);
                                    MakeRequest(config);
                                    Report(handler.HandlerType.ToString(), timelineEvent.Command, config.ToString(), timelineEvent.TrackableId);

                                    if (this._stickiness > 0)
                                    {
                                        //now some percentage of the time should stay on this site
                                        if (_random.Next(100) < this._stickiness)
                                        {
                                            var loops = _random.Next(this._depthMin, this._depthMax);
                                            Log.Trace($"Beginning {loops} loops on {config.Uri}");
                                            for (var loopNumber = 0; loopNumber < loops; loopNumber++)
                                            {
                                                try
                                                {
                                                    this._linkManager.SetCurrent(config.Uri);
                                                    GetAllLinks(config, false);
                                                    var link = this._linkManager.Choose();
                                                    if (link == null)
                                                    {
                                                        return;
                                                    }

                                                    config.Method = "GET";
                                                    config.Uri = link.Url;

                                                    Log.Trace($"Making request #{loopNumber+1}/{loops} to {config.Uri}");
                                                    MakeRequest(config);
                                                    Report(handler.HandlerType.ToString(), timelineEvent.Command, config.ToString(), timelineEvent.TrackableId);
                                                }
                                                catch (ThreadAbortException)
                                                {
                                                    ProcessManager.KillProcessAndChildrenByName(this.BrowserType.ToString().Replace("Browser", ""));
                                                    Log.Trace($"Thread aborted, {this.BrowserType.ToString()} closing...");
                                                    return;
                                                }
                                                catch (Exception e)
                                                {
                                                    Log.Error($"Browser loop error {e}");
                                                }

                                                if (_actionsBeforeRestart > 0)
                                                {
                                                    if (this._actionsCount.IsDivisibleByN(10))
                                                    {
                                                        Log.Trace($"Browser actions == {this._actionsCount}");
                                                    }
                                                    if (this._actionsCount > _actionsBeforeRestart)
                                                    {
                                                        this.Restart = true;
                                                        Log.Trace("Browser reached action threshold. Restarting...");
                                                        return;
                                                    }
                                                }

                                                Thread.Sleep(timelineEvent.DelayAfter);
                                            }
                                        }
                                    }
                                }

                                if (_actionsBeforeRestart > 0)
                                {
                                    if (this._actionsCount.IsDivisibleByN(10))
                                    {
                                        Log.Trace($"Browser actions == {this._actionsCount}");
                                    }
                                    if (this._actionsCount > _actionsBeforeRestart)
                                    {
                                        this.Restart = true;
                                        Log.Trace("Browser reached action threshold. Restarting...");
                                        return;
                                    }
                                }

                                Thread.Sleep(timelineEvent.DelayAfter);
                            }
                        case "browse":
                            config = RequestConfiguration.Load(handler, timelineEvent.CommandArgs[0]);
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
                        case "click.by.name":
                            element = Driver.FindElement(By.Name(timelineEvent.CommandArgs[0].ToString()));
                            actions = new Actions(Driver);
                            actions.MoveToElement(element).Click().Perform();
                            break;
                        case "clickbyid":
                        case "click.by.id":
                            element = Driver.FindElement(By.Id(timelineEvent.CommandArgs[0].ToString()));
                            actions = new Actions(Driver);
                            actions.MoveToElement(element).Click().Perform();
                            break;
                        case "click.by.linktext":
                            element = Driver.FindElement(By.LinkText(timelineEvent.CommandArgs[0].ToString()));
                            actions = new Actions(Driver);
                            actions.MoveToElement(element).Click().Perform();
                            break;
                        case "click.by.cssselector":
                            element = Driver.FindElement(By.CssSelector(timelineEvent.CommandArgs[0].ToString()));
                            actions = new Actions(Driver);
                            actions.MoveToElement(element).Click().Perform();
                            break;
                        case "js.executescript":
                            JS.ExecuteScript(timelineEvent.CommandArgs[0].ToString());
                            break;
                        case "manage.window.size":
                            Driver.Manage().Window.Size = new System.Drawing.Size(Convert.ToInt32(timelineEvent.CommandArgs[0]), Convert.ToInt32(timelineEvent.CommandArgs[1]));
                            break;
                    }

                    if (timelineEvent.DelayAfter > 0)
                    {
                        Thread.Sleep(timelineEvent.DelayAfter);
                    }
                }
            }
            catch (ThreadAbortException)
            {
                ProcessManager.KillProcessAndChildrenByName(this.BrowserType.ToString().Replace("Browser", ""));
                Log.Trace($"Thread aborted, {this.BrowserType.ToString()} closing...");
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        
        private void GetAllLinks(RequestConfiguration config, bool sameSite)
        {
            try
            {
                var links = Driver.FindElements(By.TagName("a"));
                foreach (var l in links)
                {
                    var node = l.GetAttribute("href");
                    if (string.IsNullOrEmpty(node))
                        continue;
                    if (Uri.TryCreate(node, UriKind.RelativeOrAbsolute, out var uri))
                    {
                        if (uri.GetDomain() != config.Uri.GetDomain())
                        {
                            if (!sameSite)
                                this._linkManager.AddLink(uri, 1);
                        }
                        // relative links - prefix the scheme and host 
                        else
                        {
                            this._linkManager.AddLink(uri, 2);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Trace(e);
            }
        }

        public void MakeRequest(RequestConfiguration config)
        {
            // Added try here because some versions of FF (v56) throw an exception for an unresolved site,
            // but in other versions it seems to fail gracefully. We want to always fail gracefully
            try
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

                this._actionsCount++;
            }
            catch (Exception e)
            {
                Log.Trace(e.Message);
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