// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Client.Infrastructure;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Ghosts.Domain.Code.Helpers;
using OpenQA.Selenium;
using Actions = OpenQA.Selenium.Interactions.Actions;
using Exception = System.Exception;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ghosts.Client.Handlers
{
    public abstract class BaseBrowserHandler : BaseHandler
    {
        public IWebDriver Driver { get; set; }
        public IJavaScriptExecutor JS { get; set; }
        public HandlerType BrowserType { get; set; }
        internal bool Restart { get; set; }
        public int stickiness;
        public int depthMin = 1;
        public int depthMax = 10;
        public int visitedRemember = 5;
        public int actionsBeforeRestart = -1;
        public LinkManager linkManager;
        public int actionsCount = 0;
        public int browseprobability = 100;
        public int jitterfactor { get; set; }  = 0;  //used with Jitter.JitterFactorDelay

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
                            ParseRandomHandlerArgs(handler);
                            DoRandomCommand(handler, timelineEvent);
                            if (this.Restart) return;  //restart has been requested
                            break;

                        case "randomalt":
                            ParseRandomHandlerArgs(handler);
                            DoRandomAltCommand(handler, timelineEvent);
                            if (this.Restart) return;  //restart has been requested
                            break;

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
                        case "upload":
                            ParseRandomHandlerArgs(handler);
                            Upload(handler, timelineEvent);
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

        public void ParseRandomHandlerArgs(TimelineHandler handler)
        {
            if (handler.HandlerArgs.ContainsKey("stickiness"))
            {
                int.TryParse(handler.HandlerArgs["stickiness"].ToString(), out stickiness);
            }
            if (handler.HandlerArgs.ContainsKey("stickiness-depth-min"))
            {
                int.TryParse(handler.HandlerArgs["stickiness-depth-min"].ToString(), out depthMin);
            }
            if (handler.HandlerArgs.ContainsKey("stickiness-depth-max"))
            {
                int.TryParse(handler.HandlerArgs["stickiness-depth-max"].ToString(), out depthMax);
            }
            if (handler.HandlerArgs.ContainsKey("visited-remember"))
            {
                int.TryParse(handler.HandlerArgs["visited-remember"].ToString(), out visitedRemember);
            }
            if (handler.HandlerArgs.ContainsKey("actions-before-restart"))
            {
                int.TryParse(handler.HandlerArgs["actions-before-restart"].ToString(), out actionsBeforeRestart);
            }
            if (handler.HandlerArgs.ContainsKey("browse-probability"))
            {
                int.TryParse(handler.HandlerArgs["browse-probability"].ToString(), out browseprobability);
                if (browseprobability < 0 || browseprobability > 100) browseprobability = 100;
            }
            if (handler.HandlerArgs.ContainsKey("delay-jitter"))
            {
                jitterfactor = Jitter.JitterFactorParse(handler.HandlerArgs["delay-jitter"].ToString());
            }
        }

        public void Upload(TimelineHandler handler, TimelineEvent timelineEvent)
        {
            var config = RequestConfiguration.Load(handler, timelineEvent.CommandArgs[_random.Next(0, timelineEvent.CommandArgs.Count)]);

            Driver.Navigate().GoToUrl("about:blank");
            var script = new StringBuilder("var xhr = new XMLHttpRequest();");
            script.Append($"xhr.open('{config.Method.ToUpper()}', '{config.Uri}', true);");
            script.Append("xhr.setRequestHeader('Content-Type', 'multipart/form-data');");
            script.Append("var formData = new FormData();");
            var f = config.FormValues["file"];
            script.Append($"formData.append('file', '{f}');");
            script.Append("xhr.onload = function() {");
            script.Append("document.write(this.responseText);");
            script.Append("};");
            script.Append($"xhr.send('{config.FormValues.ToFormValueString()}');");

            script.Append("xhr.addEventListener('load', function(event) {");
            script.Append("console.log('File uploaded successfully');");
            script.Append("});");

            script.Append("xhr.addEventListener('error', function(event) {");
            script.Append("console.error('Error uploading file');");
            script.Append("});");

            var javaScriptExecutor = (IJavaScriptExecutor)Driver;
            javaScriptExecutor.ExecuteScript(script.ToString());

            Console.WriteLine("done");
        }

        public void DoRandomCommand(TimelineHandler handler, TimelineEvent timelineEvent)
        {
            RequestConfiguration config;

            this.linkManager = new LinkManager(visitedRemember);

            while (true)
            {
                if (Driver.CurrentWindowHandle == null)
                {
                    throw new Exception("Browser window handle not available");
                }

                config = RequestConfiguration.Load(handler, timelineEvent.CommandArgs[_random.Next(0, timelineEvent.CommandArgs.Count)]);
                if (config.Uri != null && config.Uri.IsWellFormedOriginalString())
                {
                    this.linkManager.SetCurrent(config.Uri);
                    MakeRequest(config);
                    Report(handler.HandlerType.ToString(), timelineEvent.Command, config.ToString(), timelineEvent.TrackableId);
                    Thread.Sleep(timelineEvent.DelayAfter);

                    if (this.stickiness > 0)
                    {
                        //now some percentage of the time should stay on this site
                        if (_random.Next(100) < this.stickiness)
                        {
                            var loops = _random.Next(this.depthMin, this.depthMax);
                            Log.Trace($"Beginning {loops} loops on {config.Uri}");
                            for (var loopNumber = 0; loopNumber < loops; loopNumber++)
                            {
                                try
                                {
                                    this.linkManager.SetCurrent(config.Uri);
                                    GetAllLinks(config, false);
                                    var link = this.linkManager.Choose();
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

                                if (actionsBeforeRestart > 0)
                                {
                                    if (this.actionsCount.IsDivisibleByN(10))
                                    {
                                        Log.Trace($"Browser actions == {this.actionsCount}");
                                    }
                                    if (this.actionsCount > actionsBeforeRestart)
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

                if (actionsBeforeRestart > 0)
                {
                    if (this.actionsCount.IsDivisibleByN(10))
                    {
                        Log.Trace($"Browser actions == {this.actionsCount}");
                    }
                    if (this.actionsCount > actionsBeforeRestart)
                    {
                        this.Restart = true;
                        Log.Trace("Browser reached action threshold. Restarting...");
                        return;
                    }
                }
                
                Thread.Sleep(timelineEvent.DelayAfter);

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
                                this.linkManager.AddLink(uri, 1);
                        }
                        // relative links - prefix the scheme and host 
                        else
                        {
                            this.linkManager.AddLink(uri, 2);
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

                this.actionsCount++;
            }
            catch (Exception e)
            {

                Log.Trace(e.Message);
                HandleBrowserException(e);

            }
        }

        private bool isGoodLink(Uri uri)
        {
            //check if this link needs to be rejected
            var linkText = uri.ToString();
            if (linkText.Contains("support.mozilla.org") && linkText.Contains("connection-not-secure"))
            {
                //for some reason, occassionaly the Insecure Cert exception is not thrown and this
                //page about the security issue is not filtered. Do not click this link as it pops
                //up another page. Just reject it, as it is the only link on the page, and we will
                //pop back up to the top and try again
                Log.Trace($"Rejected link {linkText}");
                return false;  
            }



            return true;
        }

        private bool ClickRandomLink(RequestConfiguration config, Dictionary<string, int> urlDict, LifoQueue<Uri> urlQueue)
        {
            try
            {
                var elementList = new List<IWebElement>();
                var uriList = new List<Uri>();
                var links = Driver.FindElements(By.TagName("a"));
                string[] validSchemes = { "http", "https" };
                foreach (var l in links)
                {
                    var node = l.GetAttribute("href");
                    if (string.IsNullOrEmpty(node))
                        continue;
                    if (Uri.TryCreate(node, UriKind.RelativeOrAbsolute, out var uri))
                    {
                        if (validSchemes.Contains(uri.Scheme) && isGoodLink(uri))
                        {
                            if (urlDict.ContainsKey(uri.ToString())) continue;  //skip this
                            elementList.Add(l);
                            uriList.Add(uri);
                        }
                    }
                }
                if (uriList.Count == 0) return false;  //no links to click
                int linkNum = _random.Next(0, uriList.Count);
                var targetUri = uriList[linkNum];
                var targetElement = elementList[linkNum];
                //remember this Url
                if (urlDict.Count == visitedRemember && visitedRemember > 0)
                {
                    //at capacity, need to remove oldest
                    var lastItem = urlQueue.Last();
                    urlDict.Remove(lastItem.ToString());  //remove from dict
                }
                urlQueue.Add(targetUri);  //Queue is at capacity, last item is removed when this is added
                urlDict.Add(targetUri.ToString(), 0);
                config.Method = "GET";
                config.Uri = targetUri;  //set this so that can print out info on return


                if (targetUri.ToString().ToLower().EndsWith(".htm") || targetUri.ToString().ToLower().EndsWith(".html"))
                {
                    Driver.Navigate().GoToUrl(targetUri);
                }
                else
                {
                    BrowserHelperSupport.MoveToElementAndClick(Driver, targetElement);
                }
                this.actionsCount++;
                return true;

            }
            catch (Exception e)
            {
                Log.Trace(e);
                HandleBrowserException(e);
            }
            return true;
        }

        /// <summary>
        /// This differs from 'random' in the following ways
        ///   Jitter factor (default 0) is used in delayAfter - random value chosen from delay-delay*%jitterfactor, delay+delay*%jitterfactor
        ///   During stickiness browsing, a random link is chosen from a page with no preference to relative links
        ///   If no random links found, bounce back up and chose another link from the timeline.
        ///   After a random link is chosen, NavigateTo() is used only if the link ends with .htm/.html, else 
        ///   Javascript is used to click the link. This avoids a problem with Firefox browsing if a downloadable file link is found.
        ///   A random link is rejected if it has been recently used, or if a known 'bad' link (ie. 'Learn more' link Firefox security page)
        ///   The browse-probablity value (default 100) is also used, can cause a timelink or random link to be skipped.
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="timelineEvent"></param>
        /// <exception cref="Exception"></exception>
        public void DoRandomAltCommand(TimelineHandler handler, TimelineEvent timelineEvent)
        {
            RequestConfiguration config;
            Dictionary<string, int> urlDict;
            LifoQueue<Uri> urlQueue;


            while (true)
            {
                if (Driver.CurrentWindowHandle == null)
                {
                    throw new Exception("Browser window handle not available");
                }

                config = RequestConfiguration.Load(handler, timelineEvent.CommandArgs[_random.Next(0, timelineEvent.CommandArgs.Count)]);
                
                if (browseprobability < _random.Next(0, 100)) {
                    //skipping this link
                    Log.Trace($"Timeline choice skipped due to browse probability");
                    Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfter,jitterfactor));
                    continue;
                }
                if (config.Uri != null && config.Uri.IsWellFormedOriginalString())
                {
                    urlDict = new Dictionary<string, int>();  //use this to remember visited sites
                    urlQueue = new LifoQueue<Uri>(visitedRemember);
                    MakeRequest(config);
                    Report(handler.HandlerType.ToString(), timelineEvent.Command, config.ToString(), timelineEvent.TrackableId);
                    Thread.Sleep(timelineEvent.DelayAfter);

                    if (this.stickiness > 0)
                    {
                        //now some percentage of the time should stay on this site
                        if (_random.Next(100) < this.stickiness)
                        {
                            var loops = _random.Next(this.depthMin, this.depthMax);
                            Log.Trace($"Beginning {loops} loops on {config.Uri}");
                            for (var loopNumber = 0; loopNumber < loops; loopNumber++)
                            {
                                try
                                {
                                    if (browseprobability > _random.Next(0, 100))
                                    {
                                        if (!ClickRandomLink(config, urlDict, urlQueue)) break;  //break if no links found, reset to next choice
                                        Log.Trace($"Making request #{loopNumber + 1}/{loops} to {config.Uri}");
                                        Report(handler.HandlerType.ToString(), timelineEvent.Command, config.ToString(), timelineEvent.TrackableId);
                                    } else
                                    {
                                        Log.Trace($"Request skipped due to browse probability for #{loopNumber + 1}/{loops} to {config.Uri}");
                                    }
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

                                if (actionsBeforeRestart > 0)
                                {
                                    if (this.actionsCount.IsDivisibleByN(10))
                                    {
                                        Log.Trace($"Browser actions == {this.actionsCount}");
                                    }
                                    if (this.actionsCount > actionsBeforeRestart)
                                    {
                                        this.Restart = true;
                                        Log.Trace("Browser reached action threshold. Restarting...");
                                        return;
                                    }
                                }

                                Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfter, jitterfactor));
                            }
                        }
                    }
                }

                if (actionsBeforeRestart > 0)
                {
                    if (this.actionsCount.IsDivisibleByN(10))
                    {
                        Log.Trace($"Browser actions == {this.actionsCount}");
                    }
                    if (this.actionsCount > actionsBeforeRestart)
                    {
                        this.Restart = true;
                        Log.Trace("Browser reached action threshold. Restarting...");
                        return;
                    }
                }

                Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfter, jitterfactor));


            }
        }




 

        public virtual void HandleBrowserException(Exception e)
        {

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