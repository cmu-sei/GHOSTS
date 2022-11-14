using Ghosts.Client.Infrastructure;
using Ghosts.Domain;
using Ghosts.Domain.Code;
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
using NPOI.POIFS.Properties;
using Actions = OpenQA.Selenium.Interactions.Actions;
using Exception = System.Exception;

namespace Ghosts.Client.Handlers
{

    /// <summary>
    /// Handles Sharepoint actions for base browser handler
    /// </summary>
    internal class SharepointHelper
    {
        
        private int _sharepointDeletionProbability = -1;
        private int _sharepointUploadProbability = -1;
        private int _sharepointDownloadProbability = -1;
        private Credentials _sharepointCredentials = null;
        private string _sharepointState = "initial";
        string _sharepointSite = null;
        string _sharepointUsername = null;
        string _sharepointPassword = null;
        string _sharepointVersion = null;
        string _sharepointUploadDirectory = null;

        BaseBrowserHandler parentHandler = null; 

        public SharepointHelper(BaseBrowserHandler parent)
        {
            parentHandler = parent;
        }

        private bool CheckSharePointProbabilityVar(string name, int value)
        {
            if (!(value >= 0 && value <= 100))
            {
                parentHandler.DoLogTrace($"Variable {name} with value {value} must be an int between 0 and 100, setting to 0");
                return false;
            }
            return true;
        }

        /// <summary>
        /// If a sharepoint file upload is not accepted, deal with the error popup
        /// </summary>
        private bool SharepointUploadBlocked()
        {
            Actions actions;
            try
            {

                parentHandler.Driver.SwitchTo().ParentFrame();
                var targetElement = parentHandler.Driver.FindElement(By.XPath("//a[@class='ms-dlgCloseBtn']"));
                actions = new Actions(parentHandler.Driver);
                //close popup
                actions.MoveToElement(targetElement).Click().Perform();
                return true;  //was blocked
            }
            catch  //ignore any errors, upload may have not been blocked
            {

            }
            return false; //not blocked

        }

