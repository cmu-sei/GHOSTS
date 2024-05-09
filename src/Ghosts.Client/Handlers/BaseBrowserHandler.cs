// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Client.Infrastructure;
using Ghosts.Client.Infrastructure.Browser;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Ghosts.Domain.Code.Helpers;
using OpenQA.Selenium;
using Actions = OpenQA.Selenium.Interactions.Actions;
using System.Collections.Generic;
using System.Linq;
using Remotion.Linq.Parsing.Structure.IntermediateModel;

namespace Ghosts.Client.Handlers
{
    public abstract class BaseBrowserHandler : BaseHandler
    {
        public IWebDriver Driver { get; set; }
        public IJavaScriptExecutor JS { get; set; }
        public HandlerType BrowserType { get; set; }
        internal bool Restart { get; set; }
        public int Stickiness;
        public int DepthMin = 1;
        public int DepthMax = 10;
        public int VisitedRemember = 5;
        public int ActionsBeforeRestart = -1;
        public LinkManager LinkManager;
        public int ActionsCount = 0;
        public int BrowseProbability = 100;
        public int JitterFactor { get; set; } = 0;  //used with Jitter.JitterFactorDelay

        public bool SharePointAbort { get; set; } = false;  //will be set to True if unable to proceed with Handler execution
        public bool OutlookAbort { get; set; } = false;  //will be set to True if unable to proceed with Handler execution
        public bool SocialAbort { get; set; } = false;  //will be set to True if unable to proceed with Handler execution
        public bool BlogAbort { get; set; } = false;  //will be set to True if unable to proceed with Handler execution
        public string UserAgentString { get; set; }

        private SharepointHelper _sharePointHelper = null;
        private BlogHelper _blogHelper = null;
        private PostContentManager _postHelper = null;
        private OutlookHelper _outlookHelper = null;
        private SocialHelper _socialHelper = null;

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
                    WorkingHours.Is(handler);

