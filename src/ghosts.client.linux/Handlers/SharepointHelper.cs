using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using ghosts.client.linux.Infrastructure;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Newtonsoft.Json;
using NLog;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using Actions = OpenQA.Selenium.Interactions.Actions;
using Exception = System.Exception;


namespace ghosts.client.linux.handlers
{


    /// <summary>
    /// Supports upload, download, deletion of documents
    /// download, deletion only done from the first page
    /// Tested with Sharepoint 2013 and 2019
    /// 2019 uses the 'classic view' for 2013 compatibility
    /// </summary>
    public class SharepointHelper2013_2019 : SharepointHelper
    {

        public SharepointHelper2013_2019(BaseBrowserHandler callingHandler, IWebDriver callingDriver, string aversion)
        {
            base.Init(callingHandler, callingDriver, aversion);

        }

        public override bool DoInitialLogin(TimelineHandler handler, string user, string pw)
        {
            //have the username, password
            RequestConfiguration config;

            var portal = site;

            var pw_encoded = HttpUtility.UrlEncode(pw);
            var user_encoded = HttpUtility.UrlEncode(user);
            var target = header + user_encoded + ":" + pw_encoded + "@" + portal + "/";
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
                Log.Trace($"Sharepoint:: Unable to parse site {site}, url may be malformed. Sharepoint browser action will not be executed.");
                Log.Error(e);
                return false;

            }
            if (version == "2013") target = header + portal + "/Documents/Forms/Allitems.aspx";
            else target = header + portal + "/Shared Documents/Forms/Allitems.aspx";
            config = RequestConfiguration.Load(handler, target);
            baseHandler.MakeRequest(config);

            // check if there is a 'Return to classic Sharepoint link
            if (version != "2013")
            {
                var inClassic = false;
                try
                {
                    var targetElement = Driver.FindElement(By.XPath("//a[contains(@onclick,'GoToModern(true)')]"));
                    inClassic = true;
                }
                catch (ThreadAbortException)
                {
                    throw;  //pass up
                }
                catch
                {
                    //just ignore as if the screen is large, the menu is not present
                }
                if (!inClassic)
                {
                    try
                    {
                        // the screen may be small and the classic link hidden in the hamburger menu
                        var targetElement = Driver.FindElement(By.Id("O365_MainLink_HamburgerButton"));
                        targetElement.Click();
                        Thread.Sleep(2000);
                    }
                    catch (ThreadAbortException)
                    {
                        throw;  //pass up
                    }
                    catch
                    {
                        //just ignore as if the screen is large, the menu is not present
                    }
                    try
                    {


                        var targetElement = Driver.FindElement(By.CssSelector("[aria-label=\"Click or enter to return to classic SharePoint\""));

                        targetElement.Click();
                        Thread.Sleep(2000);
                    }
                    catch (ThreadAbortException)
                    {
                        throw;  //pass up
                    }
                    catch (System.Exception e)
                    {
                        Log.Trace($"Sharepoint:: Unable to find classic sharepoint link, browser action will not be executed.");
                        Log.Error(e);
                        return false;
                    }
                }
            }

            //click on the files tab
            try
            {
                var targetElement = Driver.FindElement(By.Id("Ribbon.Document-title"));
                targetElement.Click();
            }
            catch (ThreadAbortException)
            {
                throw;  //pass up
            }
            catch (System.Exception e)
            {
                Log.Trace($"Sharepoint:: Unable to find Sharepoint menu, login may have failed, check the credentials. Sharepoint browser action will not be executed.");
                Log.Error(e);
                return false;

            }

            return true;
        }


