using Ghosts.Client.Infrastructure;
using Ghosts.Client.Infrastructure.Browser;
using Ghosts.Domain.Code;
using Ghosts.Domain;
using Newtonsoft.Json;
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Actions = OpenQA.Selenium.Interactions.Actions;
using Exception = System.Exception;
using System.Reflection;

namespace Ghosts.Client.Handlers
{
    /// <summary>
    /// Handles Blog actions for BaseBrowserHandler
    /// </summary>
    internal class BlogHelper
    {

        private int _deletionProbability = -1;
        private int _uploadProbability = -1;
        private int _downloadProbability = -1;
        private int _replyProbability = -1;
        private Credentials _credentials = null;
        private string _state = "initial";
        public string site { get; set; } = null;
        public string username { get; set; } = null;
        public string password { get; set; } = null;
        string _version = null;

        public BaseBrowserHandler baseHandler = null;
        public BlogContentManager contentManager = null;

        public BlogHelper(BaseBrowserHandler parent)
        {
            baseHandler = parent;
            contentManager = new BlogContentManager();
        }

        private bool CheckProbabilityVar(string name, int value)
        {
            if (!(value >= 0 && value <= 100))
            {
                baseHandler.DoLogTrace($"Variable {name} with value {value} must be an int between 0 and 100, setting to 0");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Only one supported version for now
        /// </summary>
        /// <param name="version"></param>
        /// <returns></returns>
        private bool CheckVersion(string version)
        {
            if (version == "drupal") return true;
            return false;
        }

        /// <summary>
        /// Does the initial login based on version
        /// </summary>
        /// <returns></returns>
        private bool DoInitialLogin(TimelineHandler handler, string header,string user, string pw)
        {
            if (_version == "drupal") return BlogHelperDrupal.DoInitialLogin(handler,this,header,user,pw);
            return false;
        }

        private bool DoBrowse(TimelineHandler handler)
        {
            if (_version == "drupal") return BlogHelperDrupal.DoBrowse(handler, this);
            return false;
        }

        private string GetNextAction()
        {
            int choice = baseHandler.DoRandomNext(0, 101);
            string blogAction = null;
            int endRange;
            int startRange = 0;

            if (_deletionProbability > 0)
            {
                endRange = _deletionProbability;
                if (choice >= startRange && choice <= endRange) blogAction = "delete";
                else startRange = _deletionProbability + 1;
            }

            if (blogAction == null && _uploadProbability > 0)
            {
                endRange = startRange + _uploadProbability;
                if (choice >= startRange && choice <= endRange) blogAction = "upload";
                else startRange = _uploadProbability + 1;
            }

            if (blogAction == null && _downloadProbability > 0)
            {
                endRange = startRange + _downloadProbability;
                if (choice >= startRange && choice <= endRange) blogAction = "download";

            }
            if (blogAction == null && _replyProbability > 0)
            {
                endRange = startRange + _replyProbability;
                if (choice >= startRange && choice <= endRange) blogAction = "reply";

            }
            return blogAction;

        }

        /// <summary>
        /// This supports only one blog site because it remembers context between runs. Different handlers should be used for different sites
        /// On the first execution, login is done to the site, then successive runs keep the login.
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="timelineEvent"></param>
        public void Execute(TimelineHandler handler, TimelineEvent timelineEvent)
        {
            string credFname;
            string credentialKey = null;
            RequestConfiguration config;
            Actions actions;

            switch (_state)
            {


                case "initial":
                    //these are only parsed once, global for the handler as handler can only have one entry.
                    if (handler.HandlerArgs.ContainsKey("blog-version"))
                    {
                        _version = handler.HandlerArgs["blog-version"].ToString();
                        //this needs to be extended in the future
                        if (!CheckVersion(_version))
                        {
                            baseHandler.DoLogTrace($"Blog:: Unsupported Blog version {_version} , Blog browser action will not be executed.");
                            baseHandler.blogAbort = true;
                            return;
                        }
                    }
                    else
                    {
                        baseHandler.DoLogTrace($"Blog:: Handler option 'blog-version' must be specified, currently supported versions: 'drupal'. Blog browser action will not be executed.");
                        baseHandler.blogAbort = true;
                        return;
                    }

                  

                    if (_deletionProbability < 0 && handler.HandlerArgs.ContainsKey("blog-deletion-probability"))
                    {
                        int.TryParse(handler.HandlerArgs["blog-deletion-probability"].ToString(), out _deletionProbability);
                        if (!CheckProbabilityVar(handler.HandlerArgs["blog-deletion-probability"].ToString(), _deletionProbability))
                        {
                            _deletionProbability = 0;
                        }
                    }
                    if (_uploadProbability < 0 && handler.HandlerArgs.ContainsKey("blog-upload-probability"))
                    {
                        int.TryParse(handler.HandlerArgs["blog-upload-probability"].ToString(), out _uploadProbability);
                        if (!CheckProbabilityVar(handler.HandlerArgs["blog-upload-probability"].ToString(), _uploadProbability))
                        {
                            _uploadProbability = 0;
                        }
                    }
                    if (_downloadProbability < 0 && handler.HandlerArgs.ContainsKey("blog-browse-probability"))
                    {
                        int.TryParse(handler.HandlerArgs["blog-browse-probability"].ToString(), out (_downloadProbability));
                        if (!CheckProbabilityVar(handler.HandlerArgs["blog-browse-probability"].ToString(), _downloadProbability))
                        {
                            _downloadProbability = 0;
                        }
                    }

                    if (_replyProbability < 0 && handler.HandlerArgs.ContainsKey("blog-reply-probability"))
                    {
                        int.TryParse(handler.HandlerArgs["blog-reply-probability"].ToString(), out (_replyProbability));
                        if (!CheckProbabilityVar(handler.HandlerArgs["blog-reply-probability"].ToString(), _replyProbability))
                        {
                            _replyProbability = 0;
                        }
                    }

                    if ((_deletionProbability + _uploadProbability + _downloadProbability + _replyProbability) > 100)
                    {
                        baseHandler.DoLogTrace($"Blog:: The sum of the browse/upload/deletion/reply blog probabilities is > 100 , blog browser action will not be executed.");
                        baseHandler.blogAbort = true;
                        return;
                    }

                    if ((_deletionProbability + _uploadProbability + _downloadProbability + _replyProbability) == 0)
                    {
                        baseHandler.DoLogTrace($"Blog:: The sum of the download/upload/deletion/reply blog probabilities == 0 , blog browser action will not be executed.");
                        baseHandler.blogAbort = true;
                        return;
                    }

                    credFname = handler.HandlerArgs["blog-credentials-file"].ToString();

                    if (handler.HandlerArgs.ContainsKey("blog-credentials-file"))
                    {

                        try
                        {
                            _credentials = JsonConvert.DeserializeObject<Credentials>(System.IO.File.ReadAllText(credFname));
                        }
                        catch (System.Exception e)
                        {
                            baseHandler.DoLogTrace($"Blog:: Error parsing blog credentials file {credFname} , blog browser action will not be executed.");
                            baseHandler.blogAbort = true;
                            baseHandler.DoLogError(e);
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
                                if (words[0] == "site") site = words[1];
                                else if (words[0] == "credentialKey") credentialKey = words[1];
                            }
                        }
                    }

                    if (site == null)
                    {
                        baseHandler.DoLogTrace($"Blog:: The command args must specify a 'site:<value>' , blog browser action will not be executed.");
                        baseHandler.blogAbort = true;
                        return;
                    }

                    //check if site starts with http:// or https:// 
                    site = site.ToLower();
                    string header = null;
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




                    if (credentialKey == null)
                    {
                        baseHandler.DoLogTrace($"Blog:: The command args must specify a 'credentialKey:<value>' , blog browser action will not be executed.");
                        baseHandler.blogAbort = true;
                        return;
                    }

                    username = _credentials.GetUsername(credentialKey);
                    password = _credentials.GetPassword(credentialKey);

                    if (username == null || password == null)
                    {
                        baseHandler.DoLogTrace($"Blog:: The credential key {credentialKey} does not return a valid credential from file {credFname}, blog browser action will not be executed");
                        baseHandler.blogAbort = true;
                        return;
                    }

                    //have username, password - do the loging
                    if (!DoInitialLogin(handler,header,username,password)) {
                        baseHandler.blogAbort = true;
                        return;
                    }

                    //at this point we are logged in, ready for action
                    _state = "execute";
                    break;

                case "execute":

                    //determine what to do
                    string blogAction = GetNextAction();

                    
                    if (blogAction == null)
                    {
                        //nothing to do this cycle
                        baseHandler.DoLogTrace($"Blog:: Action is skipped for this cycle.");
                        return;
                    }

                    if (blogAction == "download")
                    {
                        if (!DoBrowse(handler))
                        {
                            baseHandler.blogAbort = true;
                            return;
                        }
                    }
                    if (blogAction == "delete")
                    {
                        //select a file to delete
                        try
                        {
                            var targetElements = baseHandler.Driver.FindElements(By.CssSelector("td[class='ms-cellStyleNonEditable ms-vb-itmcbx ms-vb-imgFirstCell']"));
                            if (targetElements.Count > 0)
                            {
                                int docNum = baseHandler.DoRandomNext(0, targetElements.Count);
                                actions = new Actions(baseHandler.Driver);
                                actions.MoveToElement(targetElements[docNum]).Click().Perform();

                                var checkboxElement = targetElements[docNum].FindElement(By.XPath(".//div[@role='checkbox']"));
                                string fname = checkboxElement.GetAttribute("title");

                                Thread.Sleep(1000);
                                //delete it
                                //somewhat weird, had to locate this element by the tooltip
                                var targetElement = baseHandler.Driver.FindElement(By.CssSelector("a[aria-describedby='Ribbon.Documents.Manage.Delete_ToolTip'"));
                                actions = new Actions(baseHandler.Driver);
                                //deal with the popup
                                actions.MoveToElement(targetElement).Click().Perform();
                                Thread.Sleep(1000);
                                baseHandler.Driver.SwitchTo().Alert().Accept();
                                baseHandler.DoLogTrace($"Blog:: Deleted file {fname} from site {site}.");
                                Thread.Sleep(1000);
                            }
                            else
                            {
                                baseHandler.DoLogTrace($"Blog:: No documents to delete from {site}.");
                            }
                        }
                        catch (Exception e)
                        {
                            baseHandler.DoLogTrace($"Blog:: Error performing sharepoint download from site {site}.");
                            baseHandler.DoLogError(e);
                        }
                    }
                    break;




            }

        }





    }
}
