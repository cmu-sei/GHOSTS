using Ghosts.Client.Infrastructure;
using Ghosts.Client.Infrastructure.Email;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Newtonsoft.Json;
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium;
using System.Diagnostics;
using System;
using System.IO;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using Actions = OpenQA.Selenium.Interactions.Actions;

using Exception = System.Exception;
using NLog;

namespace Ghosts.Client.Handlers
{



    /// <summary>
    /// Supports Email for Outlook 2013
    /// Xpath tutorial: lambdatest.com/blog/complete-guide-for-using-xpath-in-selenium-with-examples
    /// This class just uses all of the base class methods
    /// </summary>
    public class OutlookHelper2013 : OutlookHelper
    {

        public OutlookHelper2013(BaseBrowserHandler callingHandler, IWebDriver callingDriver, string aversion)
        {
            base.Init(callingHandler, callingDriver, aversion);

        }


    }

    // This is used for Exchange Server 2016 also
    public class OutlookHelper2019 : OutlookHelper
    {

        public OutlookHelper2019(BaseBrowserHandler callingHandler, IWebDriver callingDriver, string aversion)
        {

            base.Init(callingHandler, callingDriver, aversion);
            NewMailXpath = "//span[text()='New']//parent::span//parent::button";
            ToRecipientsXpath = "//span[text()='To']//parent::button//following::div//child::input";
            CcRecipientsXpath = "//span[text()='Cc']//parent::button//following::div//child::input";
            SubjectXpath = "//input[@placeholder='Add a subject']";
            EmailBodyXpath = "//div[@aria-label='Message body']";
            SendButtonXpath = "//button[@aria-label='Send']";
            InsertAttachmentXpath = "//button[@aria-label='Attach']";
            EmailXpath = "//div[@aria-label='Mail list']//child::*[contains(@role,'listbox')]//child::div[contains(@id,'ariaId')]";
            EmailXpathSender = ".//span[contains(@class,'lvHighlightFromClass')]";
            EmptyFolderActionXpath = "//div[@role='menu' and @iscontextmenu='1']//child::span[text()='Empty folder']//parent::div//parent::div//parent::button";

        }



    }


    /// <summary>
    /// Handles Outlook Web actions for base browser handler
    /// Base class methods are for Exchange Server 2013
    /// </summary>
    public abstract class OutlookHelper : BrowserHelper
    {


        private int _deleteProbability = -1;
        private int _readProbability = -1;
        private int _replyProbability = -1;
        private int _createProbability = -1;

        public int attachmentProbability = 0;
        public int attachmentsMin = 0;
        public int attachmentsMax = 0;
        public int attachmentsMaxSize = 10;

        public int saveAttachmentProbability = 0;

        private Credentials _credentials = null;
        private string _state = "initial";
        public int errorCount = 0;
        public int errorThreshold = 10;  //after this many errors, restart the browser
        public string site { get; set; } = null;
        string username { get; set; } = null;
        string password { get; set; } = null;
        string domain { get; set; } = null;
        public string version { get; set; } = null;
        public string uploadDirectory { get; set; } = null;

        public System.Exception LastException;
        public string AttachmentWindowTitle = "Open"; //this is for chrome

        public string EmailDeleteXpath { get; set; } = "//span[text()='Delete']//parent::button";
        public string EmailReplySendXpath { get; set; } = "//button[@title='Send' and @aria-label='Send']";
        public string EmailXpath { get; set; } = "//div[contains(@tempid,'emailslistview')]//child::div[contains(@role,'button')]";
        public string EmailXpathSender { get; set; } = ".//span[contains(@class,'lvHighlightFromClass')]";
        public string EmailOtherXpath { get; set; } = "//div[contains(@aria-label,'Search completed')]//child::div[contains(@id,'_ariaId_')]";
        public string NewMailXpath { get; set; } = "//div[@aria-label='Mail']//child::div//child::div//child::div//child::div//child::button//child::span[@role='presentation']//following-sibling::span[text()='New mail']//parent::button";
        public string EmailFiltersXpath { get; set; } = "//div[contains(@aria-label,'Email Filters')]//child::div//child::div[contains(@style,'inline-block')]//child::span[contains(@role,'menuitemradio')]";
        public string MarkAsReadXpath { get; set; } = "//div[contains(@tempid,'ItemHeaderView.Mouse')]//child::span[text()='Mark as read']//parent::button";
        public string ToRecipientsXpath { get; set; } = "//input[contains(@aria-label,'To recipients.')]";
        public string CcRecipientsXpath { get; set; } = "//input[contains(@aria-label,'Cc recipients.')]";
        public string SubjectXpath { get; set; } = "//input[contains(@aria-labelledby,'MailCompose.SubjectWellLabel')]";
        public string EmailBodyXpath { get; set; } = "//div[contains(@id,'MicrosoftOWAEditorRegion')]";

        public string SendButtonXpath { get; set; } = "//div[contains(@tempid,'mailcomposetoolbar')]//child::button[contains(@aria-label,'Send')]";

        public string ReplyButtonXpath { get; set; } = "//span[@title='Reply']//parent::button";

        public string MessageBodyXpath { get; set; } = "//div[@id='Item.MessageUniqueBody']//child::p";

        public string DiscardXpath { get; set; } = "//div[contains(@aria-label,'Message Headers')]//child::span[text()='DISCARD']/parent::button/parent::div";
        public string DiscardButtonXpath { get; set; } = "//div[contains(@aria-label,'Message Headers')]//child::span[text()='DISCARD']/parent::button";

        public string MoreActionsXpath { get; set; } = "//div[contains(@aria-label,'Mail Actions')]//child::button[contains(@aria-label,'More Actions')]";
        public string DeleteActionXpath { get; set; } = "//div[contains(@class,'contextMenuDropShadow')]//child::span[contains(@aria-label,'Delete')]//parent::div//parent::div//parent::div[contains(@tabindex,'-1')]";

        public string EmptyFolderActionXpath { get; set; } = "//div[contains(@class,'contextMenuDropShadow')]//child::div[contains(@aria-label,'Empty folder')]";

        //these Xpaths are for selecting mail folders. They have to select by label, then go back up to the element that has the event on it.
        public string InboxFolderXpath { get; set; } = "//div[contains(@aria-label,'Folder Pane')]//child::span[contains(@title,'Inbox')]//parent::div//parent::span//parent::div[contains(@role,'treeitem')]";
        public string InboxCountXpath { get; set; } = "//div[contains(@aria-label,'Folder Pane')]//child::span[contains(@title,'Inbox')]//parent::div//parent::span//parent::div[contains(@role,'treeitem')]";