        public override bool DoDownload(TimelineHandler handler)
        {

            Actions actions;


            try
            {
                //this only gets non-folder elements
                var targetElements = Driver.FindElements(By.XPath("//img[@draggable='true']"));
                if (targetElements != null && targetElements.Count > 0)
                {
                    var docNum = _random.Next(0, targetElements.Count);
                    var targetElement = targetElements[docNum];
                    var fname = targetElement.GetAttribute("title");
                    //now find the chkbox element corresponding to this
                    //get the element, then parent, then preceding sibling
                    var checkBoxElement = Driver.FindElement(By.XPath($"//img[contains(@title,'{fname}')]//parent::td//preceding-sibling::td[contains(@class,'ms-vb-imgFirstCell')]"));
                    MoveToElementAndClick(checkBoxElement);
                    Thread.Sleep(5000);
                    //download it
                    targetElement = Driver.FindElement(By.Id("Ribbon.Documents.Copies.Download-Large"));
                    actions = new Actions(Driver);
                    actions.MoveToElement(targetElement).Click().Perform();
                    Thread.Sleep(5000);
                    //have to click on document element again to deselect it in order to enable next download
                    MoveToElementAndClick(checkBoxElement);
                    Log.Trace($"Sharepoint:: Downloaded file {fname} from site {site}.");
                    Thread.Sleep(1000);
                }
            }
            catch (ThreadAbortException)
            {
                throw;  //pass up
            }
            catch (Exception e)
            {
                Log.Trace($"Sharepoint:: Error performing sharepoint download from site {site}.");
                Log.Error(e);
                errorCount += 1;
            }
            return true;
        }

        /// <summary>
        /// If a sharepoint file upload is not accepted, deal with the error popup
        /// </summary>
        public bool UploadBlocked()
        {
            Actions actions;
            try
            {

                Driver.SwitchTo().ParentFrame();
                var targetElement = Driver.FindElement(By.XPath("//a[@class='ms-dlgCloseBtn']"));
                actions = new Actions(Driver);
                //close popup
                actions.MoveToElement(targetElement).Click().Perform();
                return true;  //was blocked
            }
            catch (ThreadAbortException)
            {
                throw;  //pass up
            }
            catch  //ignore any errors, upload may have not been blocked
            {

            }
            return false; //not blocked

        }

        public override bool DoUpload(TimelineHandler handler)
        {
            Actions actions;

            try
            {
                var fname = GetUploadFile();
                if (fname == null)
                {
                    Log.Trace($"Sharepoint:: Cannot find a valid file to upload from directory {uploadDirectory}.");
                    return true;
                }
                var span = new TimeSpan(0, 0, 0, 5, 0);
                var targetElement = Driver.FindElement(By.Id("Ribbon.Documents.New.AddDocument-Large"));
                actions = new Actions(Driver);
                actions.MoveToElement(targetElement).Click().Perform();
                IWebElement uploadElement;
                var i = 0;
                // do these contortions as sometimes in 2019 this will fail because there is an
                // an intermediate popup called 'WORKING' that can confuse Selenium.
                // When this happens, the upload will not succeed but we will not get hung up on the upload pane
                while (true)
                {
                    try
                    {
                        Thread.Sleep(5000);
                        Driver.SwitchTo().Frame(Driver.FindElement(By.ClassName("ms-dlgFrame")));
                        uploadElement = Driver.FindElement(By.XPath("//input[contains(@class,'ms-fileinput')]"));
                        uploadElement.SendKeys(fname);
                        Thread.Sleep(500);
                        break;
                    }
                    catch (ThreadAbortException)
                    {
                        throw;  //pass up
                    }
                    catch (Exception)
                    {
                        i += 1;
                        if (i >= 5) break;
                    }
                }
                IWebElement okElement;
                if (version == "2013")
                {
                    okElement = Driver.FindElement(By.Id("ctl00_PlaceHolderMain_ctl03_RptControls_btnOK"));
                }
                else
                {
                    okElement = Driver.FindElement(By.XPath("//*[@value='OK']"));
                }
                actions = new Actions(Driver);
                actions.MoveToElement(okElement).Click().Perform();
                Thread.Sleep(500);
                if (UploadBlocked())
                {
                    Log.Trace($"Sharepoint:: Failed to upload file {fname} to site {site}.");
                }
                else
                {
                    Log.Trace($"Sharepoint:: Uploaded file {fname} to site {site}.");
                }


            }
            catch (ThreadAbortException)
            {
                throw;  //pass up
            }
            catch (Exception e)
            {
                Log.Trace($"Sharepoint:: Error performing sharepoint upload to site {site}.");
                Log.Error(e);
                errorCount += 1;
            }
            return true;
        }