        private string SharepointGetUploadFile()
        {
            try
            {
                string[] filelist = Directory.GetFiles(_sharepointUploadDirectory, "*");
                if (filelist.Length > 0) return filelist[parentHandler.DoRandomNext(0, filelist.Length)];
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
        public void SharepointExecute(TimelineHandler handler, TimelineEvent timelineEvent)
        {
            string credFname;
            string credentialKey = null;
            RequestConfiguration config;
            Actions actions;

            switch (_sharepointState)
            {


                case "initial":
                    //these are only parsed once, global for the handler as handler can only have one entry.
                    if (handler.HandlerArgs.ContainsKey("sharepoint-version"))
                    {
                        _sharepointVersion = handler.HandlerArgs["sharepoint-version"].ToString();
                        //this needs to be extended in the future
                        if (_sharepointVersion != "2013")
                        {
                            parentHandler.DoLogTrace($"Sharepoint:: Unsupported Sharepoint version {_sharepointVersion} , sharepoint browser action will not be executed.");
                            parentHandler.sharepointAbort = true;
                            return;
                        }
                    }
                    else
                    {
                        parentHandler.DoLogTrace($"Sharepoint:: Handler option 'sharepoint-version' must be specified, currently supported versions: '2013'. Sharepoint browser action will not be executed.");
                        parentHandler.sharepointAbort = true;
                        return;
                    }

                    if (handler.HandlerArgs.ContainsKey("sharepoint-upload-directory"))
                    {
                        string targetDir = handler.HandlerArgs["sharepoint-upload-directory"].ToString();
                        targetDir = Environment.ExpandEnvironmentVariables(targetDir);
                        if (!Directory.Exists(targetDir))
                        {
                            parentHandler.DoLogTrace($"Sharepoint:: upload directory {targetDir} does not exist, using browser downloads directory.");
                        }
                        else
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
                        parentHandler.DoLogTrace($"Sharepoint:: The sum of the download/upload/deletion sharepoint probabilities is > 100 , sharepoint browser action will not be executed.");
                        parentHandler.sharepointAbort = true;
                        return;
                    }

                    if ((_sharepointDeletionProbability + _sharepointUploadProbability + _sharepointDownloadProbability) == 0)
                    {
                        parentHandler.DoLogTrace($"Sharepoint:: The sum of the download/upload/deletion sharepoint probabilities == 0 , sharepoint browser action will not be executed.");
                        parentHandler.sharepointAbort = true;
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
                            parentHandler.DoLogTrace($"Sharepoint:: Error parsing sharepoint credentials file {credFname} , sharepoint browser action will not be executed.");
                            parentHandler.sharepointAbort = true;
                            parentHandler.DoLogError(e);
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
                        parentHandler.DoLogTrace($"Sharepoint:: The command args must specify a 'site:<value>' , sharepoint browser action will not be executed.");
                        parentHandler.sharepointAbort = true;
                        return;
                    }

                    //check if site starts with http:// or https:// 
                    _sharepointSite = _sharepointSite.ToLower();
                    string header = null;
                    Regex rx = new Regex("^http://.*", RegexOptions.Compiled);
                    var match = rx.Matches(_sharepointSite);
                    if (match.Count > 0) header = "http://";
                    if (header == null)
                    {
                        rx = new Regex("^https://.*", RegexOptions.Compiled);
                        match = rx.Matches(_sharepointSite);
                        if (match.Count > 0) header = "https://";
                    }
                    if (header != null)
                    {
                        _sharepointSite = _sharepointSite.Replace(header, "");
                    }
                    else
                    {
                        header = "http://";  //default header
                    }




                    if (credentialKey == null)
                    {
                        parentHandler.DoLogTrace($"Sharepoint:: The command args must specify a 'credentialKey:<value>' , sharepoint browser action will not be executed.");
                        parentHandler.sharepointAbort = true;
                        return;
                    }

                    _sharepointUsername = _sharepointCredentials.GetUsername(credentialKey);
                    _sharepointPassword = _sharepointCredentials.GetPassword(credentialKey);

                    if (_sharepointUsername == null || _sharepointPassword == null)
                    {
                        parentHandler.DoLogTrace($"Sharepoint:: The credential key {credentialKey} does not return a valid credential from file {credFname},   sharepoint browser action will not be executed");
                        parentHandler.sharepointAbort = true;
                        return;
                    }


                    //have the username, password
                    string portal = _sharepointSite;

                    string target = header + _sharepointUsername + ":" + _sharepointPassword + "@" + portal + "/";
                    config = RequestConfiguration.Load(handler, target);
                    try
                    {
                        parentHandler.MakeRequest(config);
                    }
                    catch (System.Exception e)
                    {
                        parentHandler.DoLogTrace($"Sharepoint:: Unable to parse site {_sharepointSite}, url may be malformed. Sharepoint browser action will not be executed.");
                        parentHandler.sharepointAbort = true;
                        parentHandler.DoLogError(e);
                        return;

                    }
                    target = header + portal + "/Documents/Forms/Allitems.aspx";
                    config = RequestConfiguration.Load(handler, target);
                    parentHandler.MakeRequest(config);
                    //click on the files tab
                    try
                    {
                        var targetElement = parentHandler.Driver.FindElement(By.Id("Ribbon.Document-title"));
                        targetElement.Click();
                    }
                    catch (System.Exception e)
                    {
                        parentHandler.DoLogTrace($"Sharepoint:: Unable to find Sharepoint menu, login may have failed, check the credentials. Sharepoint browser action will not be executed.");
                        parentHandler.sharepointAbort = true;
                        parentHandler.DoLogError(e);
                        return;

                    }

                    //at this point we are logged in, files tab selected, ready for action
                    _sharepointState = "execute";
                    break;

                case "execute":

                    //determine what to do
                    int choice = parentHandler.DoRandomNext(0, 101);
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
                        parentHandler.DoLogTrace($"Sharepoint:: Action is skipped for this cycle.");
                        return;
                    }

                    if (sharepointAction == "download")
                    {
                        //select a file to download
                        try
                        {
                            var targetElements = parentHandler.Driver.FindElements(By.CssSelector("td[class='ms-cellStyleNonEditable ms-vb-itmcbx ms-vb-imgFirstCell']"));
                            if (targetElements.Count > 0)
                            {


                                int docNum = parentHandler.DoRandomNext(0, targetElements.Count);
                                actions = new Actions(parentHandler.Driver);
                                actions.MoveToElement(targetElements[docNum]).Click().Perform();

                                var checkboxElement = targetElements[docNum].FindElement(By.XPath(".//div[@role='checkbox']"));
                                string fname = checkboxElement.GetAttribute("title");

                                Thread.Sleep(1000);
                                //download it
                                var targetElement = parentHandler.Driver.FindElement(By.Id("Ribbon.Documents.Copies.Download-Large"));
                                actions = new Actions(parentHandler.Driver);
                                actions.MoveToElement(targetElement).Click().Perform();

                                Thread.Sleep(1000);
                                //have to click on document element again to deselect it in order to enable next download
                                //targetElements[docNum].Click();  //select the doc
                                actions = new Actions(parentHandler.Driver);
                                actions.MoveToElement(targetElements[docNum]).Click().Perform();
                                parentHandler.DoLogTrace($"Sharepoint:: Downloaded file {fname} from site {_sharepointSite}.");
                                Thread.Sleep(1000);
                            }
                        }
                        catch (Exception e)
                        {
                            parentHandler.DoLogTrace($"Sharepoint:: Error performing sharepoint download from site {_sharepointSite}.");
                            parentHandler.DoLogError(e);
                        }
                    }
                    if (sharepointAction == "upload")
                    {
                        try
                        {
                            string fname = SharepointGetUploadFile();
                            if (fname == null)
                            {
                                parentHandler.DoLogTrace($"Sharepoint:: Cannot find a valid file to upload from directory {_sharepointUploadDirectory}.");
                                return;
                            }
                            var span = new TimeSpan(0, 0, 0, 5, 0);
                            var targetElement = parentHandler.Driver.FindElement(By.Id("Ribbon.Documents.New.AddDocument-Large"));
                            actions = new Actions(parentHandler.Driver);
                            actions.MoveToElement(targetElement).Click().Perform();
                            Thread.Sleep(1000);
                            parentHandler.Driver.SwitchTo().Frame(parentHandler.Driver.FindElement(By.ClassName("ms-dlgFrame")));
                            WebDriverWait wait = new WebDriverWait(parentHandler.Driver, span);
                            var uploadElement = parentHandler.Driver.FindElement(By.Id("ctl00_PlaceHolderMain_UploadDocumentSection_ctl05_InputFile"));
                            uploadElement.SendKeys(fname);
                            Thread.Sleep(500);
                            var okElement = parentHandler.Driver.FindElement(By.Id("ctl00_PlaceHolderMain_ctl03_RptControls_btnOK"));
                            actions = new Actions(parentHandler.Driver);
                            actions.MoveToElement(okElement).Click().Perform();
                            Thread.Sleep(500);
                            if (SharepointUploadBlocked())
                            {
                                parentHandler.DoLogTrace($"Sharepoint:: Failed to upload file {fname} to site {_sharepointSite}.");
                            }
                            else
                            {
                                parentHandler.DoLogTrace($"Sharepoint:: Uploaded file {fname} to site {_sharepointSite}.");
                            }


                        }
                        catch (Exception e)
                        {
                            parentHandler.DoLogTrace($"Sharepoint:: Error performing sharepoint upload to site {_sharepointSite}.");
                            parentHandler.DoLogError(e);
                        }

                    }
                    if (sharepointAction == "delete")
                    {
                        //select a file to delete
                        try
                        {
                            var targetElements = parentHandler.Driver.FindElements(By.CssSelector("td[class='ms-cellStyleNonEditable ms-vb-itmcbx ms-vb-imgFirstCell']"));
                            if (targetElements.Count > 0)
                            {
                                int docNum = parentHandler.DoRandomNext(0, targetElements.Count);
                                actions = new Actions(parentHandler.Driver);
                                actions.MoveToElement(targetElements[docNum]).Click().Perform();

                                var checkboxElement = targetElements[docNum].FindElement(By.XPath(".//div[@role='checkbox']"));
                                string fname = checkboxElement.GetAttribute("title");

                                Thread.Sleep(1000);
                                //delete it
                                //somewhat weird, had to locate this element by the tooltip
                                var targetElement = parentHandler.Driver.FindElement(By.CssSelector("a[aria-describedby='Ribbon.Documents.Manage.Delete_ToolTip'"));
                                actions = new Actions(parentHandler.Driver);
                                //deal with the popup
                                actions.MoveToElement(targetElement).Click().Perform();
                                Thread.Sleep(1000);
                                parentHandler.Driver.SwitchTo().Alert().Accept();
                                parentHandler.DoLogTrace($"Sharepoint:: Deleted file {fname} from site {_sharepointSite}.");
                                Thread.Sleep(1000);
                            }
                            else
                            {
                                parentHandler.DoLogTrace($"Sharepoint:: No documents to delete from {_sharepointSite}.");
                            }
                        }
                        catch (Exception e)
                        {
                            parentHandler.DoLogTrace($"Sharepoint:: Error performing sharepoint download from site {_sharepointSite}.");
                            parentHandler.DoLogError(e);
                        }
                    }
                    break;




            }

        }


    }
}
