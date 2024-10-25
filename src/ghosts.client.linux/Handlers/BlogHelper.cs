using System;
using System.Text.RegularExpressions;
using ghosts.client.linux.Infrastructure;
using ghosts.client.linux.Infrastructure.Browser;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Newtonsoft.Json;
using NLog;
using OpenQA.Selenium;

namespace ghosts.client.linux.handlers
{

    /// <summary>
    /// Handles Blog actions for BaseBrowserHandler
    /// </summary>
    public abstract partial class BlogHelper : BrowserHelper
    {

        private int _deletionProbability = -1;
        private int _uploadProbability = -1;
        private int _downloadProbability = -1;
        private int _replyProbability = -1;
        private Credentials _credentials = null;
        private string _state = "initial";
        public string site { get; set; } = null;
        public string header { get; set; } = null;
        public string username { get; set; } = null;
        public string password { get; set; } = null;
        string _version = null;

        public BlogContentManager contentManager = null;


        public static BlogHelper MakeHelper(BaseBrowserHandler callingHandler, IWebDriver callingDriver, TimelineHandler handler, Logger tlog)
        {
            BlogHelper helper = null;

            //get helper based on version
            if (handler.HandlerArgs.TryGetValue("blog-version", out var value))
            {
                var version = value.ToString();
                if (version == "drupal") helper = new BlogHelperDrupal(callingHandler, callingDriver);
                if (helper == null)
                {
                    tlog.Trace($"Blog:: Unsupported Blog version {version} , Blog browser action will not be executed.");
                }
            }
            else
            {
                tlog.Trace($"Blog:: Handler option 'blog-version' must be specified, currently supported versions: 'drupal'. Blog browser action will not be executed.");
            }
            return helper;
        }

        public void Init(BaseBrowserHandler parent, IWebDriver currentDriver)
        {
            baseHandler = parent;
            contentManager = new BlogContentManager();
            Driver = currentDriver;
        }

        private static bool CheckProbabilityVar(string name, int value)
        {
            if (!(value >= 0 && value <= 100))
            {
                Log.Trace($"Variable {name} with value {value} must be an int between 0 and 100, setting to 0");
                return false;
            }
            return true;
        }



        public virtual bool DoInitialLogin(TimelineHandler handler, string user, string pw)
        {
            Log.Trace($"Blog:: Unsupported action 'DoInitialLogin' in Blog version {_version} ");
            return false;
        }

        public virtual bool DoBrowse(TimelineHandler handler)
        {
            Log.Trace($"Blog:: Unsupported action 'Browse' in Blog version {_version} ");
            return false;
        }

        public virtual bool DoDelete(TimelineHandler handler)
        {
            Log.Trace($"Blog:: Unsupported action 'Delete' in Blog version {_version} ");
            return false;
        }

        public virtual bool DoUpload(TimelineHandler handler, string subject, string body)
        {
            Log.Trace($"Blog:: Unsupported action 'upload' in Blog version {_version} ");
            return true;
        }

        public virtual bool DoReply(TimelineHandler handler, string reply)
        {
            Log.Trace($"Blog:: Unsupported action 'reply' in Blog version {_version} ");
            return true;
        }


