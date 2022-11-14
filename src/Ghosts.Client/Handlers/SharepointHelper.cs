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
        
        private int _deletionProbability = -1;
        private int _uploadProbability = -1;
        private int _downloadProbability = -1;
        private Credentials _credentials = null;
        private string _state = "initial";
        string _site = null;
        string _username = null;
        string _password = null;
        string _version = null;
        string _uploadDirectory = null;

        BaseBrowserHandler parentHandler = null; 

        public SharepointHelper(BaseBrowserHandler parent)
        {
            parentHandler = parent;
        }

        private bool CheckProbabilityVar(string name, int value)
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
        private bool UploadBlocked()
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

        private string GetUploadFile()
        {
            try
            {
                string[] filelist = Directory.GetFiles(_uploadDirectory, "*");
                if (filelist.Length > 0) return filelist[parentHandler.DoRandomNext(0, filelist.Length)];
                else return null;
            }
            catch { } //ignore any errors
            return null;
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
            RequestConfiguration config;
            Actions actions;

            switch (_state)
            {


                case "initial":
                    //these are only parsed once, global for the handler as handler can only have one entry.
                    if (handler.HandlerArgs.ContainsKey("sharepoint-version"))
                    {
                        _version = handler.HandlerArgs["sharepoint-version"].ToString();
                        //this needs to be extended in the future
                        if (_version != "2013")
                        {
                            parentHandler.DoLogTrace($"Sharepoint:: Unsupported Sharepoint version {_version} , sharepoint browser action will not be executed.");
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
                            _uploadDirectory = targetDir;
                        }
                    }

                    if (_uploadDirectory == null)
                    {
                        _uploadDirectory = KnownFolders.GetDownloadFolderPath();
                    }


                    if (_deletionProbability < 0 && handler.HandlerArgs.ContainsKey("sharepoint-deletion-probability"))
                    {
                        int.TryParse(handler.HandlerArgs["sharepoint-deletion-probability"].ToString(), out _deletionProbability);
                        if (!CheckProbabilityVar(handler.HandlerArgs["sharepoint-deletion-probability"].ToString(), _deletionProbability))
                        {
                            _deletionProbability = 0;
                        }
                    }
                    if (_uploadProbability < 0 && handler.HandlerArgs.ContainsKey("sharepoint-upload-probability"))
                    {
                        int.TryParse(handler.HandlerArgs["sharepoint-upload-probability"].ToString(), out _uploadProbability);
                        if (!CheckProbabilityVar(handler.HandlerArgs["sharepoint-upload-probability"].ToString(), _uploadProbability))
                        {
                            _uploadProbability = 0;
                        }
                    }
                    if (_downloadProbability < 0 && handler.HandlerArgs.ContainsKey("sharepoint-download-probability"))
                    {
                        int.TryParse(handler.HandlerArgs["sharepoint-download-probability"].ToString(), out (_downloadProbability));
                        if (!CheckProbabilityVar(handler.HandlerArgs["sharepoint-download-probability"].ToString(), _downloadProbability))
                        {
                            _downloadProbability = 0;
                        }
                    }

                    if ((_deletionProbability + _uploadProbability + _downloadProbability) > 100)
                    {
                        parentHandler.DoLogTrace($"Sharepoint:: The sum of the download/upload/deletion sharepoint probabilities is > 100 , sharepoint browser action will not be executed.");
                        parentHandler.sharepointAbort = true;
                        return;
                    }

                    if ((_deletionProbability + _uploadProbability + _downloadProbability) == 0)
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
                            _credentials = JsonConvert.DeserializeObject<Credentials>(System.IO.File.ReadAllText(credFname));
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
                                if (words[0] == "site") _site = words[1];
                                else if (words[0] == "credentialKey") credentialKey = words[1];
                            }
                        }
                    }

                    if (_site == null)
                    {
                        parentHandler.DoLogTrace($"Sharepoint:: The command args must specify a 'site:<value>' , sharepoint browser action will not be executed.");
                        parentHandler.sharepointAbort = true;
                        return;
                    }

                    //check if site starts with http:// or https:// 
                    _site = _site.ToLower();
                    string header = null;
                    Regex rx = new Regex("^http://.*", RegexOptions.Compiled);
                    var match = rx.Matches(_site);
                    if (match.Count > 0) header = "http://";
                    if (header == null)
                    {
                        rx = new Regex("^https://.*", RegexOptions.Compiled);
                        match = rx.Matches(_site);
                        if (match.Count > 0) header = "https://";
                    }
                    if (header != null)
                    {
                        _site = _site.Replace(header, "");
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

                    _username = _credentials.GetUsername(credentialKey);
                    _password = _credentials.GetPassword(credentialKey);

                    if (_username == null || _password == null)
                    {
                        parentHandler.DoLogTrace($"Sharepoint:: The credential key {credentialKey} does not return a valid credential from file {credFname},   sharepoint browser action will not be executed");
                        parentHandler.sharepointAbort = true;
                        return;
                    }


                    //have the username, password
                    string portal = _site;

                    string target = header + _username + ":" + _password + "@" + portal + "/";
                    config = RequestConfiguration.Load(handler, target);
                    try
                    {
                        parentHandler.MakeRequest(config);
                    }
                    catch (System.Exception e)
                    {
                        parentHandler.DoLogTrace($"Sharepoint:: Unable to parse site {_site}, url may be malformed. Sharepoint browser action will not be executed.");
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
                    _state = "execute";
                    break;

                case "execute":

                    //determine what to do
                    int choice = parentHandler.DoRandomNext(0, 101);
                    string sharepointAction = null;
                    int endRange;
                    int startRange = 0;

                    if (_deletionProbability > 0)
                    {
                        endRange = _deletionProbability;
                        if (choice >= startRange && choice <= endRange) sharepointAction = "delete";
                        else startRange = _deletionProbability + 1;
                    }

                    if (sharepointAction == null && _uploadProbability > 0)
                    {
                        endRange = startRange + _uploadProbability;
                        if (choice >= startRange && choice <= endRange) sharepointAction = "upload";
                        else startRange = _uploadProbability + 1;
                    }

                    if (sharepointAction == null && _downloadProbability > 0)
                    {
                        endRange = startRange + _downloadProbability;
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
                                parentHandler.DoLogTrace($"Sharepoint:: Downloaded file {fname} from site {_site}.");
                                Thread.Sleep(1000);
                            }
                        }
                        catch (Exception e)
                        {
                            parentHandler.DoLogTrace($"Sharepoint:: Error performing sharepoint download from site {_site}.");
                            parentHandler.DoLogError(e);
                        }
                    }
                    if (sharepointAction == "upload")
                    {
                        try
                        {
                            string fname = GetUploadFile();
                            if (fname == null)
                            {
                                parentHandler.DoLogTrace($"Sharepoint:: Cannot find a valid file to upload from directory {_uploadDirectory}.");
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
                            if (UploadBlocked())
                            {
                                parentHandler.DoLogTrace($"Sharepoint:: Failed to upload file {fname} to site {_site}.");
                            }
                            else
                            {
                                parentHandler.DoLogTrace($"Sharepoint:: Uploaded file {fname} to site {_site}.");
                            }


                        }
                        catch (Exception e)
                        {
                            parentHandler.DoLogTrace($"Sharepoint:: Error performing sharepoint upload to site {_site}.");
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
                                parentHandler.DoLogTrace($"Sharepoint:: Deleted file {fname} from site {_site}.");
                                Thread.Sleep(1000);
                            }
                            else
                            {
                                parentHandler.DoLogTrace($"Sharepoint:: No documents to delete from {_site}.");
                            }
                        }
                        catch (Exception e)
                        {
                            parentHandler.DoLogTrace($"Sharepoint:: Error performing sharepoint download from site {_site}.");
                            parentHandler.DoLogError(e);
                        }
                    }
                    break;




            }

        }


    }
}
