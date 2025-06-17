using Ghosts.Domain;
using Ghosts.Domain.Code;
using Newtonsoft.Json;
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using Actions = OpenQA.Selenium.Interactions.Actions;
using Exception = System.Exception;
using NLog;
using System.Web;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;
using Ghosts.Client.Infrastructure;
using System.Web.UI.WebControls;



namespace Ghosts.Client.Handlers
{

   
    /// <summary>
    /// Supports upload, download, deletion of documents
    /// download, deletion only done from the first page
    /// Tested with Social 2013 and 2019
    /// 2019 uses the 'classic view' for 2013 compatibility
    /// </summary>
    public class SocialHelperV1 : SocialHelper
    {

        public SocialHelperV1 (BaseBrowserHandler callingHandler, IWebDriver callingDriver, string aversion)
        {
            base.Init(callingHandler, callingDriver, aversion);

        }


        public override bool DoInitialLogin(TimelineHandler handler)
        {

            postCount = 0;   //reset post count

            if (!GotoHomeSite(handler)) {
                return false;
            }
            
            // try to find an element on the page
            bool foundSocializer = false;
            try
            {
                var targetElement =  Driver.FindElement(By.XPath("//img[@title='SOCIALIZER']"));
                foundSocializer = true;
            }
            catch (ThreadAbortException)
            {
                throw;  //pass up
            }
            return foundSocializer;
        }


        
        public override bool DoBrowse(TimelineHandler handler)
        {
            // browse to the first friend suggestion in the friend feed
            // var targetElement =  Driver.FindElement(By.XPath("//ul[contains(@class,'w-friend-pages-added notification-list')]//child::div[contains(@class,'notification-event')]//child::a[contains(@class,'notification-friend')]"));
            // browse to the first person of first post in feed
            var targetElement =  Driver.FindElement(By.XPath("//div[contains(@class,'author-date')]//child::a[contains(@class,'post__author-name')]"));
            if (targetElement != null){
                BrowserHelperSupport.ElementClick(Driver, targetElement);
                Thread.Sleep(500);
                Log.Trace($"Social:: Successfully browsed post on site {site}.");
            }
            return true;
        }

        public override bool DoLike(TimelineHandler handler)
        {
            // just like the first post in the feed
            var targetElement =  Driver.FindElement(By.XPath("//a[contains(@class,'btn btn-control like-it')]"));
            if (targetElement != null){
                BrowserHelperSupport.ElementClick(Driver, targetElement);
                Thread.Sleep(500);
                Log.Trace($"Social:: Successfully liked post on site {site}.");
            }
            return true;
        }

        public override bool DoPost(TimelineHandler handler, string action)
        {

            bool doImageUpload = (action == "postWimage");
            string postDirectory = GetPostDirectory();
            if (postDirectory == null) return false;

            string[] postFileList = Directory.GetFiles(postDirectory, "post.txt");
            if (postFileList.Length > 0) {
                // get the file content
                string postContent = File.ReadAllText(postFileList[0]);
                var targetElement =  Driver.FindElement(By.XPath("//label[text()='Share what you are thinking here...']//following-sibling::textarea"));
                targetElement.SendKeys(postContent);
                Thread.Sleep(500);
                string targetName = "";
                if (userName != null)  targetName = userName;  //always use this if specified
                else {
                    if (lastUserName == null || useUniqueName) {
                        lastUserName = findUserName();
                    }
                    targetName = lastUserName;
                }
                // post target Name
                targetElement =  Driver.FindElement(By.XPath("//label[text()='Share what you are thinking here...']//following-sibling::input"));
                targetElement.Clear(); //clear the name before sending another one
                targetElement.SendKeys(targetName);
                Thread.Sleep(500);
                if (action == "postWimage"){
                    // get the image file
                    string[] imageFilesPng = Directory.GetFiles(postDirectory, "image*.png");
                    string[] imageFilesJpg = Directory.GetFiles(postDirectory, "image*.jpg");
                    
                    if ((imageFilesPng.Length + imageFilesJpg.Length) > 0){
                        
                        string imageFile = null;
                        if (imageFilesPng.Length > 0 && imageFilesJpg.Length > 0)
                        {
                            int total = imageFilesPng.Length + imageFilesJpg.Length;
                            int index = _random.Next(0, total);
                            if (index >= imageFilesPng.Length)
                            {
                                imageFile = imageFilesJpg[index - imageFilesPng.Length];
                            } else
                            {
                                imageFile = imageFilesPng[index];
                            }

                        } else if (imageFilesJpg.Length > 0)
                        {
                            imageFile = imageFilesJpg[(_random.Next(0, imageFilesJpg.Length))];
                        }
                        else
                        {
                            imageFile = imageFilesPng[(_random.Next(0, imageFilesPng.Length))];
                        }
                        // click the browse button
                        targetElement =  Driver.FindElement(By.XPath("//label[text()='Share what you are thinking here...']//following-sibling::input[@type='file']"));
                        if (targetElement != null)
                        {
                            BrowserHelperSupport.ElementClick(Driver, targetElement);
                            Thread.Sleep(500);
                            //filechoice window is open
                            AttachFile(imageFile );
                            Thread.Sleep(500);
                        }
                    }

                }



                targetElement =  Driver.FindElement(By.XPath("//button[@id='sendButton']"));
                Actions actions = new Actions(Driver);
                actions.MoveToElement(targetElement).Click().Perform();
                Thread.Sleep(500);
                Log.Trace($"Social:: Successfully added post to site {site}.");
                postCount += 1;
                

            }



            return true;
        }

       

    }