                    if (timelineEvent.DelayBeforeActual > 0)
                    {
                        Thread.Sleep(timelineEvent.DelayBeforeActual);
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
                        case "outlook":
                            if (!OutlookAbort)
                            {
                                if (_outlookHelper == null)
                                {
                                    _outlookHelper = OutlookHelper.MakeHelper(this, Driver, handler, Log);
                                    if (_outlookHelper == null) OutlookAbort = true;
                                }

                                if (_outlookHelper != null)
                                {
                                    _outlookHelper.Execute(handler, timelineEvent);
                                    this.Restart = _outlookHelper.RestartNeeded();
                                    if (this.Restart)
                                    {

                                        Log.Trace($"WebOutlook:: Restart requested for {this.BrowserType.ToString()} , restarting...");
                                        if (_outlookHelper.LastException != null)
                                        {
                                            throw (_outlookHelper.LastException); //restarts everything
                                        }
                                        else
                                        {
                                            _outlookHelper = null;  //remove the helper, this is soft reset
                                            return;  //restart has been requested 
                                        }
                                    }
                                }
                            }

                            break;
                        case "social":
                            if (!SocialAbort)
                            {
                                if (_socialHelper == null)
                                {
                                    _socialHelper = SocialHelper.MakeHelper(this, Driver, handler, Log);
                                    if (_socialHelper == null) SocialAbort = true;
                                }

                                if (_socialHelper != null)
                                {
                                    _socialHelper.Execute(handler, timelineEvent);
                                    this.Restart = _socialHelper.RestartNeeded();
                                    if (this.Restart)
                                    {
                                        _socialHelper = null;  //remove the helper
                                        Log.Trace($"Social:: Restart requested for {this.BrowserType.ToString()} , restarting...");
                                        // do a hard restart
                                        throw new Exception("Restarting Social Browser");
                                    }
                                }
                            }
                            break;
                        case "sharepoint":
                            if (!SharePointAbort)
                            {
                                if (_sharePointHelper == null)
                                {
                                    _sharePointHelper = SharepointHelper.MakeHelper(this, Driver, handler, Log);
                                    if (_sharePointHelper == null) SharePointAbort = true;
                                }

                                if (_sharePointHelper != null)
                                {
                                    _sharePointHelper.Execute(handler, timelineEvent);
                                    this.Restart = _sharePointHelper.RestartNeeded();
                                    if (this.Restart)
                                    {
                                        _sharePointHelper = null;  //remove the helper
                                        Log.Trace($"Sharepoint:: Restart requested for {this.BrowserType.ToString()} , restarting...");
                                        return;  //restart has been requested 
                                    }
                                }
                            }
                            break;
                        case "blog":
                            if (!BlogAbort)
                            {
                                if (_blogHelper == null)
                                {
                                    _blogHelper = BlogHelper.MakeHelper(this, Driver, handler, Log);
                                    if (_blogHelper == null) BlogAbort = true;  //failed to create a helper
                                }
                                if (_blogHelper != null) _blogHelper.Execute(handler, timelineEvent);
                            }
                            break;
                        case "random":
                            ParseRandomHandlerArgs(handler);
                            DoRandomCommand(handler, timelineEvent);
                            if (this.Restart) return;  //restart has been requested
                            break;

                        case "randomalt":
                            ParseRandomHandlerArgs(handler);
                            if (_postHelper == null) _postHelper = new PostContentManager();
                            DoRandomAltCommand(handler, timelineEvent);
                            if (this.Restart) return;  //restart has been requested
                            break;

                        case "browse":
                            config = RequestConfiguration.Load(handler, timelineEvent.CommandArgs[0]);
                            if (config.Uri.IsWellFormedOriginalString())
                            {
                                var r = FilterWebResponse(MakeRequest(config));
                                Report(new ReportItem { Handler = handler.HandlerType.ToString(), Command = timelineEvent.Command, Arg = config.ToString(), Trackable = timelineEvent.TrackableId, Result = r});
                            }
                            break;
                        case "download":
                            if (timelineEvent.CommandArgs.Count > 0)
                            {
                                element = Driver.FindElement(By.XPath(timelineEvent.CommandArgs[0].ToString()));
                                element.Click();
                                Report(new ReportItem { Handler = handler.HandlerType.ToString(), Command = timelineEvent.Command, Arg = string.Join(",", timelineEvent.CommandArgs), Trackable = timelineEvent.TrackableId });
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

                    if (timelineEvent.DelayAfterActual > 0)
                    {
                        Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, JitterFactor));
                    }
                }
            }
            catch (Exception e)
            {
                if (e is ThreadAbortException || e is ThreadInterruptedException)
                {
                    ProcessManager.KillProcessAndChildrenByName(this.BrowserType.ToString().Replace("Browser", ""));
                    Log.Trace($"Thread aborted, {this.BrowserType.ToString()} closing...");
                    throw;
                }
                Log.Error(e);
            }
        }

        public void ParseRandomHandlerArgs(TimelineHandler handler)
        {
            if (handler.HandlerArgs.ContainsKey("stickiness"))
            {
                int.TryParse(handler.HandlerArgs["stickiness"].ToString(), out Stickiness);
            }
            if (handler.HandlerArgs.ContainsKey("stickiness-depth-min"))
            {
                int.TryParse(handler.HandlerArgs["stickiness-depth-min"].ToString(), out DepthMin);
            }
            if (handler.HandlerArgs.ContainsKey("stickiness-depth-max"))
            {
                int.TryParse(handler.HandlerArgs["stickiness-depth-max"].ToString(), out DepthMax);
            }
            if (handler.HandlerArgs.ContainsKey("visited-remember"))
            {
                int.TryParse(handler.HandlerArgs["visited-remember"].ToString(), out VisitedRemember);
            }
            if (handler.HandlerArgs.ContainsKey("actions-before-restart"))
            {
                int.TryParse(handler.HandlerArgs["actions-before-restart"].ToString(), out ActionsBeforeRestart);
            }
            if (handler.HandlerArgs.ContainsKey("browse-probability"))
            {
                int.TryParse(handler.HandlerArgs["browse-probability"].ToString(), out BrowseProbability);
                if (BrowseProbability < 0 || BrowseProbability > 100) BrowseProbability = 100;
            }
            if (handler.HandlerArgs.ContainsKey("delay-jitter"))
            {
                JitterFactor = Jitter.JitterFactorParse(handler.HandlerArgs["delay-jitter"].ToString());
            }



        }

        public void Upload(TimelineHandler handler, TimelineEvent timelineEvent)
        {
            throw new NotImplementedException();
        }

        public void DoRandomCommand(TimelineHandler handler, TimelineEvent timelineEvent)
        {

            this.LinkManager = new LinkManager(VisitedRemember);

            while (true)
            {
                if (Driver.CurrentWindowHandle == null)
                {
                    throw new Exception("Browser window handle not available");
                }

                var config = RequestConfiguration.Load(handler, timelineEvent.CommandArgs[_random.Next(0, timelineEvent.CommandArgs.Count)]);
                if (config.Uri != null && config.Uri.IsWellFormedOriginalString())
                {
                    this.LinkManager.SetCurrent(config.Uri);
                    var r = FilterWebResponse(MakeRequest(config));
                    Report(new ReportItem { Handler = handler.HandlerType.ToString(), Command = timelineEvent.Command, Arg = config.ToString(), Trackable = timelineEvent.TrackableId, Result = r});
                    Thread.Sleep(timelineEvent.DelayAfterActual);

                    if (this.Stickiness > 0)
                    {
                        //now some percentage of the time should stay on this site
                        if (_random.Next(100) < this.Stickiness)
                        {
                            var loops = _random.Next(this.DepthMin, this.DepthMax);
                            Log.Trace($"Beginning {loops} loops on {config.Uri}");
                            for (var loopNumber = 0; loopNumber < loops; loopNumber++)
                            {
                                try
                                {
                                    this.LinkManager.SetCurrent(config.Uri);
                                    GetAllLinks(config, false);
                                    var link = this.LinkManager.Choose();
                                    if (link == null)
                                    {
                                        return;
                                    }

                                    config.Method = "GET";
                                    config.Uri = link.Url;

                                    Log.Trace($"Making request #{loopNumber + 1}/{loops} to {config.Uri}");
                                    r = FilterWebResponse(MakeRequest(config));
                                    Report(new ReportItem { Handler = handler.HandlerType.ToString(), Command = timelineEvent.Command, Arg = config.ToString(), Trackable = timelineEvent.TrackableId, Result = r});
                                }
                                catch (Exception e)
                                {
                                    if (e is ThreadAbortException || e is ThreadInterruptedException)
                                    {
                                        ProcessManager.KillProcessAndChildrenByName(this.BrowserType.ToString().Replace("Browser", ""));
                                        Log.Trace($"Thread aborted, {this.BrowserType.ToString()} closing...");
                                        throw;
                                    }
                                    Log.Error($"Browser loop error {e}");
                                }

                                if (ActionsBeforeRestart > 0)
                                {
                                    if (this.ActionsCount.IsDivisibleByN(10))
                                    {
                                        Log.Trace($"Browser actions == {this.ActionsCount}");
                                    }
                                    if (this.ActionsCount > ActionsBeforeRestart)
                                    {
                                        this.Restart = true;
                                        Log.Trace("Browser reached action threshold. Restarting...");
                                        return;
                                    }
                                }

                                Thread.Sleep(timelineEvent.DelayAfterActual);
                            }
                        }
                    }
                }

                if (ActionsBeforeRestart > 0)
                {
                    if (this.ActionsCount.IsDivisibleByN(10))
                    {
                        Log.Trace($"Browser actions == {this.ActionsCount}");
                    }
                    if (this.ActionsCount > ActionsBeforeRestart)
                    {
                        this.Restart = true;
                        Log.Trace("Browser reached action threshold. Restarting...");
                        return;
                    }
                }

                Thread.Sleep(timelineEvent.DelayAfterActual);

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
                                this.LinkManager.AddLink(uri, 1);
                        }
                        // relative links - prefix the scheme and host 
                        else
                        {
                            this.LinkManager.AddLink(uri, 2);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                if (e is ThreadAbortException || e is ThreadInterruptedException)
                {
                    throw;
                }
                Log.Trace(e);
            }
        }

        public string MakeRequest(RequestConfiguration config)
        {
            var retVal = string.Empty;
            // Added try here because some versions of FF (v56) throw an exception for an unresolved site,
            // but in other versions it seems to fail gracefully. We want to always fail gracefully
            try
            {
                switch (config.Method.ToUpper())
                {
                    case "GET":
                        Driver.Navigate().GoToUrl(config.Uri);
                        var source = Driver.PageSource.ToLower();
                        if (source.Contains("404 error") || source.Contains("404 not"))
                        {
                            throw new WebDriverException("404");
                        }
                        if (source.Contains("500 internal server"))
                        {
                            throw new WebDriverException("500");
                        }
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
                        script += $"xhr.send('{config.FormValues.ToFormValueString().Replace("'", "").Replace("\"", "")}');";

                        var javaScriptExecutor = (IJavaScriptExecutor)Driver;
                        javaScriptExecutor.ExecuteScript(script);
                        break;
                }

                this.ActionsCount++;
            }
            catch (Exception e)
            {
                if (e is ThreadAbortException || e is ThreadInterruptedException)
                {
                    throw;
                }

                if (e is WebDriverException && (e.Message.Contains("e=dnsNotFound") || e.Message.Contains("404") || e.Message.Contains("ERR_NAME_NOT_RESOLVED")))
                {
                    return "404";
                }

                if (e is WebDriverException && e.Message.Contains("500"))
                {
                    return "500";
                }
                
                Log.Trace(e.Message);
                HandleBrowserException(e);

            }
            return retVal;
        }

        private string GetInputElementText(IWebElement targetElement)
        {
            var attr = targetElement.GetAttribute("type");
            if (attr == "email") return _postHelper.Email;
            else
            {
                attr = targetElement.GetAttribute("id");
                if (attr != null && attr.ToLower().Contains("name"))
                {
                    //assume this is a name field
                    return _postHelper.FullName;
                }
                else
                {
                    return _postHelper.Subject;   //this is a single line of unknown type, not sure what to repond with here
                }
            }
        }

        private void HandleInputElement(IWebElement targetElement)
        {

            var text = GetInputElementText(targetElement);
            if (text != null) targetElement.SendKeys(text);
        }

        private void HandleTextareaElement(IWebElement targetElement)
        {

            targetElement.SendKeys(_postHelper.Body);
        }

        private bool HandleFormSubmit(RequestConfiguration config, IWebElement gfElement)
        {

            var inputElements = gfElement.FindElements(By.XPath(".//input"));
            var textareaElements = gfElement.FindElements(By.XPath(".//textarea"));
            IWebElement submitElement = null;
            _postHelper.NameEmailNext();  //generate a name and email for this page
            _postHelper.GenericContentNext();
            foreach (var inputElement in inputElements)
            {
                var attr = inputElement.GetAttribute("type");
                if (attr == "submit") submitElement = inputElement;
                else
                {
                    HandleInputElement(inputElement);
                }
            }
            foreach (var textareaElement in textareaElements)
            {
                HandleTextareaElement(textareaElement);
                Thread.Sleep(2000);
            }
            if (submitElement != null)
            {
                BrowserHelperSupport.MoveToElementAndClick(Driver, submitElement);
                Thread.Sleep(3000);
                return true;
            }
            return false;
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
                var gfElements = Driver.FindElements(By.TagName("gf"));
                if (gfElements.Count > 0)
                {
                    //found a submit form. Fill this out. If more than one, pick a random one
                    if (HandleFormSubmit(config, gfElements[_random.Next(0, gfElements.Count)])) return true;
                }
                //look for links
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
                if (urlDict.Count == VisitedRemember && VisitedRemember > 0)
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
                this.ActionsCount++;
                return true;
            }
            catch (Exception e)
            {
                if (e is ThreadAbortException || e is ThreadInterruptedException)
                {
                    throw;
                }
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
            while (true)
            {
                if (Driver.CurrentWindowHandle == null)
                {
                    throw new Exception("Browser window handle not available");
                }

                var config = RequestConfiguration.Load(handler, timelineEvent.CommandArgs[_random.Next(0, timelineEvent.CommandArgs.Count)]);

                if (BrowseProbability < _random.Next(0, 100))
                {
                    //skipping this link
                    Log.Trace($"Timeline choice skipped due to browse probability");
                    Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, JitterFactor));
                    continue;
                }
                if (config.Uri != null && config.Uri.IsWellFormedOriginalString())
                {
                    var urlDict = new Dictionary<string, int>();
                    var urlQueue = new LifoQueue<Uri>(VisitedRemember);
                    var r = FilterWebResponse(MakeRequest(config));
                    Report(new ReportItem { Handler = handler.HandlerType.ToString(), Command = timelineEvent.Command, Arg = config.ToString(), Trackable = timelineEvent.TrackableId, Result = r});
                    Thread.Sleep(timelineEvent.DelayAfterActual);

                    if (this.Stickiness > 0)
                    {
                        //now some percentage of the time should stay on this site
                        if (_random.Next(100) < this.Stickiness)
                        {
                            var loops = _random.Next(this.DepthMin, this.DepthMax);
                            Log.Trace($"Beginning {loops} loops on {config.Uri}");
                            for (var loopNumber = 0; loopNumber < loops; loopNumber++)
                            {
                                try
                                {
                                    if (BrowseProbability > _random.Next(0, 100))
                                    {
                                        if (!ClickRandomLink(config, urlDict, urlQueue)) break;  //break if no links found, reset to next choice
                                        Log.Trace($"Making request #{loopNumber + 1}/{loops} to {config.Uri}");
                                        Report(new ReportItem { Handler = handler.HandlerType.ToString(), Command = timelineEvent.Command, Arg = config.ToString(), Trackable = timelineEvent.TrackableId });
                                    }
                                    else
                                    {
                                        Log.Trace($"Request skipped due to browse probability for #{loopNumber + 1}/{loops} to {config.Uri}");
                                    }
                                }
                                catch (Exception e)
                                {
                                    if (e is ThreadAbortException || e is ThreadInterruptedException)
                                    {
                                        ProcessManager.KillProcessAndChildrenByName(this.BrowserType.ToString().Replace("Browser", ""));
                                        Log.Trace($"Thread aborted, {this.BrowserType.ToString()} closing...");
                                        throw;
                                    }
                                    Log.Error($"Browser loop error {e}");
                                }

                                if (ActionsBeforeRestart > 0)
                                {
                                    if (this.ActionsCount.IsDivisibleByN(10))
                                    {
                                        Log.Trace($"Browser actions == {this.ActionsCount}");
                                    }
                                    if (this.ActionsCount > ActionsBeforeRestart)
                                    {
                                        this.Restart = true;
                                        Log.Trace("Browser reached action threshold. Restarting...");
                                        return;
                                    }
                                }

                                Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, JitterFactor));
                            }
                        }
                    }
                }

                if (ActionsBeforeRestart > 0)
                {
                    if (this.ActionsCount.IsDivisibleByN(10))
                    {
                        Log.Trace($"Browser actions == {this.ActionsCount}");
                    }
                    if (this.ActionsCount > ActionsBeforeRestart)
                    {
                        this.Restart = true;
                        Log.Trace("Browser reached action threshold. Restarting...");
                        return;
                    }
                }

                Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, JitterFactor));
            }
        }

        public virtual void HandleBrowserException(Exception e)
        {
            // ignore
        }

        /// <summary>
        /// Close browser entirely
        /// </summary>
        public void Close()
        {
            Report(new ReportItem { Handler = BrowserType.ToString(), Command = "Close" });
            Driver.Close();
        }

        public string FilterWebResponse(string rawResponse)
        {
            string[] s = { "404", "500" };
            return s.Contains(rawResponse) ? rawResponse : "";
        }
    }
}
