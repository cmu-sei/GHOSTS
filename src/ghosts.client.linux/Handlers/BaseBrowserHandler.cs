// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ghosts.client.linux.Infrastructure.Browser;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Ghosts.Domain.Code.Helpers;
using OpenQA.Selenium;
using OpenQA.Selenium.Interactions;

namespace ghosts.client.linux.handlers
{
    public abstract class BaseBrowserHandler : BaseHandler
    {
        public IWebDriver Driver { get; set; }
        public IJavaScriptExecutor JS { get; set; }
        public HandlerType BrowserType { get; set; }
        internal bool Restart { get; set; }
        private string Result { get; set; }
        private int _stickiness;
        private int _depthMin = 1;
        private int _depthMax = 10;
        private int _visitedRemember = 5;
        private int _actionsBeforeRestart = -1;
        private LinkManager _linkManager;
        private int _actionsCount = 0;
        public string BrowserProcessTag { get; set; } = null;  //used for killing Linux browser processes

        public int BrowseProbability = 100;
        public int JitterFactor { get; set; } = 0;  //used with Jitter.JitterFactorDelay
        public bool SharePointAbort { get; set; } = false;  //will be set to True if unable to proceed with Handler execution
        public bool BlogAbort { get; set; } = false;  //will be set to True if unable to proceed with Handler execution
        public bool OutlookAbort { get; set; } = false;  //will be set to True if unable to proceed with Handler execution

        public bool SocialAbort { get; set; } = false;  //will be set to True if unable to proceed with Handler execution

        private SharepointHelper _sharePointHelper = null;

        private SocialHelper _socialHelper = null;

        private BlogHelper _blogHelper = null;

        private PostContentManager _postHelper = null;

        private OutlookHelper _outlookHelper = null;

        private static Task LaunchThread(TimelineHandler handler, TimelineEvent timelineEvent, string site)
        {
            var o = new BrowserCrawl();
            return o.Crawl(handler, timelineEvent, site);
        }

        public void ExecuteEvents(TimelineHandler handler)
        {

            //need to reparse this and store it in local instance
            if (handler.HandlerArgs.TryGetValue("browser-id", out var v1) &&
                    !string.IsNullOrEmpty(v1.ToString()))
            {
                var s = v1.ToString();
                BrowserProcessTag = $"{s}";
            }

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
                            if (handler.HandlerArgs.TryGetValue("crawl-tasks-maximum", out var v2))
                            {
                                int.TryParse(v2.ToString(), out taskMax);
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
                                    _outlookHelper = OutlookHelper.MakeHelper(this, Driver, handler, _log);
                                    if (_outlookHelper == null) OutlookAbort = true;
                                }

                                if (_outlookHelper != null)
                                {
                                    _outlookHelper.Execute(handler, timelineEvent);
                                    Restart = _outlookHelper.RestartNeeded();
                                    if (Restart)
                                    {

                                        _log.Trace($"WebOutlook:: Restart requested for {BrowserType} , restarting...");
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
                                    _socialHelper = SocialHelper.MakeHelper(this, Driver, handler, _log);
                                    if (_socialHelper == null) SocialAbort = true;
                                }

                                if (_socialHelper != null)
                                {
                                    _socialHelper.Execute(handler, timelineEvent);
                                    Restart = _socialHelper.RestartNeeded();
                                    if (Restart)
                                    {
                                        _socialHelper = null;  //remove the helper
                                        _log.Trace($"Social:: Restart requested for {BrowserType} , restarting...");
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
                                    _sharePointHelper = SharepointHelper.MakeHelper(this, Driver, handler, _log);
                                    if (_sharePointHelper == null) SharePointAbort = true;
                                }

                                if (_sharePointHelper != null)
                                {
                                    _sharePointHelper.Execute(handler, timelineEvent);
                                    Restart = _sharePointHelper.RestartNeeded();
                                    if (Restart)
                                    {
                                        _sharePointHelper = null;  //remove the helper
                                        _log.Trace($"Sharepoint:: Restart requested for {BrowserType} , restarting...");
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
                                    _blogHelper = BlogHelper.MakeHelper(this, Driver, handler, _log);
                                    if (_blogHelper == null) BlogAbort = true;  //failed to create a helper
                                }
                                _blogHelper?.Execute(handler, timelineEvent);
                            }
                            break;
                        case "random":
                            ParseRandomHandlerArgs(handler);
                            DoRandomCommand(handler, timelineEvent);
                            if (Restart) return;  //restart has been requested
                            break;

                        case "randomalt":
                            ParseRandomHandlerArgs(handler);
                            _postHelper ??= new PostContentManager();
                            DoRandomAltCommand(handler, timelineEvent);
                            if (Restart) return;  //restart has been requested
                            break;


                        case "browse":
                            config = RequestConfiguration.Load(handler, timelineEvent.CommandArgs[0]);
                            if (config.Uri.IsWellFormedOriginalString())
                            {
                                MakeRequest(config);
                                Report(new ReportItem { Handler = handler.HandlerType.ToString(), Command = timelineEvent.Command, Arg = config.ToString(), Trackable = timelineEvent.TrackableId });
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
                    _log.Trace($"Thread aborted, {BrowserType} closing...");
                    throw;
                }
                _log.Error(e);
                if (Restart) throw;
            }
        }

        private void OutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            Result += outLine.Data;
        }

