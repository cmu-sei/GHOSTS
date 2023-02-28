// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Client.Infrastructure.Email;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Microsoft.Office.Interop.Outlook;
using Redemption;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Ghosts.Domain.Code.Helpers;
using Exception = System.Exception;
using MAPIFolder = Microsoft.Office.Interop.Outlook.MAPIFolder;
using Ghosts.Client.Infrastructure;
using NPOI.SS.Formula.Functions;


namespace Ghosts.Client.Handlers;

public class Outlookv2 : BaseHandler
{
    private readonly Application _app;
    private readonly NameSpace _oMapiNamespace;
    private readonly MAPIFolder _folderOutbox;
    private readonly MAPIFolder _folderInbox;
    

    //primary actions - delete, send, reply, read
    private int _deleteProbability = 25;  //cleanup email
    private int _createProbability = 25;    //create new email
    private int _replyProbability = 25;     //reply to existing email
    private int _readProbability = 25;      //read an unread email

    //additional actions
    private int _uploadProbability = 0;   //when creating, probability to add an attachment
    private int _downloadProbability = 0; //when reading, probability to download an attachment
    private int _clickProbability = 0;   //when reading, probability to click on a link
    private int _attachmentProbability = 0;   //when creating, probability to add an attachment
    private int _saveattachmentProbability = 0;   //when reading, probability to save attachment to disk

    private string _inputDirectory = null;
    private string _outputDirectory = null;
    private int _jitterfactor = 0;  //used with Jitter.JitterFactorDelay
    private int _attachmentsMax = 0;
    private int _attachmentsMin = 0;

    private string[] _actionList = { "read", "reply", "create", "delete" };
    private int[] _probabilityList = { 0, 0, 0, 0 };