        public string SentFolderXpath { get; set; } = "//div[contains(@aria-label,'Folder Pane')]//child::span[contains(@title,'Sent')]//parent::div//parent::span//parent::div[contains(@role,'treeitem')]";
        public string DeletedFolderXpath { get; set; } = "//div[contains(@aria-label,'Folder Pane')]//child::span[contains(@title,'Delete')]//parent::div//parent::span//parent::div[contains(@role,'treeitem')]";
        public string DraftsFolderXpath { get; set; } = "//div[contains(@aria-label,'Folder Pane')]//child::span[contains(@title,'Drafts')]//parent::div//parent::span//parent::div[contains(@role,'treeitem')]";

        //public string AlertOkXpath { get; set; } = "//div[contains(@role,'alertdialog')]//child::span[text()='OK']//parent::button";

        public string AlertOkXpath { get; set; } = "//div[contains(@role,'alertdialog')]//child::span[text()='Empty folder']//parent::div//parent::div//child::span[text()='OK']//parent::button";

        public string AlertAttachmentXpath { get; set; } = "//div[contains(@role,'alertdialog')]//child::span[text()='Attachment reminder']//parent::div//parent::div//child::span[text()='Send']//parent::button";
        public string AlertSubjectXpath { get; set; } = "//div[contains(@role,'alertdialog')]//child::span[text()='Subject reminder']//parent::div//parent::div//child::span[text()='Send']//parent::button";

        public string InsertMenuXpath { get; set; } = "//div[contains(@tempid,'mailcomposeview')]//child::span[text()='INSERT']/parent::button";

        public string InsertAttachmentXpath { get; set; } = "//span[contains(@aria-label,'Insert attachment')]";


        public string FileDownloadAllXpath { get; set; } = "//span[text()='Download all']/parent::button";




        public List<string> InitialWindows = new List<string>();


        private void OutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            Log.Trace($"OutlookWeb:: STDOUT from bash process: {outLine.Data}");
            return;
        }

        private static void ErrorHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            Log.Trace($"OutlookWeb:: STDERR output from bash process: {outLine.Data}");
            return;
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
            Log.Trace($"OutlookWeb:: Spawning {p.StartInfo.FileName} with command {escapedArgs}");
            p.Start();

            string Result = "";
            while (!p.StandardOutput.EndOfStream)
            {
                Result += p.StandardOutput.ReadToEnd();
            }