        private string GetNextAction()
        {
            var choice = _random.Next(0, 101);
            string blogAction = null;
            int endRange;
            var startRange = 0;

            if (_deletionProbability > 0)
            {
                endRange = _deletionProbability;
                if (choice >= startRange && choice <= endRange) blogAction = "delete";
                else startRange = endRange + 1;
            }

            if (blogAction == null && _uploadProbability > 0)
            {
                endRange = startRange + _uploadProbability;
                if (choice >= startRange && choice <= endRange) blogAction = "upload";
                else startRange = endRange + 1;
            }

            if (blogAction == null && _downloadProbability > 0)
            {
                endRange = startRange + _downloadProbability;
                if (choice >= startRange && choice <= endRange) blogAction = "download";
                else startRange = endRange + 1;

            }
            if (blogAction == null && _replyProbability > 0)
            {
                endRange = startRange + _replyProbability;
                if (choice >= startRange && choice <= endRange) blogAction = "reply";
                else _ = endRange + 1;

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


            switch (_state)
            {


                case "initial":
                    //these are only parsed once, global for the handler as handler can only have one entry.
                    _version = handler.HandlerArgs["blog-version"].ToString();  //guaranteed to have this option, already checked in base handler


                    if (_deletionProbability < 0 && handler.HandlerArgs.TryGetValue("blog-deletion-probability", out var v1))
                    {
                        int.TryParse(v1.ToString(), out _deletionProbability);
                        if (!CheckProbabilityVar(v1.ToString(), _deletionProbability))
                        {
                            _deletionProbability = 0;
                        }
                    }
                    if (_uploadProbability < 0 && handler.HandlerArgs.TryGetValue("blog-upload-probability", out var v2))
                    {
                        int.TryParse(v2.ToString(), out _uploadProbability);
                        if (!CheckProbabilityVar(v2.ToString(), _uploadProbability))
                        {
                            _uploadProbability = 0;
                        }
                    }
                    if (_downloadProbability < 0 && handler.HandlerArgs.TryGetValue("blog-browse-probability", out var v3))
                    {
                        int.TryParse(v3.ToString(), out (_downloadProbability));
                        if (!CheckProbabilityVar(v3.ToString(), _downloadProbability))
                        {
                            _downloadProbability = 0;
                        }
                    }

                    if (_replyProbability < 0 && handler.HandlerArgs.TryGetValue("blog-reply-probability", out var v4))
                    {
                        int.TryParse(v4.ToString(), out (_replyProbability));
                        if (!CheckProbabilityVar(v4.ToString(), _replyProbability))
                        {
                            _replyProbability = 0;
                        }
                    }

                    if ((_deletionProbability + _uploadProbability + _downloadProbability + _replyProbability) > 100)
                    {
                        Log.Trace($"Blog:: The sum of the browse/upload/deletion/reply blog probabilities is > 100 , blog browser action will not be executed.");
                        baseHandler.BlogAbort = true;
                        return;
                    }

                    if ((_deletionProbability + _uploadProbability + _downloadProbability + _replyProbability) == 0)
                    {
                        Log.Trace($"Blog:: The sum of the download/upload/deletion/reply blog probabilities == 0 , blog browser action will not be executed.");
                        baseHandler.BlogAbort = true;
                        return;
                    }
                    if (handler.HandlerArgs.TryGetValue("delay-jitter", out var value))
                    {
                        baseHandler.JitterFactor = Jitter.JitterFactorParse(value.ToString());
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
                            Log.Trace($"Blog:: Error parsing blog credentials file {credFname} , blog browser action will not be executed.");
                            baseHandler.BlogAbort = true;
                            Log.Error(e);
                            return;
                        }
                    }

                    //now parse the command args
                    //parse the command args


                    var charSeparators = new char[] { ':' };
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
                        Log.Trace($"Blog:: The command args must specify a 'site:<value>' , blog browser action will not be executed.");
                        baseHandler.BlogAbort = true;
                        return;
                    }

                    //check if site starts with http:// or https://
                    site = site.ToLower();

                    Regex rx = MyRegex();
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
                        Log.Trace($"Blog:: The command args must specify a 'credentialKey:<value>' , blog browser action will not be executed.");
                        baseHandler.BlogAbort = true;
                        return;
                    }

                    username = _credentials.GetUsername(credentialKey);
                    password = _credentials.GetPassword(credentialKey);

                    if (username == null || password == null)
                    {
                        Log.Trace($"Blog:: The credential key {credentialKey} does not return a valid credential from file {credFname}, blog browser action will not be executed");
                        baseHandler.BlogAbort = true;
                        return;
                    }

                    //have username, password - do the initial login
                    if (!DoInitialLogin(handler, username, password))
                    {
                        baseHandler.BlogAbort = true;
                        return;
                    }

                    //at this point we are logged in, ready for action
                    _state = "execute";
                    break;

                case "execute":

                    //determine what to do
                    var blogAction = GetNextAction();


                    if (blogAction == null)
                    {
                        //nothing to do this cycle
                        Log.Trace($"Blog:: Action is skipped for this cycle.");
                        return;
                    }

                    if (blogAction == "download")
                    {
                        if (!DoBrowse(handler))
                        {
                            baseHandler.BlogAbort = true;
                            return;
                        }
                    }

                    if (blogAction == "delete")
                    {
                        if (!DoDelete(handler))
                        {
                            baseHandler.BlogAbort = true;
                            return;
                        }
                    }

                    if (blogAction == "upload")
                    {
                        //get new content
                        contentManager.BlogContentNext();
                        if (contentManager.Subject == null || contentManager.Body == null)
                        {
                            Log.Trace($"Blog:: Content unavailable, check Blog content file, upload skipped.");
                        }
                        else if (!DoUpload(handler, contentManager.Subject, contentManager.Body))
                        {
                            baseHandler.BlogAbort = true;
                            return;
                        }
                    }
                    if (blogAction == "reply")
                    {
                        //get new content
                        var reply = contentManager.BlogReplyNext();
                        if (reply == null)
                        {
                            Log.Trace($"Blog:: Reply content unavailable, check Blog reply file, reply action skipped.");
                        }
                        else if (!DoReply(handler, reply))
                        {
                            baseHandler.BlogAbort = true;
                            return;
                        }
                    }
                    BaseHandler.Report(new ReportItem { Handler = $"Blog: {handler.HandlerType}", Command = blogAction, Arg = "", Trackable = timelineEvent.TrackableId });


                    break;




            }

        }

        [GeneratedRegex("^http://.*", RegexOptions.Compiled)]
        private static partial Regex MyRegex();
    }
}