        public override bool DoDelete(TimelineHandler handler)
        {
            Actions actions;
            //select a file to delete
            try
            {
                var targetElements = Driver.FindElements(By.XPath("//img[@draggable='true']"));
                if (targetElements.Count > 0)
                {
                    var docNum = _random.Next(0, targetElements.Count);
                    var targetElement = targetElements[docNum];
                    var fname = targetElement.GetAttribute("title");
                    //now find the chkbox element corresponding to this
                    //get the element, then parent, then preceding sibling
                    var checkBoxElement = Driver.FindElement(By.XPath($"//img[contains(@title,'{fname}')]//parent::td//preceding-sibling::td[contains(@class,'ms-vb-imgFirstCell')]"));
                    MoveToElementAndClick(checkBoxElement);


                    Thread.Sleep(5000);
                    //delete it
                    //somewhat weird, had to locate this element by the tooltip
                    targetElement = Driver.FindElement(By.CssSelector("a[aria-describedby='Ribbon.Documents.Manage.Delete_ToolTip'"));
                    actions = new Actions(Driver);
                    //deal with the popup
                    actions.MoveToElement(targetElement).Click().Perform();
                    Thread.Sleep(5000);
                    Driver.SwitchTo().Alert().Accept();
                    Log.Trace($"Sharepoint:: Deleted file {fname} from site {site}.");
                    Thread.Sleep(1000);
                }
                else
                {
                    Log.Trace($"Sharepoint:: No documents to delete from {site}.");
                }
            }
            catch (ThreadAbortException)
            {
                throw;  //pass up
            }
            catch (Exception e)
            {
                Log.Trace($"Sharepoint:: Error performing sharepoint delete from site {site}.");
                Log.Error(e);
                errorCount += 1;
            }
            return true;
        }



    }

    /// <summary>
    /// Handles Sharepoint actions for base browser handler
    /// </summary>
    public abstract partial class SharepointHelper : BrowserHelper
    {


        private int _deletionProbability = -1;
        private int _uploadProbability = -1;
        private int _downloadProbability = -1;
        private Credentials _credentials = null;
        private string _state = "initial";
        public int errorCount = 0;
        public int errorThreshold = 3;  //after three strikes, restart the browser
        public string site { get; set; } = null;
        string username { get; set; } = null;
        string password { get; set; } = null;
        public string header { get; set; } = null;

        public string version { get; set; } = null;
        public string uploadDirectory { get; set; } = null;



        public static SharepointHelper MakeHelper(BaseBrowserHandler callingHandler, IWebDriver callingDriver, TimelineHandler handler, Logger tlog)
        {
            SharepointHelper helper = null;
            if (handler.HandlerArgs.TryGetValue("sharepoint-version", out var v5))
            {
                var version = v5.ToString();
                //this needs to be extended in the future
                if (version == "2013" || version == "2019") helper = new SharepointHelper2013_2019(callingHandler, callingDriver, version);


                if (helper == null)
                {
                    tlog.Trace($"Sharepoint:: Unsupported Sharepoint version {version} , sharepoint browser action will not be executed.");
                }
            }
            else
            {
                Log.Trace($"Sharepoint:: Handler option 'sharepoint-version' must be specified, currently supported versions: '2013'. Sharepoint browser action will not be executed.");

            }
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

        private static bool CheckProbabilityVar(string name, int value)
        {
            if (!(value >= 0 && value <= 100))
            {
                Log.Trace($"Variable {name} with value {value} must be an int between 0 and 100, setting to 0");
                return false;
            }
            return true;
        }


        public string GetUploadFile()
        {
            try
            {
                var filelist = Directory.GetFiles(uploadDirectory, "*");
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


        public virtual bool DoInitialLogin(TimelineHandler handler, string user, string pw)
        {
            Log.Trace($"Blog:: Unsupported action 'DoInitialLogin' in Blog version {version} ");
            return false;
        }

        public virtual bool DoDownload(TimelineHandler handler)
        {
            Log.Trace($"Blog:: Unsupported action 'Browse' in Blog version {version} ");
            return false;
        }

        public virtual bool DoDelete(TimelineHandler handler)
        {
            Log.Trace($"Blog:: Unsupported action 'Delete' in Blog version {version} ");
            return false;
        }

        public virtual bool DoUpload(TimelineHandler handler)
        {
            Log.Trace($"Blog:: Unsupported action 'upload' in Blog version {version} ");
            return true;
        }

        private string GetNextAction()
        {
            var choice = _random.Next(0, 101);
            string spAction = null;
            int endRange;
            var startRange = 0;

            if (_deletionProbability > 0)
            {
                endRange = _deletionProbability;
                if (choice >= startRange && choice <= endRange) spAction = "delete";
                else startRange = endRange + 1;
            }

            if (spAction == null && _uploadProbability > 0)
            {
                endRange = startRange + _uploadProbability;
                if (choice >= startRange && choice <= endRange) spAction = "upload";
                else startRange = endRange + 1;
            }

            if (spAction == null && _downloadProbability > 0)
            {
                endRange = startRange + _downloadProbability;
                if (choice >= startRange && choice <= endRange) spAction = "download";
                else _ = endRange + 1;

            }

            return spAction;

        }

        /// <summary>
        /// This supports only one sharepoint site because it remembers context between runs. Different handlers should be used for different sites
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
                    version = handler.HandlerArgs["sharepoint-version"].ToString();  //guaranteed to have this option, parsed in calling handler


                    if (handler.HandlerArgs.TryGetValue("sharepoint-upload-directory", out var v0))
                    {
                        var targetDir = v0.ToString();
                        targetDir = Environment.ExpandEnvironmentVariables(targetDir);
                        if (!Directory.Exists(targetDir))
                        {
                            Log.Trace($"Sharepoint:: upload directory {targetDir} does not exist, using browser downloads directory.");
                        }
                        else
                        {
                            uploadDirectory = targetDir;
                        }
                    }

                    uploadDirectory ??= KnownFolders.GetDownloadFolderPath();


                    if (_deletionProbability < 0 && handler.HandlerArgs.TryGetValue("sharepoint-deletion-probability", out var v1))
                    {
                        int.TryParse(v1.ToString(), out _deletionProbability);
                        if (!CheckProbabilityVar(v1.ToString(), _deletionProbability))
                        {
                            _deletionProbability = 0;
                        }
                    }
                    if (_uploadProbability < 0 && handler.HandlerArgs.TryGetValue("sharepoint-upload-probability", out var v2))
                    {
                        int.TryParse(v2.ToString(), out _uploadProbability);
                        if (!CheckProbabilityVar(v2.ToString(), _uploadProbability))
                        {
                            _uploadProbability = 0;
                        }
                    }
                    if (_downloadProbability < 0 && handler.HandlerArgs.TryGetValue("sharepoint-download-probability", out var v3))
                    {
                        int.TryParse(v3.ToString(), out (_downloadProbability));
                        if (!CheckProbabilityVar(v3.ToString(), _downloadProbability))
                        {
                            _downloadProbability = 0;
                        }
                    }

                    if ((_deletionProbability + _uploadProbability + _downloadProbability) > 100)
                    {
                        Log.Trace($"Sharepoint:: The sum of the download/upload/deletion sharepoint probabilities is > 100 , sharepoint browser action will not be executed.");
                        baseHandler.SharePointAbort = true;
                        return;
                    }

                    if ((_deletionProbability + _uploadProbability + _downloadProbability) == 0)
                    {
                        Log.Trace($"Sharepoint:: The sum of the download/upload/deletion sharepoint probabilities == 0 , sharepoint browser action will not be executed.");
                        baseHandler.SharePointAbort = true;
                        return;
                    }
                    if (handler.HandlerArgs.TryGetValue("delay-jitter", out var v4))
                    {
                        baseHandler.JitterFactor = Jitter.JitterFactorParse(v4.ToString());
                    }

                    credFname = handler.HandlerArgs["sharepoint-credentials-file"].ToString();

                    if (handler.HandlerArgs.ContainsKey("sharepoint-credentials-file"))
                    {

                        try
                        {
                            _credentials = JsonConvert.DeserializeObject<Credentials>(System.IO.File.ReadAllText(credFname));
                        }
                        catch (System.Exception e)
                        {
                            Log.Trace($"Sharepoint:: Error parsing sharepoint credentials file {credFname} , sharepoint browser action will not be executed.");
                            baseHandler.SharePointAbort = true;
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
                        Log.Trace($"Sharepoint:: The command args must specify a 'site:<value>' , sharepoint browser action will not be executed.");
                        baseHandler.SharePointAbort = true;
                        return;
                    }

                    //check if site starts with http:// or https://
                    site = site.ToLower();
                    header = null;
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
                        Log.Trace($"Sharepoint:: The command args must specify a 'credentialKey:<value>' , sharepoint browser action will not be executed.");
                        baseHandler.SharePointAbort = true;
                        return;
                    }

                    username = _credentials.GetUsername(credentialKey);
                    password = _credentials.GetPassword(credentialKey);

                    if (username == null || password == null)
                    {
                        Log.Trace($"Sharepoint:: The credential key {credentialKey} does not return a valid credential from file {credFname},   sharepoint browser action will not be executed");
                        baseHandler.SharePointAbort = true;
                        return;
                    }

                    var count = 0;
                    //have username, password - do the initial login
                    while (!DoInitialLogin(handler, username, password))
                    {
                        count += 1;
                        if (count < 10)
                        {
                            //login failed, keep trying every 5 minutes in case it is a server startup problem
                            Log.Trace($"Sharepoint:: Login failed, sleeping and trying again.");
                            Thread.Sleep(300 * 1000);
                        }
                        else
                        {
                            Log.Trace($"Sharepoint:: Repeated login failed, aborting Sharepoint browsing.");
                            baseHandler.SharePointAbort = true;
                            break;
                        }
                    }



                    //at this point we are logged in, files tab selected, ready for action
                    _state = "execute";
                    break;

                case "execute":

                    //determine what to do

                    var sharepointAction = GetNextAction();


                    if (sharepointAction == "download")
                    {
                        if (!DoDownload(handler))
                        {
                            baseHandler.BlogAbort = true;
                            return;
                        }

                    }
                    if (sharepointAction == "upload")
                    {
                        if (!DoUpload(handler))
                        {
                            baseHandler.BlogAbort = true;
                            return;
                        }
                    }

                    if (sharepointAction == "delete")
                    {
                        if (!DoDelete(handler))
                        {
                            baseHandler.BlogAbort = true;
                            return;
                        }
                    }
                    BaseHandler.Report(new ReportItem { Handler = $"Sharepoint{version}: {handler.HandlerType}", Command = sharepointAction, Arg = "", Trackable = timelineEvent.TrackableId });
                    break;


            }

        }

        [GeneratedRegex("^http://.*", RegexOptions.Compiled)]
        private static partial Regex MyRegex();
    }


}