        private static void ErrorHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            //* Do your stuff with the output (write to console/log/StringBuilder)
            Console.WriteLine(outLine.Data);
        }


        /// This is used to kill the browser process since the driver.Quit() method does not always do this
        public void KillBrowser()
        {
            try
            {
                if (BrowserProcessTag == null) return;   //no process tag, don't know which browser(s) to kill

                // this will kill all processes that have the BrowserProcessTag as part of their command string
                var command = $"ps -x -o pid,cmd | grep '{BrowserProcessTag}' | cut -f 2 -d ' ' | xargs kill";

                var p = new Process();
                //p.EnableRaisingEvents = false;
                p.StartInfo.FileName = "bash";
                p.StartInfo.Arguments = $"-c \"{command}\"";
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                //* Set your output and error (asynchronous) handlers
                p.OutputDataReceived += OutputHandler;
                p.ErrorDataReceived += ErrorHandler;
                p.StartInfo.CreateNoWindow = true;
                _log.Trace($"Spawning {p.StartInfo.FileName} with command {command}");
                p.Start();

                while (!p.StandardOutput.EndOfStream)
                {
                    Result += p.StandardOutput.ReadToEnd();
                }

                p.WaitForExit();
            }
            catch (ThreadAbortException)
            {
                throw;  //pass up
            }
            catch (System.Exception e)
            {
                _log.Trace("Exeception while trying to kill browser process.");
                _log.Error(e);
            }
        }

        public void ParseRandomHandlerArgs(TimelineHandler handler)
        {
            if (handler.HandlerArgs.TryGetValue("stickiness", out var v3))
            {
                int.TryParse(v3.ToString(), out _stickiness);
            }
            if (handler.HandlerArgs.TryGetValue("stickiness-depth-min", out var v4))
            {
                int.TryParse(v4.ToString(), out _depthMin);
            }
            if (handler.HandlerArgs.TryGetValue("stickiness-depth-max", out var v5))
            {
                int.TryParse(v5.ToString(), out _depthMax);
            }
            if (handler.HandlerArgs.TryGetValue("visited-remember", out var v6))
            {
                int.TryParse(v6.ToString(), out _visitedRemember);
            }
            if (handler.HandlerArgs.TryGetValue("actions-before-restart", out var v7))
            {
                int.TryParse(v7.ToString(), out _actionsBeforeRestart);
            }

            if (handler.HandlerArgs.TryGetValue("browse-probability", out var v8))
            {
                int.TryParse(v8.ToString(), out BrowseProbability);
                if (BrowseProbability < 0 || BrowseProbability > 100) BrowseProbability = 100;
            }
            if (handler.HandlerArgs.TryGetValue("delay-jitter", out var v9))
            {
                JitterFactor = Jitter.JitterFactorParse(v9.ToString());
            }



        }