    /// <summary>
    /// Handles Social actions for base browser handler
    /// </summary>
    public abstract class SocialHelper : BrowserHelper
    {

        
        private int _postProbability = -1;
        private int _likeProbability = -1;
        private int _browseProbability = -1;
        private int _addImageProbability = -1;
        public string userName { get; set; } = null;
        public string[] topicList { get; set; } = null;

        public int postCount { get; set; } = 0;

        public System.Exception LastException;

        public List<string> topicDirs = null;
        
        private string _state = "initial";
        public int errorCount = 0;
        public int errorThreshold = 3;  //after three strikes, restart the browser
        public string site { get; set; } = null;
        
        public string header { get; set; } = null;

        public string version { get; set; } = null;
        public string contentDirectory { get; set; } = null;

        public string lastUserName { get; set; } = null;

        public bool useUniqueName { get; set; } = true;

        public string AttachmentWindowTitle = "Open"; //this is for chrome

        private void OutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            Log.Trace($"Social:: STDOUT from bash process: {outLine.Data}");
            return;
        }

        private static void ErrorHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            Log.Trace($"Social:: STDERR output from bash process: {outLine.Data}");
            return;
        }

        public string findUserName() {

            var targetElement =  Driver.FindElement(By.XPath("//ul[contains(@class,'w-friend-pages-added notification-list')]//child::div[contains(@class,'notification-event')]//child::a[contains(@class,'notification-friend')]"));
            
            if (targetElement != null) {
                var name = targetElement.Text;
                return name;
            }
            Log.Trace($"Social:: Unable to find user name to use for post, using default name.");
            return "Dr.Mysterious";  // always return a name
        }


        public static SocialHelper MakeHelper(BaseBrowserHandler callingHandler, IWebDriver callingDriver, TimelineHandler handler, Logger tlog)
        {
            SocialHelper helper = new SocialHelperV1(callingHandler, callingDriver, "1.0");
            return helper;
        }

        public bool RestartNeeded()
        {
            return errorCount > errorThreshold;
        }
        

        public void Init(BaseBrowserHandler callingHandler, IWebDriver currentDriver, string aversion)
        {
            baseHandler = callingHandler;
            Driver = currentDriver;
            version = aversion;
        }

        private bool CheckProbabilityVar(string name, int value)
        {
            if (!(value >= 0 && value <= 100))
            {
                Log.Trace($"Variable {name} with value {value} must be an int between 0 and 100, setting to 0");
                return false;
            }
            return true;
        }

        public static bool isWindowsOs()
        {
            var OsName = System.Runtime.InteropServices.RuntimeInformation.OSDescription;

            return OsName.Contains("Windows");
        }

        private void ExecuteBashCommand(string command)
        {
            var escapedArgs = command.Replace("\"", "\\\"");


            var p = new Process();
            //p.EnableRaisingEvents = false;
            p.StartInfo.FileName = "bash";
            p.StartInfo.Arguments = $"-c \"{escapedArgs}\"";
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            //* Set your output and error (asynchronous) handlers
            p.OutputDataReceived += OutputHandler;
            p.ErrorDataReceived += ErrorHandler;
            p.StartInfo.CreateNoWindow = true;
            Log.Trace($"Social:: Spawning {p.StartInfo.FileName} with command {escapedArgs}");
            p.Start();

            string Result = "";
            while (!p.StandardOutput.EndOfStream)
            {
                Result += p.StandardOutput.ReadToEnd();
            }

            p.WaitForExit();
            Log.Trace($"Social:: Bash command output: {Result}");
        }

         public bool GotoHomeSite(TimelineHandler handler)
        {
            //go to the site and determine if this is a socializer site
            RequestConfiguration config;

            string portal = site;

            string target = header +  portal + "/";
            config = RequestConfiguration.Load(handler, target);
            try
            {
                baseHandler.MakeRequest(config);
            }
            catch (ThreadAbortException)
            {
                throw;  //pass up
            }
            catch (System.Exception e)
            {
                Log.Trace($"Social:: Unable to parse site {site}, url may be malformed. Social browser action will not be executed.");
                Log.Error(e);
                return false;

            }
            return true;
        }


        public void AttachFileWindows(string filename)
        {
            IntPtr winHandle = Winuser.FindWindow(null, AttachmentWindowTitle);
            if (winHandle == IntPtr.Zero)
            {
                Log.Trace($"WebOutlook:: Unable to find '{AttachmentWindowTitle}' window to upload file attachment.");
                return;
            }
            Winuser.SetForegroundWindow(winHandle);
            string s;
            if (Driver is OpenQA.Selenium.Firefox.FirefoxDriver)
            {
                s = filename + "{TAB}{TAB}{ENTER}";
            }
            else
            {
                s = filename + "{ENTER}";
            }

            System.Windows.Forms.SendKeys.SendWait(s);
            Thread.Sleep(200);
            winHandle = Winuser.FindWindow(null, AttachmentWindowTitle);
            if (winHandle == IntPtr.Zero)
            {
                return;
            }
            // the window is still open. Grr. try closing it.
            Winuser.SetForegroundWindow(winHandle);
            System.Windows.Forms.SendKeys.SendWait("%{F4}");

        }


        public void AttachFile(string filename)
        {
            AttachFileWindows(filename);
        }



        public string GetUploadFile()
        {
            try
            {
                string[] filelist = Directory.GetFiles(contentDirectory, "*");
                if (filelist.Length > 0) return filelist[_random.Next(0, filelist.Length)];
                else return null;
            }
            catch (ThreadAbortException)
            {
                throw;  //pass up
            }
            catch { } //ignore any errors
            return null;
        }


        public virtual bool DoInitialLogin(TimelineHandler handler)
        {
            Log.Trace($"Social:: Unsupported action 'DoInitialLogin' in Social version {version} ");
            return false;
        }

        public virtual bool DoPost(TimelineHandler handler, string action)
        {
            Log.Trace($"Social:: Unsupported action {action} in Social version {version} ");
            return false;
        }

        public virtual bool DoLike(TimelineHandler handler)
        {
            Log.Trace($"Social:: Unsupported action: like in Social version {version} ");
            return false;
        }

        public virtual bool DoBrowse(TimelineHandler handler)
        {
            Log.Trace($"Social:: Unsupported action: like in Social version {version} ");
            return false;
        }

        public string GetPostDirectory()
        {
            try
            {
                
                if (topicDirs != null && topicDirs.Count > 0) {
                    //ensure these topic dirs still exists
                    List<string> dirlist = new List<string>();
                    foreach (string topicDir in topicDirs){
                        if (Directory.Exists(topicDir)) dirlist.Add(topicDir);
                    }
                
                    if (dirlist.Count > 0) {
                        //this will be the topic directory
                        string topicDir = dirlist[_random.Next(0, dirlist.Count)];
                        // get the post directory
                        string[] topicContentDirList = Directory.GetDirectories(topicDir, "*", SearchOption.TopDirectoryOnly);
                        if (topicContentDirList.Length > 0) {
                            string topicContentDir = topicContentDirList[_random.Next(0, topicContentDirList.Length)];
                            //get the post file
                            return topicContentDir;
                        }
                    }
                }
                else return null;
            }
            catch (ThreadAbortException)
            {
                throw;  //pass up
            }
            return null;
        }



        private string GetNextAction()
        {
            
            var choice = _random.Next(0, 101);
            string action = null;
            int endRange;
            var startRange = 0;
            
            if (postCount == 0) {
                // do at least one post so user can be set
                if (_addImageProbability > _random.Next(0, 100)) action = "postWimage";
                else action = "post";
                return action;
            }
            if (_likeProbability > 0)
            {
                endRange = _likeProbability;
                if (choice >= startRange && choice <= endRange) action = "like";
                else startRange = endRange + 1;
            }

            if (action == null && _browseProbability > 0)
            {
                endRange = startRange + _browseProbability;
                if (choice >= startRange && choice <= endRange) action = "browse";
                else startRange = endRange + 1;
            }

            if (action == null && _postProbability > 0)
            {
                endRange = startRange + _postProbability;
                if (choice >= startRange && choice <= endRange) {
                    if (_addImageProbability > _random.Next(0, 100)) action = "postWimage";
                    else action = "post";
                }
                else startRange = endRange + 1;
            }
            
            
            return action;

        }

        /// <summary>
        /// This supports only one social site because it remembers context between runs. Different handlers should be used for different sites
        /// On the first execution, login is done to the site, then successive runs keep the login.
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="timelineEvent"></param>
        public void Execute(TimelineHandler handler, TimelineEvent timelineEvent)
        {
            try {
            
                switch (_state)
                {


                    case "initial":
                        //these are only parsed once, global for the handler as handler can only have one entry.
                        version = handler.HandlerArgs["social-version"].ToString();  //guaranteed to have this option, parsed in calling handler

                        if (handler.HandlerArgs.ContainsKey("social-username"))
                        {
                            userName = handler.HandlerArgs["social-username"].ToString();
                        }

                        if (handler.HandlerArgs.ContainsKey("social-use-unique-user"))
                        {
                            useUniqueName = handler.HandlerArgs["social-use-unique-user"].ToString().ToLower() == "true";
                        }

                        if (handler.HandlerArgs.ContainsKey("social-content-directory"))
                        {
                            string targetDir = handler.HandlerArgs["social-content-directory"].ToString();
                            targetDir = Environment.ExpandEnvironmentVariables(targetDir);
                            if (!Directory.Exists(targetDir))
                            {
                                Log.Trace($"Social:: contentdirectory {targetDir} does not exist, aborting social handler.");
                                baseHandler.SocialAbort = true;
                            }
                            else
                            {
                                contentDirectory = targetDir;
                            }

                        }

                        string topics = null;
                        if (handler.HandlerArgs.ContainsKey("social-topiclist")) {
                            // will be used to prune topic list directories
                            topics = handler.HandlerArgs["social-topiclist"].ToString();
                            topics = topics.ToLower();
                        }
                        if (contentDirectory != null) {
                            // create list of valid topic dirs
                            topicDirs =  new List<string>();
                            string[] dirlist = Directory.GetDirectories(contentDirectory, "*", SearchOption.TopDirectoryOnly);
                            if (dirlist.Length > 0) {
                                //get the base directory
                                foreach (var dir in dirlist) {
                                    if (topics == null) {
                                        topicDirs.Add(dir);
                                    } else {
                                        var f = Path.GetFileName(dir);
                                        if (topics.ToLower().Contains(f)) {
                                            topicDirs.Add(dir);
                                        }
                                    }
                                }
                                if (topicDirs.Count == 0 && topics != null){
                                    // No match to specified topics, add all available
                                    foreach (var dir in dirlist) {
                                        topicDirs.Add(dir);
                                    }
                                }
                            } else {
                                // no topic dirs, abort
                                Log.Trace($"Social:: contentdirectory {contentDirectory} does not have topic subdirectories, aborting, aborting social handler.");
                                baseHandler.SocialAbort = true;
                            }
                        }
        
                        if (handler.HandlerArgs.ContainsKey("social-post-probability"))
                        {
                            int.TryParse(handler.HandlerArgs["social-post-probability"].ToString(), out _postProbability);
                            if (!CheckProbabilityVar(handler.HandlerArgs["social-post-probability"].ToString(), _postProbability))
                            {
                                _postProbability = 0;
                            }
                        }
                        if (handler.HandlerArgs.ContainsKey("social-like-probability"))
                        {
                            int.TryParse(handler.HandlerArgs["social-like-probability"].ToString(), out _likeProbability);
                            if (!CheckProbabilityVar(handler.HandlerArgs["social-like-probability"].ToString(), _likeProbability))
                            {
                                _likeProbability = 0;
                            }
                        }
                        if (handler.HandlerArgs.ContainsKey("social-browse-probability"))
                        {
                            int.TryParse(handler.HandlerArgs["social-browse-probability"].ToString(), out _browseProbability);
                            if (!CheckProbabilityVar(handler.HandlerArgs["social-browse-probability"].ToString(), _browseProbability))
                            {
                                _browseProbability = 0;
                            }
                        }
                        if (_addImageProbability < 0 && handler.HandlerArgs.ContainsKey("social-addimage-probability"))
                        {
                            int.TryParse(handler.HandlerArgs["social-addimage-probability"].ToString(), out _addImageProbability);
                            if (!CheckProbabilityVar(handler.HandlerArgs["social-addimage-probability"].ToString(), _addImageProbability))
                            {
                                _addImageProbability = 0;
                            }
                        }
                        
                        if (handler.HandlerArgs.ContainsKey("delay-jitter"))
                        {
                            baseHandler.JitterFactor = Jitter.JitterFactorParse(handler.HandlerArgs["delay-jitter"].ToString());
                        }

                        if (handler.HandlerArgs.ContainsKey("social-username"))
                        {
                            userName = handler.HandlerArgs["social-username"].ToString();
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
                                    if (words[0] == "site") site = words[1];
                                }
                            }
                        }

                        if (site == null)
                        {
                            Log.Trace($"Social:: The command args must specify a 'site:<value>' , social browser action will not be executed.");
                            baseHandler.SocialAbort = true;
                            return;
                        }

                        //check if site starts with http:// or https:// 
                        site = site.ToLower();
                        header = null;
                        Regex rx = new Regex("^http://.*", RegexOptions.Compiled);
                        var match = rx.Matches(site);
                        if (match.Count > 0) header = "http://";
                        if (header == null)
                        {
                            rx = new Regex("^https://.*", RegexOptions.Compiled);
                            match = rx.Matches(site);
                            if (match.Count > 0) header = "https://";
                        }
                        if (header != null)
                        {
                            site = site.Replace(header, "");
                        }
                        else
                        {
                            header = "http://";  //default header
                        }

                        if (!DoInitialLogin(handler)) {
                            Log.Trace($"Social:: Target site {site} does not appear to be a socializer site, aborting Social browsing.");
                            baseHandler.SocialAbort = true;
                            return;
                        }

                        if (Driver is OpenQA.Selenium.Firefox.FirefoxDriver)
                        {
                            AttachmentWindowTitle = "File Upload";
                        }

                        //at this point we are logged in, files tab selected, ready for action
                        _state = "execute";
                        break;

                    case "execute":

                        //determine what to do
                        //first go back to home site
                        GotoHomeSite(handler);  
                        Thread.Sleep(500);

                        string socialAction = GetNextAction();

                        if (socialAction == "post" || socialAction == "postWimage"){
                            if (!DoPost(handler,socialAction))
                            {
                                baseHandler.SocialAbort = true;
                                return;
                            }
                        }
                        else if (socialAction == "like") {
                            if (!DoLike(handler))
                            {
                                baseHandler.SocialAbort = true;
                                return;
                            }
                        }
                        else if (socialAction == "browse") {
                            if (!DoBrowse(handler))
                            {
                                baseHandler.SocialAbort = true;
                                return;
                            }
                        }


                        this.baseHandler.Report(new ReportItem {Handler = $"Social{version}: {handler.HandlerType.ToString()}", Command = socialAction, Arg = "", Trackable = timelineEvent.TrackableId});
                        break;


                }

            }
            catch (Exception e)
            {
                if (e is ThreadAbortException || e is ThreadInterruptedException)
                {
                    throw;
                }
                errorCount = errorThreshold + 1;  // an exception at  this level needs a restart
                LastException = e;  //save last exception so that it can be thrown up during restart
                Log.Trace($"WebSocial:: Error at top level of execute loop.");
                Log.Error(e);
                Thread.Sleep(20000);   // sleep to prevent tight error loop
            }
        }

    }


    }

    
    