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
    /// Handles Social actions for base browser handler
    /// Updated for Pandora V9
    /// </summary>
    public class SocialHelperV1 : SocialHelper
    {

        public SocialHelperV1 (BaseBrowserHandler callingHandler, IWebDriver callingDriver, string aversion)
        {
            base.Init(callingHandler, callingDriver, aversion);

        }

        private bool IsSupportedTheme(string aTheme)
        {
        
        bool isSupported = Array.Exists(supportedThemes, element => element == aTheme);
        return isSupported; 
        }

        private void GetTopicDirectories(string aTheme)
        {
            if (contentDirectory != null)
            {
                var themeDirectory = Path.Combine(contentDirectory, aTheme);
                // create list of valid topic dirs
                topicDirs = new List<string>();
                var dirlist =
                    Directory.GetDirectories(themeDirectory, "*", SearchOption.TopDirectoryOnly);
                if (dirlist.Length > 0)
                {
                    //get the base directory
                    foreach (var dir in dirlist)
                    {
                        if (topics == null)
                        {
                            topicDirs.Add(dir);
                        }
                        else
                        {
                            var f = Path.GetFileName(dir);
                            if (topics.Contains(f, StringComparison.CurrentCultureIgnoreCase))
                            {
                                topicDirs.Add(dir);
                            }
                        }
                    }

                    if (topicDirs.Count == 0 && topics != null)
                    {
                        // No match to specified topics, add all available
                        foreach (var dir in dirlist)
                        {
                            topicDirs.Add(dir);
                        }
                    }
                }
            }
        }


        public override bool DoInitialLogin(TimelineHandler handler)
        {

            if (!GotoHomeSite(handler)) {
                return false;
            }

            if (siteToTheme != null && siteToTheme.ContainsKey(site))
            {
                // already discovered the theme
                theme = siteToTheme[site];  
                return true;
            }

            // try to find an element on the page
            theme = null;
            
            // Try looking for PandoraV9 theme other than default
            string themeValue = null;
            try
            {
                
                var targetElement = Driver.FindElement(By.XPath("//head//child::link[@rel='stylesheet']"));
                themeValue = targetElement.GetAttribute("href");
                string[] words = themeValue.Split('/');
                if (words.Length > 3)
                {
                    // check if supported theme
                    themeValue = words[words.Length-3];
                    if (IsSupportedTheme(themeValue)) {
                        theme = themeValue;
                    }
                }
            }
            catch (ThreadAbortException ex)
            {
                throw;  //pass up
            }
            catch (Exception ex)
            {
                //ignore
            }

            if (theme == null)
            {
                try {
                    // try another way to get the theme for Pandora V9
                    var targetElement = Driver.FindElement(By.XPath("//body[@data-theme]"));
                    themeValue = targetElement.GetAttribute("data-theme");
                    if (IsSupportedTheme(themeValue)) {
                        theme = themeValue;
                    }
                }
                catch (ThreadAbortException ex)
                {
                    throw;  //pass up
                }
                catch (Exception ex)
                {
                    //ignore
                }
            }
            if (theme == null)
            {
                // Check for Pandora V9 default theme
                try
                {
                    var targetElement = Driver.FindElement(By.XPath("//img[@title='PANDORA']"));
                    theme = "default";
                }
                catch (ThreadAbortException ex)
                {
                    throw;  //pass up
                }
                catch (Exception ex)
                {
                    //ignore
                }
            }
            if (theme == null) 
            {
                // Check for old Socializer 
                try
                {
                    var targetElement = Driver.FindElement(By.XPath("//img[@title='SOCIALIZER']"));
                    theme = "default";
                }
                catch (ThreadAbortException ex)
                {
                    throw;  //pass up
                }
                catch (Exception ex)
                {
                    //ignore
                }
            }
            if (theme == null) 
            {
                // Check if we have a web error or some kind
                try
                {
                    var targetElement = Driver.FindElement(By.XPath("//head//child::title"));
                    if (targetElement.Text.Contains("temporarily unavailable", StringComparison.OrdinalIgnoreCase))
                    {
                        // web error. Leave theme as null, return true, and try again later
                        Log.Trace(
                            $"Social: Web error -- Site {site} is temporarily unavailable, trying again later.");
                        return true;
                    }

                }
                catch (ThreadAbortException ex)
                {
                    throw;  //pass up
                }
                catch (Exception ex)
                {
                    // if we get here, we have no theme and no web error, so we will log the error and return false
                    if (themeValue == null) {
                    Log.Trace(
                        $"Social:: Unable to verify that site {site} is a Pandora/Socializer site, url may be malformed. Social browser action will not be executed.");
                    Log.Error(ex);
                    } else if (theme == null)
                    {
                        Log.Trace(
                            $"Social:: Unable to verify that site {site} is has a supported Pandora/Socializer theme site, url may be malformed. Social browser action will not be executed.");
                        Log.Error(ex);
                    }
                    return false;
                    }
            }


            if (theme != null)
            {
                if (siteToTheme == null)
                {
                    siteToTheme = new Dictionary<string, string>();
                }
                if (!siteToTheme.ContainsKey(site))
                {
                    siteToTheme.Add(site,theme);
                }
                if (themeToPostcount == null)
                {
                themeToPostcount = new Dictionary<string, int>();
                }
                if (!themeToPostcount.ContainsKey(theme))
                {
                    themeToPostcount.Add(theme,0);
                }
                GetTopicDirectories(theme);
            }

            if (theme != null)
            {
                Log.Trace($"Social:: Verified that site {site} is a Pandora/Socializer site with theme {theme}.");
            }

            return theme != null;
        }

        public string GetThemeAction(string aTheme, string action)
        {
            if (theme == null) return null;
            if (!xPathByTheme.ContainsKey(aTheme)) return null;
            var themeDictionary = xPathByTheme[aTheme];
            if (!themeDictionary.ContainsKey(action)) return null;
            return themeDictionary[action];
        }

        public string findUserName()
        {
            string targetXpath = GetThemeAction(theme, "Username");
            IWebElement targetElement;
            if (targetXpath != null) {
                targetElement = Driver.FindElement(By.XPath(targetXpath));
                if (theme == "default") {
                    var name = targetElement.Text;
                    return name;
                } else if (theme == "facebook" || theme == "instagram")
                {
                    var src = targetElement.GetAttribute("src");
                    string[] words = src.Split('/');
                    if (words.Length > 2) return words[words.Length-2];
                } else if (theme == "linkedin" || theme == "reddit")
                {
                    return targetElement.Text;
                } else if (theme == "x")
                {
                    var src = targetElement.GetAttribute("src");
                    string[] words = src.Split('/');
                    if (words.Length > 2) return words[words.Length-1];
                }
            }
            Log.Trace($"Social:: Unable to find user name to use for post, using default name.");
            return "Dr.Mysterious"; // always return a name
        }
        
        public override bool DoBrowse(TimelineHandler handler)
        {
            var targetXpath = GetThemeAction(theme, "Browse");
            if (targetXpath != null) {
            var targetElement = Driver.FindElement(By.XPath(targetXpath));
            BrowserHelperSupport.ElementClick(Driver, targetElement);
            Thread.Sleep(500);
            Log.Trace($"Social:: Successfully browsed post on site {site}.");
            }
            return true;
        }

        public override bool DoLike(TimelineHandler handler)
        {
            // just like the first post in the feed
            // always scroll to the element as it may off the viewport
            // this relies on the fact that before the next action the page
            // will be reset to the top
            try
                {
                    // this is hacky -- element may be out of view, scroll to it
                    var targetXpath = GetThemeAction(theme, "Like");
                    var targetElement = Driver.FindElement(By.XPath(targetXpath));
                    if (targetElement != null)
                    {
                        // this uses Javascript for compatibility with older Selenium
                        IJavaScriptExecutor js = (IJavaScriptExecutor)Driver;
                        js.ExecuteScript("arguments[0].scrollIntoView(true);", targetElement);
                        Thread.Sleep(500);
                        targetElement = Driver.FindElement(By.XPath(targetXpath));
                        BrowserHelperSupport.ElementClick(Driver, targetElement);
                        Thread.Sleep(500);
                        Log.Trace($"Social:: Successfully liked post on site {site}.");
                    }
                    
                }
            catch (ThreadAbortException ex)
                {
                    throw;  //pass up
                }
            catch (Exception ex)
                {
                    //ignore others as there may be no posts to like
                }
            return true;
        }

        public override bool DoPost(TimelineHandler handler, string action)
        {

            bool doImageUpload = (action == "postWimage");
            string postDirectory = GetPostDirectory();
            if (postDirectory == null) return false;

            string[] postFileList = Directory.GetFiles(postDirectory, "post.txt");
            string useEnterKey = GetThemeAction(theme,"__USE_ENTER_KEY__");

            if (postFileList.Length > 0) {
                // get the file content
                string postContent = File.ReadAllText(postFileList[0]);
                if (stripEmojis)
                {
                    postContent = Regex.Replace(postContent, @"[^\u0000-\u007F]+", "");
                }
                string targetXpath = GetThemeAction(theme, "PostTextContent");
                IWebElement targetElement;
                if (targetXpath != null) {
                    targetElement = Driver.FindElement(By.XPath(targetXpath));
                    targetElement.Clear(); //clear before sending another one
                     if (useEnterKey != null)
                    {
                        targetElement.SendKeys(postContent + Keys.Enter);
                        Log.Trace($"Social:: Successfully added post on site {site}.");
                        themeToPostcount[theme] += 1;
                    } else {
                        targetElement.SendKeys(postContent);
                    }
                    Thread.Sleep(500);
                }
                targetXpath = GetThemeAction(theme, "PostTitle");
                if (targetXpath != null) {
                    targetElement = Driver.FindElement(By.XPath(targetXpath));
                    targetElement.Clear(); //clear before sending another one
                    string title;
                    string[] words = postContent.Split('\n');
                    if (words.Length > 1)
                    {
                        title = words[0];
                    } else
                    {
                        title = "My very own post";
                    }
                    targetElement.SendKeys(title);
                    
                }
                string targetName = "";
                if (userName != null)  targetName = userName;  //always use this if specified
                else {
                    if (lastUserName == null || useUniqueName) {
                        lastUserName = findUserName();
                    }
                    targetName = lastUserName;
                }
                // post target Name
                targetXpath = GetThemeAction(theme, "PostAuthorName");
                if (targetXpath != null)
                {
                    targetElement = Driver.FindElement(By.XPath(targetXpath));
                    targetElement.Clear(); //clear the name before sending another one
                    targetElement.SendKeys(targetName);
                    Thread.Sleep(500);   
                }                
                targetXpath = GetThemeAction(theme, "PostImageFileInput");
                if (action == "postWimage" && targetXpath != null) 
                {
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
                        // Send image file to the file input element
                        targetElement =  Driver.FindElement(By.XPath("//label[text()='Share what you are thinking here...']//following-sibling::input[@type='file']"));
                        if (targetElement != null)
                        {
                            targetElement.SendKeys(imageFile);
                            Thread.Sleep(500);
                        }
                    }

                }

                targetXpath = GetThemeAction(theme, "PostButton");
                if (targetXpath != null && useEnterKey == null)
                {
                    targetElement = Driver.FindElement(By.XPath(targetXpath));
                    Actions actions = new Actions(Driver);
                    actions.MoveToElement(targetElement).Click().Perform();
                   Thread.Sleep(500);
                    Log.Trace($"Social:: Successfully added post on site {site}.");
                    themeToPostcount[theme] += 1;
                }

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

        public List<string> allSites = null;

        public string theme { get; set; } = null;

        public string[] supportedThemes = ["discord","facebook","instagram","linkedin","reddit","x"];

        public Dictionary<string, Dictionary<string, string>> xPathByTheme = null;

        public Dictionary<string, string> siteToTheme = null;

        public Dictionary<string, int> themeToPostcount = null;
        public System.Exception LastException;

        public List<string> topicDirs = null;
        
        private string _state = "initial";
        public int errorCount = 0;
        public int errorThreshold = 3;  //after three strikes, restart the browser
        public string site { get; set; } = null;
        
        public string header { get; set; } = null;

        public string version { get; set; } = null;
        public string contentDirectory { get; set; } = null;

        public string topics { get; set; } = null;

        public string lastUserName { get; set; } = null;

        public bool useUniqueName { get; set; } = true;

        public bool stripEmojis { get; set; } = true;

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

        public static SocialHelper MakeHelper(BaseBrowserHandler callingHandler, IWebDriver callingDriver, TimelineHandler handler, Logger tlog)
        {
            SocialHelper helper = new SocialHelperV1(callingHandler, callingDriver, "1.0");
            helper.stripEmojis = handler.HandlerType == HandlerType.BrowserEdge;
            return helper;
        }

        public bool RestartNeeded()
        {
            return errorCount > errorThreshold;
        }

        private void InitXpathByTheme()
        {
            xPathByTheme = new Dictionary<string, Dictionary<string, string>>
            {
                ["default"] = new Dictionary<string, string>
                {
                    ["Browse"] = "//div[contains(@class,'author-date')]//child::a[contains(@class,'post__author-name')]",
                    ["Like"] = "//a[contains(@class,'btn btn-control like-it')]",
                    ["Username"] =  "//ul[contains(@class,'w-friend-pages-added notification-list')]//child::div[contains(@class,'notification-event')]//child::a[contains(@class,'notification-friend')]",
                    ["PostTextContent"] = "//label[text()='Share what you are thinking here...']//following-sibling::textarea",
                    ["PostAuthorName"] = "//label[text()='Share what you are thinking here...']//following-sibling::input",
                    ["PostImageFileInput"] = "//label[text()='Share what you are thinking here...']//following-sibling::input[@type='file']",
                    ["PostButton"] = "//button[@id='sendButton']"
                },
                
                ["facebook"] = new Dictionary<string, string>
                {
                    ["Browse"] = "//div[@class='post-user-name']//child::a[@href]",
                    ["PostTextContent"] = "//input[@class='create-post-input']",
                    ["Like"] = "//button[@class='post-action like-btn like-it']",
                    ["Username"] = "//div[@class='create-post-top']//child::img[@class='profile-pic']",
                    ["PostButton"] = "//button[@class='post-btn']"
                },
                ["linkedin"] = new Dictionary<string, string>
                {
                    ["PostTextContent"] = "//textarea[@class='post-input']",
                    ["Like"] = "//button[@class='action-btn like-btn like-it']",
                    ["Username"] = "//div[@class='profile-details']//child::h3[@class='profile-name']",
                    ["PostButton"] = "//button[@class='post-btn']"
                },
                ["discord"] = new Dictionary<string, string>
                {
                    ["__USE_ENTER_KEY__"] = "yes",
                    ["PostTextContent"] = "//input[@class='message-input']",
                    ["Like"] = "//button[@class='message-action like-it']",
                    ["PostButton"] = "//div[@class='input-actions']//child::button[@class='send-btn']"
                },
                ["instagram"] = new Dictionary<string, string>
                {
                    ["PostTextContent"] = "//textarea[@class='post-input']",
                    ["Username"] = "//div[@class='post-composer']//child::img[@class='profile-pic']",
                    ["Like"] = "//div[@class='post-actions']//child::div[@class='action-buttons']//child::button[@class='action-btn like-btn like-it']",
                    ["PostButton"] = "//div[@class='post-composer']//child::button[@class='post-btn']"
                },
                ["reddit"] = new Dictionary<string, string>
                {
                    ["PostTextContent"] = "//div[@class='post-composer']//child::textarea[@class='post-input']",
                    ["PostTitle"] = "//div[@class='post-composer']//child::input[@class='post-title']",
                    ["Username"] = "//div[@class='user-menu']//child::span[@class='username']",
                    ["Like"] = "//div[@class='post-actions']//child::button[@class='action-btn like-it']",
                    ["PostButton"] = "//div[@class='post-composer']//child::button[@class='post-btn']"
                },
                ["x"] = new Dictionary<string, string>
                {
                    ["PostTextContent"] = "//div[@class='tweet-input-container']//child::textarea[@class='tweet-input']",
                    ["Username"] = "//div[@class='tweet-composer']//child::img[@class='tweet-avatar']",
                    ["Like"] = "//div[@class='tweet-actions']//child::button[@class='action-btn like-it']",
                    ["PostButton"] = "//div[@class='tweet-toolbar']//child::button[@class='tweet-btn']"
                }
            };

        }

        

        public void Init(BaseBrowserHandler callingHandler, IWebDriver currentDriver, string aversion)
        {
            baseHandler = callingHandler;
            Driver = currentDriver;
            version = aversion;
            InitXpathByTheme();
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
            
            if (!themeToPostcount.ContainsKey(theme) || themeToPostcount[theme] == 0)
            {
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
                        allSites = new List<string>();

                        foreach (var cmd in timelineEvent.CommandArgs)
                        {
                            //each argument string is key:value, parse this
                            var argString = cmd.ToString();
                            if (!string.IsNullOrEmpty(argString))
                            {
                                var words = argString.Split(charSeparators, 2, StringSplitOptions.None);
                                if (words.Length == 2)
                                {
                                    if (words[0] == "site")
                                    {
                                        site = words[1];
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
                                            header = "http://"; //default header
                                        }

                                        allSites.Add(site);
                                    }
                                }
                            }
                        }

                        if (allSites.Count == 0)
                        {
                            Log.Trace(
                                $"Social:: The command args must specify at least one 'site:<value>' , social browser action will not be executed.");
                            baseHandler.SocialAbort = true;
                            return;
                        }

                        if (Driver is OpenQA.Selenium.Firefox.FirefoxDriver)
                        {
                            AttachmentWindowTitle = "File Upload";
                        }

                        // choose a random site, this just browses to site as we don't want an empty browser sitting there
                        var index = _random.Next(0, allSites.Count);
                        site = allSites[index];

                        // this always goes back to home site
                        // also fills in siteToTheme cache
                        if (!DoInitialLogin(handler))
                        {
                            Log.Trace(
                                $"Social:: Target site {site} does not appear to be a socializer site, aborting Social browsing.");
                            baseHandler.SocialAbort = true;
                            return;
                        }

                        //this initial browse was just a dummy browse to have the browser show something
                        _state = "execute";
                        break;

                    case "execute":

                        // choose a random site
                        var index1 = _random.Next(0, allSites.Count);
                        site = allSites[index1];

                        // this always goes back to home site
                        if (!DoInitialLogin(handler))
                        {
                            Log.Trace(
                                $"Social:: Target site {site} does not appear to be a socializer site, aborting Social browsing.");
                            baseHandler.SocialAbort = true;
                            return;
                        }

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
                        //throw new Exception($"Social:: Simulated error to test restart logic, site {site}, action {socialAction}.");
                        break;


                }

            }
            catch (Exception e)
            {
                if (e is ThreadAbortException || e is ThreadInterruptedException)
                {
                    throw;
                }
                // DO NOT restart Socializer. The restart logic is not correct.
                //errorCount = errorThreshold + 1;  // an exception at  this level needs a restart
                errorCount = 0;  // reset error count so that we don't restart on next iteration
                LastException = e;  //save last exception so that it can be thrown up during restart
                Log.Trace($"WebSocial:: Error at top level of execute loop.");
                if (theme != null) Log.Trace($"Social:: Theme is {theme}, site is {site}.");
                else Log.Trace($"Social:: Theme is null, site is {site}.");
                Log.Error(e);
                Thread.Sleep(20000);   // sleep to prevent tight error loop
            }
        }

       
    }

   
    }

    
    