        public void DoRandomCommand(TimelineHandler handler, TimelineEvent timelineEvent)
        {
            _linkManager = new LinkManager(_visitedRemember);

            while (true)
            {
                if (Driver.CurrentWindowHandle == null)
                {
                    throw new Exception("Browser window handle not available");
                }

                var config = RequestConfiguration.Load(handler, timelineEvent.CommandArgs[_random.Next(0, timelineEvent.CommandArgs.Count)]);
                if (config.Uri != null && config.Uri.IsWellFormedOriginalString())
                {
                    _linkManager.SetCurrent(config.Uri);
                    MakeRequest(config);
                    Report(new ReportItem { Handler = handler.HandlerType.ToString(), Command = timelineEvent.Command, Arg = config.ToString(), Trackable = timelineEvent.TrackableId });

                    if (_stickiness > 0)
                    {
                        //now some percentage of the time should stay on this site
                        if (_random.Next(100) < _stickiness)
                        {
                            var loops = _random.Next(_depthMin, _depthMax);
                            _log.Trace($"Beginning {loops} loops on {config.Uri}");
                            for (var loopNumber = 0; loopNumber < loops; loopNumber++)
                            {
                                try
                                {
                                    _linkManager.SetCurrent(config.Uri);
                                    GetAllLinks(config, false);
                                    var link = _linkManager.Choose();
                                    if (link == null)
                                    {
                                        return;
                                    }

                                    config.Method = "GET";
                                    config.Uri = link.Url;

                                    _log.Trace($"Making request #{loopNumber + 1}/{loops} to {config.Uri}");
                                    MakeRequest(config);
                                    Report(new ReportItem { Handler = handler.HandlerType.ToString(), Command = timelineEvent.Command, Arg = config.ToString(), Trackable = timelineEvent.TrackableId });
                                }
                                catch (Exception e)
                                {
                                    if (e is ThreadAbortException || e is ThreadInterruptedException)
                                    {
                                        _log.Trace($"Thread aborted, {BrowserType} closing...");
                                        throw;
                                    }
                                    _log.Error($"Browser loop error {e}");
                                }

                                if (_actionsBeforeRestart > 0)
                                {
                                    if (_actionsCount.IsDivisibleByN(10))
                                    {
                                        _log.Trace($"Browser actions == {_actionsCount}");
                                    }
                                    if (_actionsCount > _actionsBeforeRestart)
                                    {
                                        Restart = true;
                                        _log.Trace("Browser reached action threshold. Restarting...");
                                        return;
                                    }
                                }

                                Thread.Sleep(timelineEvent.DelayAfterActual);
                            }
                        }
                    }
                }

                if (_actionsBeforeRestart > 0)
                {
                    if (_actionsCount.IsDivisibleByN(10))
                    {
                        _log.Trace($"Browser actions == {_actionsCount}");
                    }
                    if (_actionsCount > _actionsBeforeRestart)
                    {
                        Restart = true;
                        _log.Trace("Browser reached action threshold. Restarting...");
                        return;
                    }
                }

                Thread.Sleep(timelineEvent.DelayAfterActual);
            }

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
                    _log.Trace($"Timeline choice skipped due to browse probability");
                    Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, JitterFactor));
                    continue;
                }
                if (config.Uri != null && config.Uri.IsWellFormedOriginalString())
                {
                    var urlDict = new Dictionary<string, int>();
                    var urlQueue = new LifoQueue<Uri>(_visitedRemember);
                    MakeRequest(config);
                    Report(new ReportItem { Handler = handler.HandlerType.ToString(), Command = timelineEvent.Command, Arg = config.ToString(), Trackable = timelineEvent.TrackableId });
                    Thread.Sleep(timelineEvent.DelayAfterActual);

                    if (_stickiness > 0)
                    {
                        //now some percentage of the time should stay on this site
                        if (_random.Next(100) < _stickiness)
                        {
                            var loops = _random.Next(_depthMin, _depthMax);
                            _log.Trace($"Beginning {loops} loops on {config.Uri}");
                            for (var loopNumber = 0; loopNumber < loops; loopNumber++)
                            {
                                try
                                {
                                    if (BrowseProbability > _random.Next(0, 100))
                                    {
                                        if (!ClickRandomLink(config, urlDict, urlQueue)) break;  //break if no links found, reset to next choice
                                        _log.Trace($"Making request #{loopNumber + 1}/{loops} to {config.Uri}");
                                        Report(new ReportItem { Handler = handler.HandlerType.ToString(), Command = timelineEvent.Command, Arg = config.ToString(), Trackable = timelineEvent.TrackableId });
                                    }
                                    else
                                    {
                                        _log.Trace($"Request skipped due to browse probability for #{loopNumber + 1}/{loops} to {config.Uri}");
                                    }
                                }
                                catch (Exception e)
                                {
                                    if (e is ThreadAbortException || e is ThreadInterruptedException)
                                    {
                                        _log.Trace($"Thread aborted, {BrowserType} closing...");
                                        throw;
                                    }
                                    _log.Error($"Browser loop error {e}");
                                }


                                if (_actionsBeforeRestart > 0)
                                {
                                    if (_actionsCount.IsDivisibleByN(10))
                                    {
                                        _log.Trace($"Browser actions == {_actionsCount}");
                                    }
                                    if (_actionsCount > _actionsBeforeRestart)
                                    {
                                        Restart = true;
                                        _log.Trace("Browser reached action threshold. Restarting...");
                                        return;
                                    }
                                }

                                Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, JitterFactor));
                            }
                        }
                    }
                }

                if (_actionsBeforeRestart > 0)
                {
                    if (_actionsCount.IsDivisibleByN(10))
                    {
                        _log.Trace($"Browser actions == {_actionsCount}");
                    }
                    if (_actionsCount > _actionsBeforeRestart)
                    {
                        Restart = true;
                        _log.Trace("Browser reached action threshold. Restarting...");
                        return;
                    }
                }

                Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, JitterFactor));
            }
        }

        private static bool isGoodLink(Uri uri)
        {
            //check if this link needs to be rejected
            var linkText = uri.ToString();
            if (linkText.Contains("support.mozilla.org") && linkText.Contains("connection-not-secure"))
            {
                //for some reason, occassionaly the Insecure Cert exception is not thrown and this
                //page about the security issue is not filtered. Do not click this link as it pops
                //up another page. Just reject it, as it is the only link on the page, and we will
                //pop back up to the top and try again
                _log.Trace($"Rejected link {linkText}");
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
                var linkNum = _random.Next(0, uriList.Count);
                var targetUri = uriList[linkNum];
                var targetElement = elementList[linkNum];
                //remember this Url
                if (urlDict.Count == _visitedRemember && _visitedRemember > 0)
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
                _actionsCount++;
                return true;
            }
            catch (Exception e)
            {
                if (e is ThreadAbortException || e is ThreadInterruptedException)
                {
                    throw;
                }
                _log.Trace(e);
                HandleBrowserException(e);
            }
            return true;
        }

        private string GetInputElementText(IWebElement targetElement)
        {
            var attr = targetElement.GetAttribute("type");
            if (attr == "email") return _postHelper.Email;
            else
            {
                attr = targetElement.GetAttribute("id");
                if (attr != null && attr.Contains("name", StringComparison.CurrentCultureIgnoreCase))
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


        public virtual void HandleBrowserException(Exception e)
        {
            // ignore this will be overridden if needed
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
                                _linkManager.AddLink(uri, 1);
                        }
                        // relative links - prefix the scheme and host
                        else
                        {
                            _linkManager.AddLink(uri, 2);
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
                _log.Trace(e);
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

                _actionsCount++;
            }
            catch (Exception e)
            {
                if (e is ThreadAbortException || e is ThreadInterruptedException)
                {
                    throw;
                }
                _log.Trace(e.Message);
            }
        }

        /// <summary>
        /// Close browser
        /// </summary>
        public void Close()
        {
            Report(new ReportItem { Handler = BrowserType.ToString(), Command = "Close" });
            Driver.Close();
        }

        public void Stop()
        {
            Report(new ReportItem { Handler = BrowserType.ToString(), Command = "Stop" });
            Close();
        }
    }
}