            p.WaitForExit();
            Log.Trace($"OutlookWeb:: Bash command output: {Result}");
        }

        public static bool isWindowsOs()
        {
            var OsName = System.Runtime.InteropServices.RuntimeInformation.OSDescription;

            return OsName.Contains("Windows");
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

        public void AttachFileLinux(string filename)
        {
            string cmd = $"xdotool search -name '{AttachmentWindowTitle}' windowfocus type '{filename}' ";
            ExecuteBashCommand(cmd);
            Thread.Sleep(500);
            cmd = $"xdotool search -name '{AttachmentWindowTitle}' windowfocus key KP_Enter";
            ExecuteBashCommand(cmd);
            Thread.Sleep(300);
            return;
        }

        public void AttachFile(string filename)
        {
            if (isWindowsOs()) AttachFileWindows(filename);
            else AttachFileLinux(filename);
        }


        public static OutlookHelper MakeHelper(BaseBrowserHandler callingHandler, IWebDriver callingDriver, TimelineHandler handler, Logger tlog)
        {
            OutlookHelper helper = null;
            if (handler.HandlerArgs.ContainsKey("exchange-version"))
            {
                var version = handler.HandlerArgs["exchange-version"].ToString();
                //this needs to be extended in the future
                if (version == "2013") helper = new OutlookHelper2013(callingHandler, callingDriver, version);
                else if (version == "2019" || version == "2016") helper = new OutlookHelper2019(callingHandler, callingDriver, version);
                if (helper == null)
                {
                    Log.Trace($"WebOutlook:: Unsupported Exchange version {version} , outlook browser action will not be executed.");
                }
            }
            else
            {
                Log.Trace($"WebOutlook:: Handler option 'exchange-version' must be specified, currently supported versions: '2013'. Outlook browser action will not be executed.");

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

        private bool CheckProbabilityVar(string name, int value)
        {
            if (!(value >= 0 && value <= 100))
            {
                Log.Trace($"Variable {name} with value {value} must be an int between 0 and 100, setting to 0");
                return false;
            }
            return true;
        }

        private string GetRecipientString(List<string> targets)
        {

            string recipients = null;
            foreach (var a in targets)
            {
                var target = a.Trim();
                if (recipients == null) recipients = target;
                else if (!recipients.Contains(target)) recipients = recipients + ";" + target;

            }
            return recipients;
        }

        private void HandleAttachmentReminder()
        {
            try
            {
                var targetElement = Driver.FindElement(By.XPath(AlertAttachmentXpath));
                if (targetElement != null)
                {
                    BrowserHelperSupport.ElementClick(Driver, targetElement);
                    Thread.Sleep(500);
                }

            }

            catch (Exception e)
            {
                if (e is ThreadAbortException || e is ThreadInterruptedException)
                {
                    throw e;
                }
                //ignore
            }

        }

        private void HandleSubjectReminder()
        {
            try
            {
                var targetElement = Driver.FindElement(By.XPath(AlertSubjectXpath));
                if (targetElement != null)
                {
                    BrowserHelperSupport.ElementClick(Driver, targetElement);
                    Thread.Sleep(500);
                }

            }
            catch (Exception e)
            {
                if (e is ThreadAbortException || e is ThreadInterruptedException)
                {
                    throw e;
                }
                //ignore
            }

        }

        /// <summary>
        /// This uses the actual 'reply' button instead of doing what OutlookV2 does
        /// which is read the email body and quote. This uses the 'reply' button
        /// because it is diffcult to get the return address from the email web fields.
        /// </summary>
        /// <param name="handler"></param>
        /// <returns></returns>
        public bool DoReply(TimelineHandler handler)
        {
            try
            {
                if (!SelectFolder(InboxFolderXpath))
                {
                    Log.Trace($"WebOutlook:: Unable to select inbox folder, reply will not be sent.");
                    return false;
                }
                //first, select an email, do not mark as read
                if (!SelectOneEmail(false))
                {
                    Log.Trace($"WebOutlook:: Unable to select email, reply will not be sent.");
                    return false;
                }


                //Hit the reply button, this marks the email as read
                if (version == "2013")
                {
                    var emailListElements = Driver.FindElements(By.XPath(ReplyButtonXpath));
                    if (emailListElements == null)
                    {
                        Log.Trace($"WebOutlook:: Unable to find reply button in current mail, replywill not be sent.");
                        return false;
                    }
                    BrowserHelperSupport.ElementClick(Driver, emailListElements[0]);
                    Thread.Sleep(500);
                }
                else
                {
                    if (!DoCurrentEmailAction("Reply"))
                    {
                        Log.Trace($"WebOutlook:: Unable to find reply button in current mail, reply will not be sent.");
                        return false;
                    }
                }

                //compose the message and send
                var emailReply = new EmailReplyManager();  //this has the reply

                if (version == "2013")
                {
                    Driver.SwitchTo().Frame("EditorBody");
                    var targetElement = Driver.FindElement(By.XPath(EmailBodyXpath));
                    if (targetElement == null)
                    {
                        Log.Trace($"WebOutlook:: Unable to find body field in reply email form, mail will not be sent.");
                        return false;
                    }
                    if (Driver is OpenQA.Selenium.Chrome.ChromeDriver)
                    {
                        BrowserHelperSupport.ElementClick(Driver, targetElement);  //needed for Chrome
                        Thread.Sleep(300);
                    }
                    targetElement.SendKeys(emailReply.Reply);
                    Thread.Sleep(500);
                    //switch back to parent frame
                    Driver.SwitchTo().DefaultContent();
                    //send the email
                    targetElement = Driver.FindElement(By.XPath(SendButtonXpath));
                    if (targetElement == null)
                    {
                        Log.Trace($"WebOutlook:: Unable to find send button in reply email form, mail will not be sent.");
                        return false;
                    }
                    BrowserHelperSupport.ElementClick(Driver, targetElement);
                    Thread.Sleep(500);
                }
                else
                {
                    var targetElement = Driver.FindElement(By.XPath(EmailBodyXpath));
                    if (targetElement == null)
                    {
                        Log.Trace($"WebOutlook:: Unable to find body field in reply email form, mail will not be sent.");
                        return false;
                    }
                    targetElement.SendKeys(emailReply.Reply);
                    Thread.Sleep(500);
                    targetElement = Driver.FindElement(By.XPath(EmailReplySendXpath));
                    if (targetElement == null)
                    {
                        Log.Trace($"WebOutlook:: Unable to find send button in reply email form, mail will not be sent.");
                        return false;
                    }
                    BrowserHelperSupport.ElementClick(Driver, targetElement);
                    Thread.Sleep(500);
                }

                HandleAttachmentReminder();  //handle the attachment popup if present
                HandleSubjectReminder();

                Log.Trace($"WebOutlook:: Email reply successful.");
                return true;
            }
            catch (System.Exception e)
            {
                if (e is ThreadAbortException || e is ThreadInterruptedException)
                {
                    throw e;
                }
                Log.Error(e);
                return false;
            }
        }

        public bool DoInitialLogin(TimelineHandler handler, string user, string pw, string domain)
        {

            //have the username, password
            RequestConfiguration config;
            string target = site;
            config = RequestConfiguration.Load(handler, target);
            try
            {
                baseHandler.MakeRequest(config);
            }
            catch (System.Exception e)
            {
                if (e is ThreadAbortException || e is ThreadInterruptedException)
                {
                    throw e;
                }
                Log.Trace($"WebOutlook:: Unable to parse site {site}, url may be malformed. Outlook browser action will not be executed.");
                baseHandler.OutlookAbort = true;
                Log.Error(e);
                return false;

            }

            //before doing anything, check if are already logged in
            try
            {
                if (SelectFolder(InboxFolderXpath)) return true;   //if this works, already logged in
            }
            catch (System.Exception e)
            {
                if (e is ThreadAbortException || e is ThreadInterruptedException)
                {
                    throw e;
                }
                //ignore this error and continue to try to login

            }


            if (handler.HandlerType == HandlerType.BrowserFirefox)
            {
                //check for certificate 
                var targetElements = Driver.FindElements(By.XPath("//*[@class='certerror']"));
                if (targetElements != null && targetElements.Count > 0)
                {
                    //need to handle the certerror
                    BrowserHelperSupport.FirefoxHandleInsecureCertificate(Driver);
                }
            }


            var targetElement = Driver.FindElement(By.XPath("//input[@id='username']"));
            if (targetElement == null)
            {
                Log.Trace($"WebOutlook:: Unable to find username field for login, outlook browser action will not be executed.");
                baseHandler.OutlookAbort = true;
                return false;
            }
            targetElement.SendKeys($"{domain}\\{user}");
            Thread.Sleep(1000);
            targetElement = Driver.FindElement(By.XPath("//input[@id='password']"));
            if (targetElement == null)
            {
                Log.Trace($"WebOutlook:: Unable to find password field for login, outlook browser action will not be executed.");
                baseHandler.OutlookAbort = true;
                return false;
            }
            targetElement.SendKeys(pw);
            Thread.Sleep(1000);
            targetElement = Driver.FindElement(By.XPath("//div[@class='signinbutton']"));
            if (targetElement == null)
            {
                Log.Trace($"WebOutlook:: Unable to find signin button for login, outlook browser action will not be executed.");
                baseHandler.OutlookAbort = true;
                return false;
            }
            targetElement.Click();
            Thread.Sleep(1000);
            // check if the Language/Date setup page is displayed
            var optionElements = Driver.FindElements(By.XPath("//select[@name='tzid']//following::option"));
            if (optionElements != null && optionElements.Count > 0)
            {
                //use default language selection, select timezone
                bool found = false;
                foreach (IWebElement te in optionElements)
                {
                    string avalue = te.GetAttribute("value");
                    if (avalue.Contains("Central Standard Time"))
                    {
                        te.Click();
                        Thread.Sleep(500);
                        found = true;
                        break;
                    }
                }
                if (found)
                {
                    //click the save settings button
                    targetElement = Driver.FindElement(By.XPath("//div[@class='signInEnter']//child::div"));
                    if (targetElement != null)
                    {
                        Actions actions = new Actions(Driver);
                        actions.MoveToElement(targetElement).Click().Perform();
                        Thread.Sleep(500);
                    }
                }

            }
            // at this point, should be logged in. Check for main page
            var conductorElements = Driver.FindElements(By.XPath("//div[@class='conductorContent']"));
            if (conductorElements != null && conductorElements.Count > 0)
            {
                return true;
            }
            return false;
        }


        public bool SelectFolder(string FolderXpath)
        {
            // select the folder
            var targetElement = Driver.FindElement(By.XPath(FolderXpath));
            if (targetElement == null)
            {
                return false;
            }
            BrowserHelperSupport.MoveToElementAndClick(Driver, targetElement);
            Thread.Sleep(500);
            return true;
        }

        /// <summary>
        /// This handles file attachments, all errors are logged but ignored.
        /// </summary>
        public void HandleFileAttachments()
        {
            try
            {
                int numAttachments = _random.Next(attachmentsMin, attachmentsMax);
                if (numAttachments > 0)
                {
                    List<string> attachments = GetRandomFiles(uploadDirectory, "*", numAttachments, attachmentsMaxSize);
                    if (attachments != null)
                    {
                        //try adding these attachments
                        foreach (string FileToAttach in attachments)
                        {
                            if (version == "2013")
                            {
                                var insertElement = Driver.FindElement(By.XPath(InsertMenuXpath));
                                if (insertElement != null)
                                {
                                    BrowserHelperSupport.ElementClick(Driver, insertElement);
                                    Thread.Sleep(500);
                                    //now  click on the attachment choice
                                    insertElement = Driver.FindElement(By.XPath(InsertAttachmentXpath));
                                    if (insertElement != null)
                                    {
                                        BrowserHelperSupport.ElementClick(Driver, insertElement);
                                        Thread.Sleep(500);
                                        //filechoice window is open
                                        AttachFile(FileToAttach);
                                    }
                                }
                            }
                            else
                            {
                                var insertAttachmentElement = Driver.FindElement(By.XPath(InsertAttachmentXpath));
                                if (insertAttachmentElement != null)
                                {
                                    BrowserHelperSupport.ElementClick(Driver, insertAttachmentElement);
                                    Thread.Sleep(500);
                                    //filechoice window is open
                                    AttachFile(FileToAttach);
                                }
                            }
                        }
                    }
                }

            }
            catch (System.Exception e)
            {
                if (e is ThreadAbortException || e is ThreadInterruptedException)
                {
                    throw e;
                }
                //log error but continue on
                Log.Error(e);

            }

        }


        public bool DoCreate(TimelineHandler handler, EmailConfiguration emailConfig)
        {

            try
            {


                if (!SelectFolder(InboxFolderXpath))
                {
                    Log.Trace($"WebOutlook:: Unable to select inbox folder, email will not be sent.");
                    return false;
                }

                //get To Elements
                //Parse To
                string ToRecipients = null;
                if (emailConfig.To.Count > 0)
                {
                    ToRecipients = GetRecipientString(emailConfig.To);
                    Log.Trace($"WebOutlook:: TO {ToRecipients}");
                }
                else
                {
                    Log.Trace($"WebOutlook:: Must specify to-address.");
                    return false;
                }
                string CcRecipients = null;
                if (emailConfig.Cc.Count > 0)
                {
                    CcRecipients = GetRecipientString(emailConfig.Cc);
                    Log.Trace($"WebOutlook:: CC {CcRecipients}");
                }

                var targetElement = Driver.FindElement(By.XPath(NewMailXpath));
                if (targetElement == null)
                {
                    Log.Trace($"WebOutlook:: Unable to find new mail button, mail will not be sent.");
                    return false;
                }
                BrowserHelperSupport.ElementClick(Driver, targetElement);
                Thread.Sleep(500);

                //new mail form should be displayed

                if (attachmentProbability != 0 && _random.Next(0, 100) <= attachmentProbability)
                {
                    HandleFileAttachments();
                }


                targetElement = Driver.FindElement(By.XPath(ToRecipientsXpath));
                if (targetElement == null)
                {
                    Log.Trace($"WebOutlook:: Unable to find To: field in new email form, mail will not be sent.");
                    return false;
                }
                if (Driver is OpenQA.Selenium.Chrome.ChromeDriver)
                {
                    BrowserHelperSupport.ElementClick(Driver, targetElement);  //needed for Chrome
                    Thread.Sleep(300);
                }
                targetElement.SendKeys(ToRecipients);
                Thread.Sleep(500);
                Log.Trace($"WebOutlook:: Email To: field written");

                if (CcRecipients != null)
                {
                    targetElement = Driver.FindElement(By.XPath(CcRecipientsXpath));
                    if (targetElement == null)
                    {
                        Log.Trace($"WebOutlook:: Unable to find To: field in new email form, mail will not be sent.");
                        return false;
                    }
                    if (Driver is OpenQA.Selenium.Chrome.ChromeDriver)
                    {
                        BrowserHelperSupport.ElementClick(Driver, targetElement);  //needed for Chrome
                        Thread.Sleep(300);
                    }
                    targetElement.SendKeys(CcRecipients);
                    Thread.Sleep(500);
                    Log.Trace($"WebOutlook:: Email Cc: field written");

                }
                //Subject
                targetElement = Driver.FindElement(By.XPath(SubjectXpath));
                if (targetElement == null)
                {
                    Log.Trace($"WebOutlook:: Unable to find Subject: field in new email form, mail will not be sent.");
                    return false;
                }
                if (Driver is OpenQA.Selenium.Chrome.ChromeDriver)
                {
                    BrowserHelperSupport.ElementClick(Driver, targetElement);  //needed for Chrome
                    Thread.Sleep(300);
                }
                if (emailConfig.Subject == null || emailConfig.Subject == "")
                {
                    // do not have an empty subject
                    targetElement.SendKeys("Attention All");
                }
                else
                {
                    targetElement.SendKeys(emailConfig.Subject);
                }
                Log.Trace($"WebOutlook:: Email subject created.");

                Thread.Sleep(500);


                if (emailConfig.Body != null)
                {
                    //to send the message, have to switch iframe

                    if (version == "2013") Driver.SwitchTo().Frame("EditorBody");
                    targetElement = Driver.FindElement(By.XPath(EmailBodyXpath));
                    if (targetElement == null)
                    {
                        Log.Trace($"WebOutlook:: Unable to find body field in new email form, mail will not be sent.");
                        return false;
                    }
                    if (Driver is OpenQA.Selenium.Chrome.ChromeDriver)
                    {
                        BrowserHelperSupport.ElementClick(Driver, targetElement);  //needed for Chrome
                        Thread.Sleep(300);
                    }
                    targetElement.SendKeys(emailConfig.Body);
                    Thread.Sleep(500);
                    //switch back to parent frame
                    if (version == "2013") Driver.SwitchTo().DefaultContent();
                    Thread.Sleep(300);
                    Log.Trace($"WebOutlook:: Email body created.");
                }

                //send the email
                targetElement = Driver.FindElement(By.XPath(SendButtonXpath));
                if (targetElement == null)
                {
                    Log.Trace($"WebOutlook:: Unable to find send button in new email form, mail will not be sent.");
                    return false;
                }
                BrowserHelperSupport.ElementClick(Driver, targetElement);
                Thread.Sleep(500);
                HandleAttachmentReminder();  //handle the attachment popup if present
                HandleSubjectReminder();

                Log.Trace($"WebOutlook:: Email sent successful.");
                return true;

            }
            catch (System.Exception e)
            {
                if (e is ThreadAbortException || e is ThreadInterruptedException)
                {
                    throw e;
                }
                //log error and  return false
                Log.Error(e);
                return false;
            }
        }

        // Keep deleting elements until there are only 10 left in the folder
        // if reach 500 deleted, then return and wait for next deletion cycle.

        public bool DeleteItemsInFolder(string FolderName, string FolderXpath, string EmailSearchPath, bool DeleteAll, out int NumDeleted)
        {
            NumDeleted = 0;
            if (!SelectFolder(FolderXpath))
            {
                Log.Trace($"WebOutlook:: Unable to select {FolderName} folder, deletion not done.");
                return false;
            }


            while (true)
            {

                //select the first email from "ALL"
                SelectEmail("All");
                ReadOnlyCollection<IWebElement> emailElements = Driver.FindElements(By.XPath(EmailSearchPath));
                if (emailElements == null || emailElements.Count == 0) return true; //nothing to delete
                if (emailElements.Count <= 20) return true;   //quit when under 20
                //there is no good way to determine the total number of emails in the inbox.
                //just delete 15 emails per loop
                int MaxToDelete = 15;
                int count = 0;
                var elementToDelete = emailElements[0];
                while (count < MaxToDelete)
                {
                    //select the first email
                    BrowserHelperSupport.MoveToElementAndClick(Driver, elementToDelete);
                    Thread.Sleep(200);

                    bool UseDeleteMenu = true;
                    if (version == "2013")
                    {
                        //check for discard available. this will be for drafts
                        var DiscardElements = Driver.FindElements(By.XPath(DiscardXpath));
                        if (DiscardElements != null && DiscardElements.Count > 0)
                        {
                            var DiscardElement = DiscardElements[0];
                            var cattr = DiscardElement.GetAttribute("class");
                            if (cattr != "hidden")
                            {
                                UseDeleteMenu = false;
                                //get the actual button
                                DiscardElements = Driver.FindElements(By.XPath(DiscardButtonXpath));
                                if (DiscardElements != null && DiscardElements.Count > 0)
                                {
                                    BrowserHelperSupport.ElementClick(Driver, DiscardElements[0]);
                                    Thread.Sleep(500);
                                }

                            }
                        }
                    }

                    if (UseDeleteMenu)
                    {
                        if (version == "2013")
                        {
                            //get the delete menu
                            var targetElement = Driver.FindElement(By.XPath(MoreActionsXpath));
                            if (targetElement == null)
                            {
                                Log.Trace($"WebOutlook:: Unable to find MoreActions for current email, deletion not done.");
                                return false;
                            }
                            // bring up the more actions menu
                            BrowserHelperSupport.ElementClick(Driver, targetElement);
                            Thread.Sleep(200);
                            targetElement = Driver.FindElement(By.XPath(DeleteActionXpath));
                            if (targetElement == null)
                            {
                                Log.Trace($"WebOutlook:: Unable to find Delete action for current email, deletion not done.");
                                return false;
                            }
                            // delete the current email
                            BrowserHelperSupport.ElementClick(Driver, targetElement);
                            Thread.Sleep(500);
                        }
                        else
                        {
                            // 2019, 2016 has a handy delete button that can be used
                            var targetElement = Driver.FindElement(By.XPath(EmailDeleteXpath));
                            if (targetElement == null)
                            {
                                Log.Trace($"WebOutlook:: Unable to find Delete action for current email, deletion not done.");
                                return false;
                            }
                            // delete the current email
                            BrowserHelperSupport.ElementClick(Driver, targetElement);
                            Thread.Sleep(500);
                        }
                    }
                    count = count + 1;
                    NumDeleted = NumDeleted + 1;
                    if (NumDeleted > 500) return true;  //stop after 500  
                    //get next one
                    emailElements = Driver.FindElements(By.XPath(EmailSearchPath));
                    if (emailElements == null || emailElements.Count == 0) return true;
                    elementToDelete = emailElements[0];


                }
            }
        }

        public bool EmptyFolder(string FolderName, string FolderXpath)
        {
            // find the folder to empty
            var targetElement = Driver.FindElement(By.XPath(FolderXpath));
            if (targetElement == null)
            {
                Log.Trace($"WebOutlook:: Unable to find folder {FolderName} to empty, will not be emptied. ");
                return false;
            }
            //open folder context menu
            BrowserHelperSupport.MoveToElementAndContextMenu(Driver, targetElement);
            Thread.Sleep(500);

            // select empty folder choice
            targetElement = Driver.FindElement(By.XPath(EmptyFolderActionXpath));
            if (targetElement == null)
            {
                Log.Trace($"WebOutlook:: Unable to select folder {FolderName} to empty, will not be emptied. ");
                return false;
            }
            BrowserHelperSupport.MoveToElementAndClick(Driver, targetElement);
            Thread.Sleep(1000);


            //click  OK button on popup
            var targetElements = Driver.FindElements(By.XPath(AlertOkXpath));
            if (targetElements == null)
            {
                return false;
            }
            //there can be more than one of these, only one is active
            foreach (var anElement in targetElements)
            {
                try
                {
                    BrowserHelperSupport.MoveToElementAndClick(Driver, anElement);
                    Thread.Sleep(300);
                }
                catch (System.Exception e)
                {
                    if (e is ThreadAbortException || e is ThreadInterruptedException)
                    {
                        throw e;
                    }
                    Log.Error(e);
                }

            }


            return true;
        }

        public bool DoDelete(TimelineHandler handler)
        {
            try
            {
                int NumDeleted = 0;
                DeleteItemsInFolder("Inbox", InboxFolderXpath, EmailXpath, false, out NumDeleted);
                if (NumDeleted > 0)
                {
                    Log.Trace($"WebOutlook:: Successfully deleted {NumDeleted} items from inbox. ");
                    // only do this if we had to delete something
                    EmptyFolder("Sent Items", SentFolderXpath);
                    EmptyFolder("Drafts", DraftsFolderXpath);
                    EmptyFolder("Deleted Items", DeletedFolderXpath);
                }
                return true;
            }
            catch (System.Exception e)
            {
                if (e is ThreadAbortException || e is ThreadInterruptedException)
                {
                    throw e;
                }
                Log.Error(e);
                return false;
            }
        }


        public void RemoveLastFilter()
        {
            try
            {
                string RemoveFilterXpath = "//div[contains(@class,'folderHeadContainer')]//child::span[contains(@class,'ms-Icon--x')]//parent::button";
                var RemoveFilterElement = Driver.FindElement(By.XPath(RemoveFilterXpath));

                if (RemoveFilterElement != null)
                {
                    BrowserHelperSupport.ElementClick(Driver, RemoveFilterElement);
                    Thread.Sleep(300);
                }
            }
            catch (System.Exception e)
            {
                if (e is ThreadAbortException || e is ThreadInterruptedException)
                {
                    throw e;
                }
                //ignore this error as Filter may not be selected
            }
        }

        public IWebElement GetFilterElement()
        {
            string FilterXpath = "//span[text()='Filter']//parent::button";
            try
            {
                return Driver.FindElement(By.XPath(FilterXpath));
            }
            catch (System.Exception e)
            {
                if (e is ThreadAbortException || e is ThreadInterruptedException)
                {
                    throw e;
                }
                //ignore
            }
            return null;
        }


        public void SelectEmail(string mailType)
        {
            try
            {
                if (version == "2013")
                {
                    var menuElements = Driver.FindElements(By.XPath(EmailFiltersXpath));
                    if (menuElements != null && menuElements.Count > 0)
                    {
                        foreach (var menuElement in menuElements)
                        {
                            string menuText = menuElement.Text;
                            if (mailType == menuText)
                            {
                                BrowserHelperSupport.ElementClick(Driver, menuElement);
                                break;
                            }
                        }
                    }
                }
                else
                {
                    //Try finding the menu 'Filter' element
                    var MenuFilter = GetFilterElement();
                    //if null, try removing last filter
                    if (MenuFilter == null)
                    {
                        RemoveLastFilter();
                        //try again
                        MenuFilter = GetFilterElement();
                    }
                    if (MenuFilter != null)
                    {
                        //click on the menu filter
                        BrowserHelperSupport.ElementClick(Driver, MenuFilter);
                        Thread.Sleep(300);
                        //now select a menu item
                        string MenuItemXpath = $"//div[@role='menu' and @iscontextmenu='1']//child::span[text()='{mailType}']//parent::div//parent::div//parent::button";
                        var MenuItemElement = Driver.FindElement(By.XPath(MenuItemXpath));
                        BrowserHelperSupport.ElementClick(Driver, MenuItemElement);
                        Thread.Sleep(300);
                    }
                }
            }
            catch (System.Exception e)
            {
                if (e is ThreadAbortException || e is ThreadInterruptedException)
                {
                    throw e;
                }
                Log.Trace($"WebOutlook:: Error selecting email filter {mailType}.");
                Log.Error(e);
            }
        }


        public IWebElement GetOneEmailFromCurrentList()
        {

            var settings = Program.Configuration.Email;
            string[] EmailNoReply = null;
            if (settings.EmailNoReply != null && settings.EmailNoReply != "")
            {
                EmailNoReply = settings.EmailNoReply.ToLower().Split(',');
            }
            ReadOnlyCollection<IWebElement> emailElements = Driver.FindElements(By.XPath(EmailXpath));
            if (emailElements != null && emailElements.Count > 0)
            {
                if (EmailNoReply == null)
                {
                    // no filtering based on reply address
                    //select one of the first 5
                    int max = emailElements.Count;
                    if (max > 5) max = 5;
                    int choice = _random.Next(0, max);
                    return emailElements[choice];
                }
                else
                {
                    // have to work harder, filter by return address
                    List<IWebElement> targetEmails = new List<IWebElement>();
                    var count = 0;
                    foreach (IWebElement emailElement in emailElements)
                    {
                        try
                        {
                            var replyElement = emailElement.FindElement(By.XPath(EmailXpathSender));
                            if (replyElement != null)
                            {
                                var sender = replyElement.Text.ToLower();
                                // check if this sender is ok
                                bool reject = false;
                                foreach (string target in EmailNoReply)
                                {
                                    if (sender.Contains(target))
                                    {
                                        reject = true;
                                        break;
                                    }
                                }
                                if (!reject)
                                {
                                    targetEmails.Add(emailElement);
                                    if (targetEmails.Count > 5) break;
                                }
                                count += 1;
                                if (count > 50) break;
                            }

                        }
                        catch (Exception e)
                        {
                            //ignore any exceptions
                        }

                    }

                    // filtering is done
                    if (targetEmails.Count == 0)
                    {
                        Log.Trace($"WebOutlook:: Unable to find valid email to reply to because of EmailNoReply filter..");
                        return null; //unable to find valid email
                    }
                    else
                    {
                        //return a random one out of the list
                        int choice = _random.Next(0, targetEmails.Count + 1);
                        return targetEmails[choice];
                    }

                }
            }

            return null;
        }

        public bool DoCurrentEmailAction(string action)
        {

            string MenuMoreActionsXpath = "//button[@aria-label='More Actions']";
            IWebElement targetElement = null;
            try
            {
                targetElement = Driver.FindElement(By.XPath(MenuMoreActionsXpath));
                if (targetElement != null)
                {
                    //bring up the menu
                    BrowserHelperSupport.ElementClick(Driver, targetElement);
                    Thread.Sleep(300);
                    var MenuItemXpath = $"//div[@role='menu' and @iscontextmenu='1']//child::span[text()='{action}']//parent::div//parent::button";
                    targetElement = Driver.FindElement(By.XPath(MenuItemXpath));
                    if (targetElement != null)
                    {
                        BrowserHelperSupport.ElementClick(Driver, targetElement);
                        Thread.Sleep(300);
                        return true;
                    }
                }
            }
            catch (System.Exception e)
            {
                if (e is ThreadAbortException || e is ThreadInterruptedException)
                {
                    throw e;
                }
                Log.Trace($"WebOutlook:: Error performing current email action:  {action}.");
                Log.Error(e);
                return false;
            }
            return false;
        }



        public bool MarkCurrentEmailAsRead()
        {
            if (version == "2013")
            {
                try
                {
                    IWebElement targetElement = null;
                    try
                    {
                        targetElement = Driver.FindElement(By.XPath(MarkAsReadXpath));
                    }
                    catch (System.Exception e)
                    {
                        if (e is ThreadAbortException || e is ThreadInterruptedException)
                        {
                            throw e;
                        }
                        //ignore may not be present
                    }
                    if (targetElement != null)
                    {
                        BrowserHelperSupport.ElementClick(Driver, targetElement);
                        Thread.Sleep(300);
                        return true;
                    }
                }
                catch (System.Exception e)
                {
                    if (e is ThreadAbortException || e is ThreadInterruptedException)
                    {
                        throw e;
                    }
                    Log.Trace($"WebOutlook:: Error marking email as read.");
                    Log.Error(e);
                }
                return false;
            }
            else
            {
                return DoCurrentEmailAction("Mark as read");
            }
        }

        public void DownloadAttachmentsCurrentEmail()
        {
            try
            {
                IWebElement targetElement = null;
                if (saveAttachmentProbability > _random.Next(0, 100))
                {
                    try
                    {
                        targetElement = Driver.FindElement(By.XPath(FileDownloadAllXpath));
                    }
                    catch (System.Exception e)
                    {
                        if (e is ThreadAbortException || e is ThreadInterruptedException)
                        {
                            throw e;
                        }
                        //ignore may not be present
                    }

                    if (targetElement != null)
                    {
                        BrowserHelperSupport.ElementClick(Driver, targetElement);
                        Thread.Sleep(300);
                        Log.Trace($"WebOutlook:: Attachments downloads successfull.");
                    }
                }
            }
            catch (System.Exception e)
            {
                if (e is ThreadAbortException || e is ThreadInterruptedException)
                {
                    throw e;
                }
                //ignore button present but may not work
            }
        }

        public bool SelectOneEmail(bool MarkAsRead)
        {
            //select the Unread Email
            SelectEmail("Unread");
            var emailElement = GetOneEmailFromCurrentList();
            if (emailElement != null)
            {
                //select this email
                BrowserHelperSupport.MoveToElementAndClick(Driver, emailElement);
                Thread.Sleep(500);
                //this is an unread email that was just selected, mark as read
                if (MarkAsRead)
                {
                    // before marking as read, download the attachments
                    DownloadAttachmentsCurrentEmail();
                    MarkCurrentEmailAsRead();

                }
                return true;
            }
            // if we get here, no unread email
            SelectEmail("All");
            emailElement = GetOneEmailFromCurrentList();
            if (emailElement != null)
            {
                //select this email
                BrowserHelperSupport.MoveToElementAndClick(Driver, emailElement);
                Thread.Sleep(500);
                return true;
            }
            return false;
        }

        public bool DoRead(TimelineHandler handler)
        {
            try
            {
                if (!SelectFolder(InboxFolderXpath))
                {
                    Log.Trace($"WebOutlook:: Unable to select inbox folder, email will not be read.");
                    return false;
                }
                // successful if an exception is not thrown
                SelectOneEmail(true);
                Log.Trace($"WebOutlook:: Email read successful.");
                return true;
            }
            catch (System.Exception e)
            {
                if (e is ThreadAbortException || e is ThreadInterruptedException)
                {
                    throw e;
                }
                Log.Error(e);
                return false;
            }
        }

        private string GetNextAction()
        {
            int choice = _random.Next(0, 101);
            string action = null;
            int endRange;
            int startRange = 0;

            if (_deleteProbability > 0)
            {
                endRange = _deleteProbability;
                if (choice >= startRange && choice <= endRange) action = "delete";
                else startRange = endRange + 1;
            }

            if (action == null && _createProbability > 0)
            {
                endRange = startRange + _createProbability;
                if (choice >= startRange && choice <= endRange) action = "create";
                else startRange = endRange + 1;
            }

            if (action == null && _readProbability > 0)
            {
                endRange = startRange + _readProbability;
                if (choice >= startRange && choice <= endRange) action = "read";
                else startRange = endRange + 1;

            }
            if (action == null && _replyProbability > 0)
            {
                endRange = startRange + _replyProbability;
                if (choice >= startRange && choice <= endRange) action = "reply";
                else startRange = endRange + 1;

            }
            return action;

        }

        private void setProbabilityDefaults()
        {
            _deleteProbability = 10;
            _readProbability = 30;
            _replyProbability = 30;
            _createProbability = 30;
        }

        /// <summary>
        /// This supports only one exchange site because it remembers context between runs. Different handlers should be used for different sites
        /// On the first execution, login is done to the site, then successive runs keep the login.
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="timelineEvent"></param>
        public void Execute(TimelineHandler handler, TimelineEvent timelineEvent)
        {
            string credFname;
            string credentialKey = null;
            EmailConfiguration emailConfig;

            try
            {

                switch (_state)
                {


                    case "initial":
                        //these are only parsed once, global for the handler as handler can only have one entry.



                        version = "2013";
                        if (handler.HandlerArgs.ContainsKey("exchange-version"))
                        {
                            version = handler.HandlerArgs["exchange-version"].ToString();
                        }

                        if (handler.HandlerArgs.ContainsKey("outlook-uploads-directory"))
                        {
                            string targetDir = handler.HandlerArgs["outlook-uploads-directory"].ToString();
                            targetDir = Environment.ExpandEnvironmentVariables(targetDir);
                            if (!Directory.Exists(targetDir))
                            {
                                Log.Trace($"WebOutlook:: upload directory {targetDir} does not exist, using browser downloads directory.");
                            }
                            else
                            {
                                uploadDirectory = targetDir;
                            }
                        }

                        if (uploadDirectory == null)
                        {
                            uploadDirectory = KnownFolders.GetDownloadFolderPath();
                        }


                        if (_deleteProbability < 0 && handler.HandlerArgs.ContainsKey("outlook-delete-probability"))
                        {
                            int.TryParse(handler.HandlerArgs["outlook-delete-probability"].ToString(), out _deleteProbability);
                            if (!CheckProbabilityVar(handler.HandlerArgs["outlook-delete-probability"].ToString(), _deleteProbability))
                            {
                                _deleteProbability = 0;
                            }
                        }

                        if (_createProbability < 0 && handler.HandlerArgs.ContainsKey("outlook-create-probability"))
                        {
                            int.TryParse(handler.HandlerArgs["outlook-create-probability"].ToString(), out _createProbability);
                            if (!CheckProbabilityVar(handler.HandlerArgs["outlook-create-probability"].ToString(), _createProbability))
                            {
                                _createProbability = 0;
                            }
                        }

                        if (_replyProbability < 0 && handler.HandlerArgs.ContainsKey("outlook-reply-probability"))
                        {
                            int.TryParse(handler.HandlerArgs["outlook-reply-probability"].ToString(), out _replyProbability);
                            if (!CheckProbabilityVar(handler.HandlerArgs["outlook-reply-probability"].ToString(), _replyProbability))
                            {
                                _replyProbability = 0;
                            }
                        }

                        if (_readProbability < 0 && handler.HandlerArgs.ContainsKey("outlook-read-probability"))
                        {
                            int.TryParse(handler.HandlerArgs["outlook-read-probability"].ToString(), out _readProbability);
                            if (!CheckProbabilityVar(handler.HandlerArgs["outlook-read-probability"].ToString(), _readProbability))
                            {
                                _readProbability = 0;
                            }
                        }


                        if ((_deleteProbability + _readProbability + _createProbability + _replyProbability) > 100)
                        {
                            Log.Trace($"WebOutlook:: The sum of the delete/read/create/reply outlook probabilities is > 100 , reverting to defaults.");
                            setProbabilityDefaults();
                        }

                        if ((_deleteProbability + _readProbability + _createProbability + _replyProbability) == 0)
                        {
                            Log.Trace($"WebOutlook:: The sum of the delete/read/create/reply outlook probabilities == 0 , reverting to defaults.");
                            setProbabilityDefaults();
                        }
                        if (handler.HandlerArgs.ContainsKey("delay-jitter"))
                        {
                            baseHandler.JitterFactor = Jitter.JitterFactorParse(handler.HandlerArgs["delay-jitter"].ToString());
                        }

                        if (handler.HandlerArgs.ContainsKey("outlook-save-attachment-probability"))
                        {
                            int.TryParse(handler.HandlerArgs["outlook-save-attachment-probability"].ToString(), out saveAttachmentProbability);
                            if (!CheckProbabilityVar(handler.HandlerArgs["outlook-save-attachment-probability"].ToString(), saveAttachmentProbability))
                            {
                                saveAttachmentProbability = 0;
                            }
                        }

                        if (handler.HandlerArgs.ContainsKey("outlook-attachment-probability"))
                        {
                            int.TryParse(handler.HandlerArgs["outlook-attachment-probability"].ToString(), out attachmentProbability);
                            if (!CheckProbabilityVar(handler.HandlerArgs["outlook-attachment-probability"].ToString(), attachmentProbability))
                            {
                                attachmentProbability = 0;
                            }
                        }

                        if (handler.HandlerArgs.ContainsKey("outlook-max-attachments-size"))
                        {
                            int.TryParse(handler.HandlerArgs["outlook-max-attachments-size"].ToString(), out attachmentsMaxSize);
                            if (attachmentsMaxSize <= 0)
                            {
                                attachmentsMaxSize = 10;
                            }
                        }

                        if (handler.HandlerArgs.ContainsKey("outlook-min-attachments"))
                        {
                            int.TryParse(handler.HandlerArgs["outlook-min-attachments"].ToString(), out attachmentsMin);
                            if (attachmentsMin < 0)
                            {
                                attachmentsMin = 1;
                            }
                        }

                        if (handler.HandlerArgs.ContainsKey("outlook-max-attachments"))
                        {
                            int.TryParse(handler.HandlerArgs["outlook-max-attachments"].ToString(), out attachmentsMax);
                            if (attachmentsMax < 0)
                            {
                                attachmentsMax = 10;
                            }
                        }

                        credFname = handler.HandlerArgs["outlook-credentials-file"].ToString();

                        if (handler.HandlerArgs.ContainsKey("outlook-credentials-file"))
                        {

                            try
                            {
                                _credentials = JsonConvert.DeserializeObject<Credentials>(System.IO.File.ReadAllText(credFname));
                            }
                            catch (System.Exception e)
                            {
                                Log.Trace($"WebOutlook:: Error parsing outlook credentials file {credFname} , outlook browser action will not be executed.");
                                baseHandler.OutlookAbort = true;
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
                                    if (words[0] == "site") site = words[1];
                                    else if (words[0] == "credentialKey") credentialKey = words[1];
                                }
                            }
                        }

                        if (handler.HandlerArgs.ContainsKey("outlook-url"))
                        {
                            site = handler.HandlerArgs["outlook-url"].ToString();
                        }


                        if (site == null)
                        {
                            Log.Trace($"WebOutlook:: The handler args must specify a 'outlook-url' , outlook browser action will not be executed.");
                            baseHandler.OutlookAbort = true;
                            return;
                        }

                        site = site.ToLower();

                        if (handler.HandlerArgs.ContainsKey("outlook-credential-key"))
                        {
                            credentialKey = handler.HandlerArgs["outlook-credential-key"].ToString();
                        }


                        if (credentialKey == null)
                        {
                            Log.Trace($"WebOutlook:: The handler args must specify a 'outlook-credential-key' value, outlook browser action will not be executed.");
                            baseHandler.OutlookAbort = true;
                            return;
                        }

                        username = _credentials.GetUsername(credentialKey);
                        password = _credentials.GetPassword(credentialKey);
                        domain = _credentials.GetDomain(credentialKey);

                        if (username == null || password == null || domain == null)
                        {
                            Log.Trace($"WebOutlook:: The credential key {credentialKey} does not return a valid credential from file {credFname}, username or password or domain is null,   outlook browser action will not be executed");
                            baseHandler.OutlookAbort = true;
                            return;
                        }

                        //have username, password - do the initial login
                        while (!DoInitialLogin(handler, username, password, domain))
                        {
                            //login failed, keep trying every 5 minutes in case it is a server startup problem
                            Log.Trace($"WebOutlook:: Login failed, sleeping and trying again.");
                            Thread.Sleep(300 * 1000);
                        }

                        //at this point we are logged in, files tab selected, ready for action
                        _state = "execute";

                        if (Driver is OpenQA.Selenium.Firefox.FirefoxDriver)
                        {
                            AttachmentWindowTitle = "File Upload";
                        }

                        foreach (var winname in Driver.WindowHandles)
                        {
                            InitialWindows.Add(winname);
                        }

                        break;

                    //todo: Keep track of number of errors. Reset after good operation
                    //if number of errors gets too large, abort
                    case "execute":

                        //close any windows not in initial window set
                        foreach (var winname in Driver.WindowHandles)
                        {
                            if (!InitialWindows.Contains(winname))
                            {
                                Driver.SwitchTo().Window(winname).Close();
                            }
                        }
                        Driver.SwitchTo().Window(InitialWindows[0]);


                        //determine what to do
                        string action = GetNextAction();



                        if (action == null)
                        {
                            //nothing to do this cycle
                            Log.Trace($"WebOutlook:: Action is skipped for this cycle.");
                            return;
                        }

                        bool success = true;

                        Log.Trace($"WebOutlook:: Starting action: {action}.");
                        if (action == "create")
                        {
                            emailConfig = new EmailConfiguration(timelineEvent.CommandArgs);
                            success = DoCreate(handler, emailConfig);
                        }

                        if (action == "delete")
                        {
                            success = DoDelete(handler);
                        }

                        if (action == "read")
                        {
                            success = DoRead(handler);
                        }
                        if (action == "reply")
                        {
                            success = DoReply(handler);
                        }
                        if (success)
                        {
                            errorCount = 0; //zero the error count
                            Log.Trace($"WebOutlook:: Completed action: {action}.");
                        }
                        else
                        {
                            Log.Trace($"WebOutlook:: Failed action: {action}.");
                            errorCount = errorCount + 1;
                        }

                        this.baseHandler.Report(new ReportItem { Handler = $"WebOutlook: {handler.HandlerType.ToString()}", Command = action, Arg = "" , Trackable = timelineEvent.TrackableId });

                        break;

                }


            }
            catch (System.Exception e)
            {
                if (e is ThreadAbortException || e is ThreadInterruptedException)
                {
                    throw e;
                }
                errorCount = errorThreshold + 1;  // an exception at  this level needs a restart
                LastException = e;  //save last exception so that it can be thrown up during restart
                Log.Trace($"WebOutlook:: Error at top level of execute loop.");
                Log.Error(e);
            }

        }

    }




}