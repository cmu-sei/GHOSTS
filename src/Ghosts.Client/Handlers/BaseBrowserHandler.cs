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
using AutoItX3Lib;
using static System.Net.WebRequestMethods;
using Newtonsoft.Json;
using System.IO;
using System.Xml.Linq;
using OpenQA.Selenium.Support.UI;
using Microsoft.Office.Interop.Outlook;
using Actions = OpenQA.Selenium.Interactions.Actions;
using Exception = System.Exception;

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

        private int _sharepointDeletionProbability = -1;
        private int _sharepointUploadProbability = -1;
        private int _sharepointDownloadProbability = -1;
        private Credentials _sharepointCredentials = null;
        private bool _sharepointAbort = false;  //will be set to True if unable to proceed with Handler execution
        private string _sharepointState = "initial";
        string _sharepointSite = null;
        string _sharepointUsername = null;
        string _sharepointPassword = null;
        string _sharepointVersion = null;
        string _sharepointUploadDirectory = null;
       


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
                            if (!_sharepointAbort) SharepointExecute(handler, timelineEvent);
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
                                if (config.Uri.IsWellFormedOriginalString())
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


        private bool CheckSharePointProbabilityVar(string name,int value)
        {
            if (!(value >= 0 && value <= 100))
            {
                Log.Trace($"Variable {name} with value {value} must be an int between 0 and 100, setting to 0");
                return false;
            }
            return true;
        }

        /// <summary>
        /// If a sharepoint file upload is not accepted, deal with the error popup
        /// </summary>
        private void handleSharepointUploadBlocked()
        {
            Actions actions;
            try
            {
                
                Driver.SwitchTo().ParentFrame();
                var targetElement = Driver.FindElement(By.XPath("//a[@class='ms-dlgCloseBtn']"));
                actions = new Actions(Driver);
                //close popup
                actions.MoveToElement(targetElement).Click().Perform();
            }
            catch  //ignore any errors, upload may have not been blocked
            {

            }

        }

        private string SharepointGetUploadFile()
        {
            try
            {
                string[] filelist = Directory.GetFiles(_sharepointUploadDirectory, "*");
                if (filelist.Length > 0) return filelist[_random.Next(0, filelist.Length)];
                else return null;
            }
            catch { } //ignore any errors
            return null;
        }

        /// <summary>
        /// This supports only sharepoint site because its remember context between runs. Different handlers should be used for different sites
        /// On the first execution, login is done to the site, then successive runs keep the login.
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="timelineEvent"></param>
        private void SharepointExecute(TimelineHandler handler, TimelineEvent timelineEvent )
        {
            string credFname;
            string credentialKey = null;
            RequestConfiguration config;
            Actions actions;


            switch (_sharepointState) {


                case "initial" :
                    //these are only parsed once, global for the handler as handler can only have one entry.
                    if (handler.HandlerArgs.ContainsKey("sharepoint-version"))
                    {
                        _sharepointVersion = handler.HandlerArgs["sharepoint-version"].ToString();
                        //this needs to be extended in the future
                        if (_sharepointVersion != "2013")
                        {
                            Log.Trace($"Sharepoint:: Unsupported Sharepoint version {_sharepointVersion} , sharepoint browser action will not be executed.");
                            _sharepointAbort = true;
                            return;
                        }
                    }
                    else
                    {
                        Log.Trace($"Sharepoint:: Handler option 'sharepoint-version' must be specified, currently supported versions: '2013'. Sharepoint browser action will not be executed.");
                        _sharepointAbort = true;
                        return;
                    }

                    if (handler.HandlerArgs.ContainsKey("sharepoint-upload-directory"))
                    {
                        string targetDir = handler.HandlerArgs["sharepoint-version"].ToString();
                        targetDir = Environment.ExpandEnvironmentVariables(targetDir);
                        if (!Directory.Exists(targetDir))
                        {
                            Log.Trace($"Sharepoint:: upload directory {targetDir} does not exist, using browser downloads directory.");
                        } else
                        {
                            _sharepointUploadDirectory = targetDir;
                        }
                    }

                    if (_sharepointUploadDirectory == null)
                    {
                        _sharepointUploadDirectory = KnownFolders.GetDownloadFolderPath();
                    }


                    if (_sharepointDeletionProbability < 0 && handler.HandlerArgs.ContainsKey("sharepoint-deletion-probability"))
                    {
                        int.TryParse(handler.HandlerArgs["sharepoint-deletion-probability"].ToString(), out _sharepointDeletionProbability);
                        if (!CheckSharePointProbabilityVar(handler.HandlerArgs["sharepoint-deletion-probability"].ToString(), _sharepointDeletionProbability))
                        {
                            _sharepointDeletionProbability = 0;
                        }
                    }
                    if (_sharepointUploadProbability < 0 && handler.HandlerArgs.ContainsKey("sharepoint-upload-probability"))
                    {
                        int.TryParse(handler.HandlerArgs["sharepoint-upload-probability"].ToString(), out _sharepointUploadProbability);
                        if (!CheckSharePointProbabilityVar(handler.HandlerArgs["sharepoint-upload-probability"].ToString(), _sharepointUploadProbability))
                        {
                            _sharepointUploadProbability = 0;
                        }
                    }
                    if (_sharepointDownloadProbability < 0 && handler.HandlerArgs.ContainsKey("sharepoint-download-probability"))
                    {
                        int.TryParse(handler.HandlerArgs["sharepoint-download-probability"].ToString(), out (_sharepointDownloadProbability));
                        if (!CheckSharePointProbabilityVar(handler.HandlerArgs["sharepoint-download-probability"].ToString(), _sharepointDownloadProbability))
                        {
                            _sharepointDownloadProbability = 0;
                        }
                    }

                    if ((_sharepointDeletionProbability + _sharepointUploadProbability + _sharepointDownloadProbability) > 100)
                    {
                        Log.Trace($"Sharepoint:: The sum of the download/upload/deletion sharepoint probabilities is > 100 , sharepoint browser action will not be executed.");
                        _sharepointAbort = true;
                        return;
                    }

                    if ((_sharepointDeletionProbability + _sharepointUploadProbability + _sharepointDownloadProbability) == 0)
                    {
                        Log.Trace($"Sharepoint:: The sum of the download/upload/deletion sharepoint probabilities == 0 , sharepoint browser action will not be executed.");
                        _sharepointAbort = true;
                        return;
                    }

                    credFname = handler.HandlerArgs["sharepoint-credentials-file"].ToString();

                    if (handler.HandlerArgs.ContainsKey("sharepoint-credentials-file"))
                    {

                        try
                        {
                            _sharepointCredentials = JsonConvert.DeserializeObject<Credentials>(System.IO.File.ReadAllText(credFname));
                        }
                        catch (System.Exception e)
                        {
                            Log.Trace($"Sharepoint:: Error parsing sharepoint credentials file {credFname} , sharepoint browser action will not be executed.");
                            _sharepointAbort = true;
                            Log.Error(e);
                            return;
                        }
                    }

                    //now parse the command args
                    //parse the command args
                    

                    char[] charSeparators = new char[] { ':' };
                    foreach (var cmd in timelineEvent.CommandArgs)
                    {
                        //each argument string is key:value, parse this
                        var argString = cmd.ToString();
                        if (!string.IsNullOrEmpty(argString))
                        {
                            var words = argString.Split(charSeparators, 2, StringSplitOptions.None);
                            if (words.Length == 2)
                            {
                                if (words[0] == "site") _sharepointSite = words[1];
                                else if (words[0] == "credentialKey") credentialKey = words[1];
                            }
                        }
                    }

                    if (_sharepointSite == null)
                    {
                        Log.Trace($"Sharepoint:: The command args must specify a 'site:<value>' , sharepoint browser action will not be executed.");
                        _sharepointAbort = true;
                        return;
                    }

                    if (credentialKey == null)
                    {
                        Log.Trace($"Sharepoint:: The command args must specify a 'credentialKey:<value>' , sharepoint browser action will not be executed.");
                        _sharepointAbort = true;
                        return;
                    }

                    _sharepointUsername = _sharepointCredentials.GetUsername(credentialKey);
                    _sharepointPassword = _sharepointCredentials.GetPassword(credentialKey);

                    if (_sharepointUsername == null || _sharepointPassword == null)
                    {
                        Log.Trace($"Sharepoint:: The credential key {credentialKey} does not return a valid credential from file {credFname},   sharepoint browser action will not be executed");
                        _sharepointAbort = true;
                        return;
                    }
                   

                    //have the username, password
                    string portal = _sharepointSite;
                    string target = "http://" + _sharepointUsername + ":" + _sharepointPassword + "@" + portal + "/";
                    config = RequestConfiguration.Load(handler, target);
                    try
                    {
                        MakeRequest(config);
                    }
                    catch (System.Exception e)
                    {
                        Log.Trace($"Sharepoint:: Unable to parse site {_sharepointSite}, url may be malformed. Sharepoint browser action will not be executed.");
                        _sharepointAbort = true;
                        Log.Error(e);
                        return;

                    }
                    target = "http://" + portal + "/Documents/Forms/Allitems.aspx";
                    config = RequestConfiguration.Load(handler, target);
                    MakeRequest(config);
                    //click on the files tab
                    try
                    {
                        var targetElement = Driver.FindElement(By.Id("Ribbon.Document-title"));
                        targetElement.Click();
                    } 
                    catch (System.Exception e)
                    {
                        Log.Trace($"Sharepoint:: Unable to find Sharepoint menu, login may have failed, check the credentials. Sharepoint browser action will not be executed.");
                        _sharepointAbort = true;
                        Log.Error(e);
                        return;

                    }
                    
                    //at this point we are logged in, files tab selected, ready for action
                    _sharepointState = "execute";
                    break;

                case "execute":

                    //determine what to do
                    int choice = _random.Next(0, 101);
                    string sharepointAction = null;
                    int endRange;
                    int startRange = 0;

                    if (_sharepointDeletionProbability > 0)
                    {
                        endRange = _sharepointDeletionProbability;
                        if (choice >= startRange && choice <= endRange) sharepointAction = "delete";
                        else startRange = _sharepointDeletionProbability + 1;
                    }

                    if (sharepointAction == null && _sharepointUploadProbability > 0)
                    {
                        endRange = startRange + _sharepointUploadProbability;
                        if (choice >= startRange && choice <= endRange) sharepointAction = "upload";
                        else startRange = _sharepointUploadProbability + 1;
                    }

                    if (sharepointAction == null && _sharepointDownloadProbability > 0)
                    {
                        endRange = startRange + _sharepointDownloadProbability;
                        if (choice >= startRange && choice <= endRange) sharepointAction = "download";
                        
                    }
                    if (sharepointAction == null)
                    {
                        //nothing to do this cycle
                        Log.Trace($"Sharepoint:: Action is skipped for this cycle.");
                        return;
                    }

                    if (sharepointAction == "download")
                    {
                        //select a file to download
                        try
                        {
                            var targetElements = Driver.FindElements(By.CssSelector("td[class='ms-cellStyleNonEditable ms-vb-itmcbx ms-vb-imgFirstCell']"));
                            if (targetElements.Count > 0)
                            {
                                int docNum = _random.Next(0, targetElements.Count);
                                actions = new Actions(Driver);
                                actions.MoveToElement(targetElements[docNum]).Click().Perform();

                                Thread.Sleep(1000);
                                //download it
                                var targetElement = Driver.FindElement(By.Id("Ribbon.Documents.Copies.Download-Large"));
                                actions = new Actions(Driver);
                                actions.MoveToElement(targetElement).Click().Perform();
                                Thread.Sleep(1000);
                                //have to click on document element again to deselect it in order to enable next download
                                //targetElements[docNum].Click();  //select the doc
                                actions = new Actions(Driver);
                                actions.MoveToElement(targetElements[docNum]).Click().Perform();
                                Thread.Sleep(1000);
                            }
                        }
                        catch (Exception e)
                        {
                            Log.Trace($"Sharepoint:: Error performing sharepoint download from site {_sharepointSite}.");
                            Log.Error(e);
                        }
                    }
                    if (sharepointAction == "upload")
                    {
                        try
                        {
                            string fname = SharepointGetUploadFile();
                            if (fname == null)
                            {
                                Log.Trace($"Sharepoint:: Cannot find a valid file to upload from directory {_sharepointUploadDirectory}.");
                                return;
                            }
                            var span = new TimeSpan(0, 0, 0, 5, 0);
                            var targetElement = Driver.FindElement(By.Id("Ribbon.Documents.New.AddDocument-Large"));
                            actions = new Actions(Driver);
                            actions.MoveToElement(targetElement).Click().Perform();
                            Thread.Sleep(1000);
                            Driver.SwitchTo().Frame(Driver.FindElement(By.ClassName("ms-dlgFrame")));
                            WebDriverWait wait = new WebDriverWait(Driver, span);                          
                            var uploadElement = Driver.FindElement(By.Id("ctl00_PlaceHolderMain_UploadDocumentSection_ctl05_InputFile"));
                            //uploadElement.SendKeys(@"C:\ghosts_data\uploads\test.bat");
                            uploadElement.SendKeys(@"C:\ghosts_data\uploads\misc.txt");
                            Thread.Sleep(500);
                            var okElement = Driver.FindElement(By.Id("ctl00_PlaceHolderMain_ctl03_RptControls_btnOK"));
                            actions = new Actions(Driver);
                            actions.MoveToElement(okElement).Click().Perform();
                            Thread.Sleep(500);
                            handleSharepointUploadBlocked();
                            Thread.Sleep(500);
                        } 
                        catch (Exception e)
                        {
                            Log.Trace($"Sharepoint:: Error performing sharepoint upload to site {_sharepointSite}.");
                            Log.Error(e);
                        }

                    }
                    if (sharepointAction == "delete")
                    {
                        //select a file to delete
                        try
                        {
                            var targetElements = Driver.FindElements(By.CssSelector("td[class='ms-cellStyleNonEditable ms-vb-itmcbx ms-vb-imgFirstCell']"));
                            if (targetElements.Count > 0)
                            {
                                int docNum = _random.Next(0, targetElements.Count);
                                actions = new Actions(Driver);
                                actions.MoveToElement(targetElements[docNum]).Click().Perform();

                                Thread.Sleep(1000);
                                //delete it
                                //somewhat weird, had to locate this element by the tooltip
                                var targetElement = Driver.FindElement(By.CssSelector("a[aria-describedby='Ribbon.Documents.Manage.Delete_ToolTip'"));
                                actions = new Actions(Driver);
                                //deal with the popup
                                actions.MoveToElement(targetElement).Click().Perform();
                                Thread.Sleep(1000);
                                Driver.SwitchTo().Alert().Accept();
                                Thread.Sleep(1000);
                            } else
                            {
                                Log.Trace($"Sharepoint:: No documents to delete from {_sharepointSite}.");
                            }
                        }
                        catch (Exception e)
                        {
                            Log.Trace($"Sharepoint:: Error performing sharepoint download from site {_sharepointSite}.");
                            Log.Error(e);
                        }
                    }
                    break;




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

        private void MakeRequest(RequestConfiguration config)
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