    public Outlookv2(TimelineHandler handler)
    {
        try
        {
            base.Init(handler);
            //redemption prep
            //tell the app where the 32 and 64 bit dlls are located
            //by default, they are assumed to be in the same folder as the current assembly and be named
            //Redemption.dll and Redemption64.dll.
            //In that case, you do not need to set the two properties below
            var currentDir = new FileInfo(GetType().Assembly.Location).Directory;
            RedemptionLoader.DllLocation64Bit = Path.GetFullPath(currentDir + @"\lib\redemption64.dll");
            RedemptionLoader.DllLocation32Bit = Path.GetFullPath(currentDir + @"\lib\redemption.dll");
            //Create a Redemption object and use it
            Log.Trace("Creating new RDO session");
            var session = RedemptionLoader.new_RDOSession();
            Log.Trace("Attempting RDO session logon...");
            session.Logon(Missing.Value, Missing.Value, Missing.Value, Missing.Value, Missing.Value, Missing.Value);
        }
        catch (Exception e)
        {
            Log.Error($"RDO load error: {e}");
        }

        try
        {
            _app = new Application();
            _oMapiNamespace = _app.GetNamespace("MAPI");
            _folderInbox = _oMapiNamespace.GetDefaultFolder(OlDefaultFolders.olFolderInbox);
            _folderOutbox = _oMapiNamespace.GetDefaultFolder(OlDefaultFolders.olFolderOutbox);
            Log.Trace("Launching Outlook");
            _folderInbox.Display();

            if (handler.Loop)
            {
                while (true)
                {
                    ExecuteEvents(handler);
                }
            }
            else
            {
                ExecuteEvents(handler);
            }
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
    }

    public void ExecuteEvents(TimelineHandler handler)
    {
        ParseHandlerArgs(handler);
        _probabilityList[0] = _readProbability;
        _probabilityList[1] = _replyProbability;
        _probabilityList[2] = _createProbability;
        _probabilityList[3] = _deleteProbability;


        try
        {
            foreach (var timelineEvent in handler.TimeLineEvents)
            {
                var action = SelectActionFromProbabilities(_probabilityList, _actionList);
                if (action == null)
                {
                    Log.Trace("Outlookv2:: No action this cycle.");
                }
                else
                {
                    Infrastructure.WorkingHours.Is(handler);


                    if (timelineEvent.DelayBefore > 0)
                    {
                        Log.Trace($"DelayBefore sleeping for {timelineEvent.DelayBefore} ms");
                        Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayBefore, _jitterfactor));
                    }

                    Log.Trace($"Outlookv2:: Performing action {action} .");

                    switch (action)
                    {
                        case "create":
                            try
                            {
                                var emailConfig = new EmailConfiguration(timelineEvent.CommandArgs);
                                if (SendEmailViaOutlook(emailConfig))
                                {
                                    Report(handler.HandlerType.ToString(), timelineEvent.Command, emailConfig.ToString());
                                    Log.Trace("Outlookv2:: Created email");
                                }
                            }
                            catch (Exception e)
                            {
                                Log.Error(e);
                            }

                            break;
                        case "reply":
                            try
                            {
                                var emailConfig = new EmailConfiguration(timelineEvent.CommandArgs);
                                if (ReplyViaOutlook(emailConfig))
                                {
                                    Report(handler.HandlerType.ToString(), timelineEvent.Command, emailConfig.ToString());
                                    Log.Trace("Outlookv2:: Replied email");
                                }
                            }
                            catch (Exception e)
                            {
                                Log.Error(e);
                            }
                            break;
                        case "read":
                            try
                            {
                                if (ReadViaOutlook())
                                {
                                    Log.Trace("Outlookv2:: Read email");
                                }
                            }
                            catch (Exception e)
                            {
                                Log.Error(e);
                            }
                            break;
                        case "delete":
                            try
                            {
                                if (DeleteViaOutlook())
                                {
                                    Log.Trace("Outlookv2:: Deleted email");
                                }
                            }
                            catch (Exception e)
                            {
                                Log.Error(e);
                            }
                            break;

                    }

                    if (timelineEvent.DelayAfter > 0)
                    {
                        Log.Trace($"DelayAfter sleeping for {timelineEvent.DelayAfter} ms");
                        Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfter, _jitterfactor));
                    }
                }
            }
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
    }

    private void ParseHandlerArgs(TimelineHandler handler)
    {
        if (handler.HandlerArgs.ContainsKey("read-probability"))
        {
            int.TryParse(handler.HandlerArgs["read-probability"].ToString(), out _readProbability);
            if (!CheckProbabilityVar(handler.HandlerArgs["read-probability"].ToString(), _readProbability))
            {
                _readProbability = 0;
            }
        }
        if (handler.HandlerArgs.ContainsKey("delete-probability"))
        {
            int.TryParse(handler.HandlerArgs["delete-probability"].ToString(), out _deleteProbability);
            if (!CheckProbabilityVar(handler.HandlerArgs["delete-probability"].ToString(), _deleteProbability))
            {
                _deleteProbability = 0;
            }
        }
        if (handler.HandlerArgs.ContainsKey("reply-probability"))
        {
            int.TryParse(handler.HandlerArgs["reply-probability"].ToString(), out _replyProbability);
            if (!CheckProbabilityVar(handler.HandlerArgs["reply-probability"].ToString(), _replyProbability))
            {
                _replyProbability = 0;
            }
        }
        if (handler.HandlerArgs.ContainsKey("create-probability"))
        {
            int.TryParse(handler.HandlerArgs["create-probability"].ToString(), out _createProbability);
            if (!CheckProbabilityVar(handler.HandlerArgs["create-probability"].ToString(), _createProbability))
            {
                _createProbability = 0;
            }
        }
       
        if ((_readProbability + _deleteProbability + _createProbability + _replyProbability) > 100)
        {
            Log.Trace($"Outlookv2:: The sum of the read/delete/create/reply probabilities is > 100 , using defaults.");
            setProbabilityDefaults();

        }

        if ((_readProbability + _deleteProbability + _createProbability + _replyProbability) == 0)
        {
            Log.Trace($"Outlookv2:: The sum of the read/delete/create/reply probabilities == 0 , using defaults.");
            setProbabilityDefaults();
        }

        if (handler.HandlerArgs.ContainsKey("click-probability"))
        {
            int.TryParse(handler.HandlerArgs["click-probability"].ToString(), out _clickProbability);
            if (!CheckProbabilityVar(handler.HandlerArgs["click-probability"].ToString(), _clickProbability))
            {
                _clickProbability = 0;
            }
        }

        if (handler.HandlerArgs.ContainsKey("attachment-probability"))
        {
            int.TryParse(handler.HandlerArgs["attachment-probability"].ToString(), out _attachmentProbability);
            if (!CheckProbabilityVar(handler.HandlerArgs["attachment-probability"].ToString(), _attachmentProbability))
            {
                _attachmentProbability = 0;
            }
        }

        if (handler.HandlerArgs.ContainsKey("save-attachment-probability"))
        {
            int.TryParse(handler.HandlerArgs["save-attachment-probability"].ToString(), out _saveattachmentProbability);
            if (!CheckProbabilityVar(handler.HandlerArgs["save-attachment-probability"].ToString(), _saveattachmentProbability))
            {
                _saveattachmentProbability = 0;
            }
        }


        if (handler.HandlerArgs.ContainsKey("upload-probability"))
        {
            int.TryParse(handler.HandlerArgs["upload-probability"].ToString(), out _uploadProbability);
            if (!CheckProbabilityVar(handler.HandlerArgs["upload-probability"].ToString(), _uploadProbability))
            {
                _uploadProbability = 0;
            }
        }

        if (handler.HandlerArgs.ContainsKey("download-probability"))
        {
            int.TryParse(handler.HandlerArgs["download-probability"].ToString(), out _downloadProbability);
            if (!CheckProbabilityVar(handler.HandlerArgs["download-probability"].ToString(), _downloadProbability))
            {
                _downloadProbability = 0;
            }
        }

        if (handler.HandlerArgs.ContainsKey("input-directory"))
        {
            string targetDir = handler.HandlerArgs["input-directory"].ToString();
            targetDir = Environment.ExpandEnvironmentVariables(targetDir);
            if (!Directory.Exists(targetDir))
            {
                Log.Trace($"Outlookv2:: input directory {targetDir} does not exist, using browser downloads directory.");
            }
            else
            {
                _inputDirectory = targetDir;
            }
        }

        if (_inputDirectory == null)
        {
            _inputDirectory = KnownFolders.GetDownloadFolderPath();
        }

        if (handler.HandlerArgs.ContainsKey("output-directory"))
        {
            string targetDir = handler.HandlerArgs["output-directory"].ToString();
            targetDir = Environment.ExpandEnvironmentVariables(targetDir);
            if (!Directory.Exists(targetDir))
            {
                //try creating the directory
                try
                {
                    Directory.CreateDirectory(targetDir);
                    _outputDirectory = targetDir;
                }
                catch(Exception ex)
                {
                    Log.Trace(ex);
                    Log.Trace($"Outlookv2:: output directory {targetDir} does not exist and cannot be created, using browser downloads directory.");
                }
                
            }
            else
            {
                _outputDirectory = targetDir;
            }
        }

        if (_outputDirectory == null)
        {
            _outputDirectory = KnownFolders.GetDownloadFolderPath();
        }

        if (handler.HandlerArgs.ContainsKey("min-attachments"))
        {
            int.TryParse(handler.HandlerArgs["min-attachments"].ToString(), out _attachmentsMin);
            if (_attachmentsMin < 0)
            {
                _attachmentsMin = 1;
            }
        }

        if (handler.HandlerArgs.ContainsKey("max-attachments"))
        {
            int.TryParse(handler.HandlerArgs["max-attachments"].ToString(), out _attachmentsMax);
            if (_attachmentsMax < 0)
            {
                _attachmentsMax = 10;
            }
        }

        if (_attachmentsMax < _attachmentsMin) _attachmentsMax = _attachmentsMin;

        if (handler.HandlerArgs.ContainsKey("delay-jitter"))
        {
            _jitterfactor = Jitter.JitterFactorParse(handler.HandlerArgs["delay-jitter"].ToString());
            if (_jitterfactor < 0) _jitterfactor = 0;
        }
    }

    private void setProbabilityDefaults()
    {
        _readProbability = 25;
        _deleteProbability = 10;
        _createProbability = 40;
        _replyProbability = 25;
    }


    // return false if nothing to do
    private bool CleanFolderOneItem(string targetFolderName, bool deleteAll)
    {
        var folderName = GetFolder(targetFolderName);
        var targetFolder = this._app.Session.GetDefaultFolder(folderName);

        var folderItems = targetFolder.Items;
        var count = folderItems.Count;
        var settings = Program.Configuration.Email;

        if (!deleteAll && count <= settings.EmailsMax)
        {
            return false ; //finished
        }

        if (deleteAll && count == 0) return false ; //finished
        MailItem folderItem = (MailItem)folderItems.GetLast();
        //you are supposed to delete starting at the last item
        folderItem.Delete();
        
        return true;
    }


    private void CleanFolder(string targetFolderName, bool deleteAll)
    {
        while (CleanFolderOneItem(targetFolderName, deleteAll)) ;
    }


    private bool DeleteViaOutlook()
    {
        try
        {
            
            var settings = Program.Configuration.Email;

            if (settings.EmailsMax <= 0)
            {
                Log.Trace("Outlookv2: EmailsMax in application.json must be set greater than 0 for cleanup/deletion operation to occur.");
                return true;
            }

            CleanFolder("INBOX", false);
            CleanFolder("SENT", false);
            CleanFolder("DRAFTS", true);
            CleanFolder("DELETED", true);

        }
        catch (Exception e)
        {
            Log.Error(e);
            return false;
        }

        return true;
    }

    private List<string> DownloadAttachments(MailItem folderItem)
    {
        List<string> retval = null;

        if (_saveattachmentProbability == 0) return retval;

        try
        {
            if (folderItem.Attachments.Count > 0)
            {
                
                for (int i = 1; i <= folderItem.Attachments.Count; i++)
                {
                    if (_random.Next(0, 100) <= _saveattachmentProbability)
                    {
                        string outpath = Path.Combine(_outputDirectory, folderItem.Attachments[i].FileName);
                        if (File.Exists(outpath))
                        {
                            File.Delete(outpath);   //delete the existing file
                        }
                        folderItem.Attachments[i].SaveAsFile(outpath);
                        if (retval == null) retval = new List<string>();
                        retval.Add(outpath);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Trace("Outlookv2::  error while dowloading attachments");
            Log.Error(ex);
        }

        return retval;  //eventually may want to do something with these attachments
    }

    private bool ReadViaOutlook()
    {
        try
        {
            var folderItems = _folderInbox.Items;

            foreach (MailItem folderItem in folderItems)
            {
                if (!folderItem.UnRead) continue;
                // mark as read
                folderItem.UnRead = false;
                folderItem.Display(false);
                DownloadAttachments(folderItem);
                Thread.Sleep(10000);
                folderItem.Close(Microsoft.Office.Interop.Outlook.OlInspectorClose.olDiscard);
                return true;
            }
            //if get here, read an email already read
            var choice = _random.Next(0, folderItems.Count - 1);
            var index = 0;
            foreach (MailItem folderItem in folderItems)
            {
                if (index == choice)
                {
                    folderItem.Display(false);
                    DownloadAttachments(folderItem);
                    Thread.Sleep(10000);
                    folderItem.Close(Microsoft.Office.Interop.Outlook.OlInspectorClose.olDiscard);
                    break;
                }
                index += 1;
            }
        }
        catch (Exception e)
        {
            Log.Error(e);
            return false;
        }

        return true;
    }


    private bool ClickRandomLink(TimelineEvent timelineEvent)
    {
        try
        {
            var folderItemsRaw = _folderInbox.Items;
            var folderItems = new List<MailItem>();
            foreach (MailItem folderItem in folderItemsRaw)
            {
                folderItems.Add(folderItem);
            }

            var filteredEmails = folderItems.Where(x => x.BodyFormat == OlBodyFormat.olFormatHTML && x.HTMLBody.Contains("<a href="));
            var mailItem = filteredEmails.PickRandom();

            //check deny list
            var list = DenyListManager.ScrubList(mailItem.HTMLBody.GetHrefUrls());
            if (list.Any())
            {
                list.PickRandom().OpenUrl();
            }
        }
        catch (Exception e)
        {
            Log.Error(e);
            return false;
        }

        return true;
    }

    private bool Navigate(IEnumerable<object> config)
    {
        var hasErrors = true;

        try
        {
            foreach (var configuredFolder in config)
            {
                try
                {
                    var fName = configuredFolder.ToString();
                    var sleepTime = 5000;

                    var configArray = fName.Split(Convert.ToChar("|"));
                    if (configArray.GetUpperBound(0) > 0)
                    {
                        try
                        {
                            fName = configArray[0].Trim();
                            sleepTime = Convert.ToInt32(configArray[1].Trim()) * 1000;
                        }
                        catch
                        {
                            //
                        }
                    }

                    var folderName = GetFolder(fName);
                    var f = this._app.Session.GetDefaultFolder(folderName);
                    f.Display();
                    Log.Trace($"Folder displayed: {folderName} - now sleeping for {sleepTime}");
                    Thread.Sleep(sleepTime);
                }
                catch (Exception e)
                {
                    Log.Debug($"Could not navigate to folder: {configuredFolder}: {e}");
                }
                this.CloseExplorers();
            }
        }
        catch (Exception exc)
        {
            Log.Debug(exc);
            hasErrors = false;
        }
        return hasErrors;
    }



    private OlDefaultFolders GetFolder(string folder)
    {
        Log.Trace(folder.ToUpper());
        switch (folder.ToUpper())
        {
            default:
                return OlDefaultFolders.olFolderInbox;
            case "OUTBOX":
                return OlDefaultFolders.olFolderOutbox;
            case "DRAFTS":
                return OlDefaultFolders.olFolderDrafts;
            case "SENT":
                return OlDefaultFolders.olFolderSentMail;
            case "DELETED":
            case "DELETEDITEMS":
                return OlDefaultFolders.olFolderDeletedItems;
            case "JUNK":
                return OlDefaultFolders.olFolderJunk;
        }
    }

    private void CloseExplorers()
    {
        var explorerCount = this._app.Explorers.Count;
        Log.Trace($"Explorer count: {explorerCount}");
        if (explorerCount > 0)
        {
            //# MS Program APIs are 1-indexed.
            for (var i = 1; i < explorerCount + 1; i++)
            {
                try
                {
                    this._app.Explorers[i].Close();
                    Log.Trace($"Closing app explorer: {i}");
                }
                catch (Exception exc)
                {
                    Log.Trace($"Error in closing app explorer: {exc}");
                }
            }
        }
    }

    // ReSharper disable once UnusedParameter.Local
    private bool ReplyViaOutlook(EmailConfiguration emailConfig)
    {
        var config = Program.Configuration.Email;

        try
        {
            var folderItems = _folderInbox.Items;

            foreach (MailItem folderItem in folderItems)
            {
                if (!folderItem.UnRead) continue;

                var emailReply = new EmailReplyManager();

                var replyMail = folderItem.Reply();

                using (var quoted = new StringWriter())
                {
                    quoted.WriteLine(emailReply.Reply);
                    quoted.WriteLine("");
                    quoted.WriteLine("");
                    quoted.WriteLine($"On {folderItem.SentOn:f}, {folderItem.SenderEmailAddress} wrote:");
                    using (var reader = new StringReader(folderItem.Body))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            quoted.Write("> ");
                            quoted.WriteLine(line);
                        }
                    }

                    replyMail.Body = quoted.ToString();
                }

                replyMail.Subject = $"RE: {folderItem.Subject}";

                var rdoMail = new SafeMailItem
                {
                    Item = replyMail
                };

                var r = rdoMail.Recipients.AddEx(folderItem.SenderEmailAddress);
                r.Resolve();
                rdoMail.Recipients.ResolveAll();
                rdoMail.Send();

                var mapiUtils = new MAPIUtils();
                mapiUtils.DeliverNow();

                // mark as read
                folderItem.UnRead = false;

                if (config.SetForcedSendReceive)
                {
                    Log.Trace("Forcing mapi - send and receive, then sleeping for 3000");
                    _oMapiNamespace.SendAndReceive(false);
                    Thread.Sleep(3000);
                }

                return true;
            }
        }
        catch (Exception e)
        {
            Log.Error($"Outlook reply error: {e}");
        }
        return false;
    }

    private List<string> GetRandomFiles(string targetDir, string pattern, int count)
    {
        try
        {
            while (true)
            {
                string[] filelist = Directory.GetFiles(targetDir, pattern);
                if (filelist.Length == 0) return null;
                if (count == 0) return null;
                List<string> retVal = new List<string>();
                if (count == 1)
                {
                    retVal.Add(filelist[_random.Next(0,filelist.Length-1)]);
                    return retVal;
                }
                // need more than one, have to avoid duplicates, prune down
                foreach (string file in filelist)
                {
                    retVal.Add(file);
                }
                
                while (true)
                {
                    if (retVal.Count <= count) break;
                    var index = _random.Next(0, retVal.Count - 1);
                    retVal.RemoveAt(index);
                }

                return retVal;
            }
        }
        catch { } //ignore any errors
        return null;
    }

    private bool SendEmailViaOutlook(EmailConfiguration emailConfig)
    {
        ClientConfiguration.EmailSettings config = Program.Configuration.Email;
        bool wasSuccessful = false;

        try
        {
            //now create mail object (but we'll not send it via outlook)
            Log.Trace("Creating outlook mail item");
            dynamic mailItem = _app.CreateItem(OlItemType.olMailItem);

            //Add subject
            if (!string.IsNullOrWhiteSpace(emailConfig.Subject))
            {
                mailItem.Subject = emailConfig.Subject;
            }

            Log.Trace($"Setting message subject to: {mailItem.Subject}");

     
            //Set message body according to type of message
            switch (emailConfig.BodyType)
            {
                case EmailConfiguration.EmailBodyType.HTML:
                    mailItem.HTMLBody = emailConfig.Body;
                    Log.Trace($"Setting message HTMLBody to: {emailConfig.Body}");
                    break;
                case EmailConfiguration.EmailBodyType.RTF:
                    mailItem.RTFBody = emailConfig.Body;
                    Log.Trace($"Setting message RTFBody to: {emailConfig.Body}");
                    break;
                case EmailConfiguration.EmailBodyType.PlainText:
                    mailItem.Body = emailConfig.Body;
                    Log.Trace($"Setting message Body to: {emailConfig.Body}");
                    break;
                default:
                    throw new Exception("Bad email body type: " + emailConfig.BodyType);
            }

            List<string> attachments = emailConfig.Attachments;

            if (attachments.Count == 0)
            {
                if (_attachmentProbability != 0 && _random.Next(0,100) <= _attachmentProbability)
                {
                    int numAttachments = _random.Next(_attachmentsMin, _attachmentsMax);
                    if (numAttachments > 0)
                    {
                        attachments = GetRandomFiles(_inputDirectory, "*", numAttachments);
                    }
                }
            }

            //attachments
            if (attachments != null && attachments.Count > 0)
            {
                //Add attachments
                foreach (string path in attachments)
                {
                    mailItem.Attachments.Add(path);
                    Log.Trace($"Adding attachment from: {path}");
                }
            }

            if (config.SetAccountFromConfig || config.SetAccountFromLocal)
            {
                Accounts accounts = _app.Session.Accounts;
                Account acc = null;

                if (config.SetAccountFromConfig)
                {
                    //Look for our account in the Outlook
                    foreach (Account account in accounts)
                    {
                        if (account.SmtpAddress.Equals(emailConfig.From, StringComparison.CurrentCultureIgnoreCase))
                        {
                            //Use it
                            acc = account;
                            break;
                        }
                    }
                }

                if (acc == null)
                {
                    foreach (Account account in accounts)
                    {
                        acc = account;
                        break;
                    }
                }

                //Did we get the account?
                if (acc != null)
                {
                    Log.Trace($"Sending via {acc.DisplayName}");
                    //Use this account to send the e-mail
                    mailItem.SendUsingAccount = acc;
                }
            }

            if (config.SaveToOutbox)
            {
                Log.Trace("Saving mailItem to outbox...");
                mailItem.Move(_folderOutbox);
                mailItem.Save();
            }

            Log.Trace("Attempting new Redemtion SafeMailItem...");
            var rdoMail = new SafeMailItem
            {
                Item = mailItem
            };

            //Parse To
            if (emailConfig.To.Count > 0)
            {
                var list = emailConfig.To.Distinct();
                foreach (var a in list)
                {
                    var r = rdoMail.Recipients.AddEx(a.Trim());
                    r.Resolve();
                    Log.Trace($"RdoMail TO {a.Trim()}");
                }
            }
            else
            {
                throw new Exception("Must specify to-address");
            }

            //Parse Cc
            if (emailConfig.Cc.Count > 0)
            {
                var list = emailConfig.Cc.Distinct();
                foreach (var a in list)
                {
                    var r = rdoMail.Recipients.AddEx(a.Trim());
                    r.Resolve();
                    if (r.Resolved)
                    {
                        r.Type = 2; //CC
                    }

                    Log.Trace($"RdoMail CC {a.Trim()}");
                }
            }

            if (emailConfig.Bcc.Count > 0)
            {
                var list = emailConfig.Bcc.Distinct();
                foreach (var a in list)
                {
                    var r = rdoMail.Recipients.AddEx(a.Trim());
                    r.Resolve();
                    if (r.Resolved)
                    {
                        r.Type = 3; //BCC
                    }

                    Log.Trace($"RdoMail BCC {a.Trim()}");
                }
            }

            rdoMail.Recipients.ResolveAll();

            Log.Trace("Attempting to send Redemtion SafeMailItem...");
            rdoMail.Send();

            var mapiUtils = new MAPIUtils();
            mapiUtils.DeliverNow();

            //Done
            wasSuccessful = true;

            Log.Trace("Redemtion SafeMailItem was sent successfully");

            if (config.SetForcedSendReceive)
            {
                Log.Trace("Forcing mapi - send and receive, then sleeping for 3000");
                _oMapiNamespace.SendAndReceive(false);
                Thread.Sleep(3000);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }
        Log.Trace($"Returning - wasSuccessful:{wasSuccessful}");
        return wasSuccessful;
    }
}