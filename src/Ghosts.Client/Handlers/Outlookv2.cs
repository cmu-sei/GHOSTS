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
using ReportItem = Ghosts.Domain.Code.ReportItem;

namespace Ghosts.Client.Handlers;

public class Outlookv2 : BaseHandler
{
    //private readonly Application _app;
    //private readonly NameSpace _oMapiNamespace;
    //private readonly MAPIFolder _folderOutbox;
    //private readonly MAPIFolder _folderInbox;
    //private readonly MAPIFolder _folderDeletedItems;

    private Application _app;
    private NameSpace _oMapiNamespace;
    private MAPIFolder _folderOutbox;
    private MAPIFolder _folderInbox;
    private MAPIFolder _folderDeletedItems;


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
    private int _initialOutlookDelay = 3 * 60 * 1000;
    private int _attachmentsMaxSize = 10; //max total size of attachments in MB

    private string _inputDirectory = null;
    private string _outputDirectory = null;
    private int _jitterfactor = 0;  //used with Jitter.JitterFactorDelay
    private int _attachmentsMax = 0;
    private int _attachmentsMin = 0;

    private string[] _actionList = { "read", "reply", "create", "delete" };
    private int[] _probabilityList = { 0, 0, 0, 0 };
    
    private int _actionCount = 0;
    private int _replyErrorCount = 0;
    private int _replyErrorThreshold = 10;  //after this many reply errors, abandon replies in favor of create
    private bool _firstDisplay = false;
    private int _totalErrorCount = 0;
    private int _restartThreadhold = 20;
    
    

    public Outlookv2(TimelineHandler handler)
    {
        try
        {
            base.Init(handler);
            MakeRdoSession();
        }
        
        catch (Exception e)
        {
            Log.Error($"Outlookv2:: Init error {e}");
        }

        try
        {
            InitialDisplay();

            if (handler.Loop)
            {
                while (true)
                {
                    ExecuteEvents(handler);
                    if (_totalErrorCount > _restartThreadhold )
                    {
                        _totalErrorCount = 0;
                        Log.Trace("Outlookv2:: Total successive error count exceeded threshold, Outlookv2 restarting.");
                        doRestart();
                    }
                }
            }
            else
            {
                ExecuteEvents(handler);
            }
        }
        catch (ThreadAbortException)
        {
            ProcessManager.KillProcessAndChildrenByName(ProcessManager.ProcessNames.Outlook);
            Log.Trace("Outlookv2 closing...");
        }
        catch (Exception e)
        {
            Log.Error(e);
        }

    }

    public void MakeRdoSession()
    {
        try
        {

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
            Log.Error($"Outlookv2:: RDO load error {e}");
        }

    }

    public void InitialDisplay()
    {
        _app = new Application();
        _oMapiNamespace = _app.GetNamespace("MAPI");
        _folderInbox = _oMapiNamespace.GetDefaultFolder(OlDefaultFolders.olFolderInbox);
        _folderOutbox = _oMapiNamespace.GetDefaultFolder(OlDefaultFolders.olFolderOutbox);
        _folderDeletedItems = _oMapiNamespace.GetDefaultFolder(OlDefaultFolders.olFolderDeletedItems);
        Log.Trace("Launching Outlook");
        _folderInbox.Display();

    }



    public void doRestart()
    {
        try
        {
            //this will restart by killing Outlook, then restarting it.
            ProcessManager.KillProcessAndChildrenByName(ProcessManager.ProcessNames.Outlook);
            Thread.Sleep(10000);
            //restart
            MakeRdoSession();
            InitialDisplay();
        }
        catch (ThreadAbortException)
        {
            throw;  //pass up
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
    }

    
    public void ExecuteEvents(TimelineHandler handler)
    {
        try
        {
            ParseHandlerArgs(handler);
        }
        
        catch (Exception e)
        {
            Log.Trace("Outlookv2:: Error parsing handler args");
            Log.Error(e);

        }

        if (!_firstDisplay)
        {
            Thread.Sleep(_initialOutlookDelay); //wait 5 minutes before attempting any action
            _firstDisplay = true;
        }

        try
        {
            foreach (var timelineEvent in handler.TimeLineEvents)
            {
                if (_replyProbability > 0 && _replyErrorCount > _replyErrorThreshold)
                {
                    //too many reply errors.  Null this out.
                    _createProbability += _replyProbability;
                    _replyProbability = 0;
                    Log.Trace($"Outlookv2:: Too many reply errors, added reply probability to create, and halted future reply actions.");
                }
                _probabilityList[0] = _readProbability;
                _probabilityList[1] = _replyProbability;
                _probabilityList[2] = _createProbability;
                _probabilityList[3] = _deleteProbability;

                var action = SelectActionFromProbabilities(_probabilityList, _actionList);
                if (_actionCount < 5) action = "create";  //always send 5 emails first before allowing other actions
                if (_actionCount < 100) _actionCount += 1;
                if (action == null)
                {
                    Log.Trace("Outlookv2:: No action this cycle.");
                }
                else
                {
                    
                    WorkingHours.Is(handler);


                    if (timelineEvent.DelayBeforeActual > 0)
                    {
                        Log.Trace($"DelayBefore sleeping for {timelineEvent.DelayBeforeActual} ms");
                        Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayBeforeActual, _jitterfactor));
                    }

                    Log.Trace($"Outlookv2:: Performing action {action} .");
                    //before doing any action, clean the drafts folder as there should be no drafts here.
                    MoveToDeleted("DRAFTS", true, true);
                    EmailConfiguration emailConfig;

                    switch (action)
                    {
                        case "create":
                            
                            emailConfig = new EmailConfiguration(timelineEvent.CommandArgs);
                            if (SendEmailViaOutlook(emailConfig))
                            {
                                Log.Trace("Outlookv2:: Created email");
                                _totalErrorCount = 0;  //zero on success
                            }
                            else
                            {
                                _totalErrorCount++;
                            }
                           
                            break;
                        case "reply":
                            
                            emailConfig = new EmailConfiguration(timelineEvent.CommandArgs);
                            if (ReplyViaOutlook(emailConfig))
                            {
                                
                                Log.Trace("Outlookv2:: Replied email");
                                _totalErrorCount = 0;  //zero on success
                            }
                            else
                            {
                                _totalErrorCount++;
                            }
                            
                            break;
                        case "read":
                            
                            if (ReadViaOutlook())
                            {
                                Log.Trace("Outlookv2:: Read email");
                                _totalErrorCount = 0;  //zero on success
                            }
                            else
                            {
                                _totalErrorCount++;
                            }
                            
                            break;
                        case "delete":
                            
                            if (DeleteViaOutlook())
                            {
                                Log.Trace("Outlookv2:: Deleted email");
                                _totalErrorCount = 0;  //zero on success
                            }
                            else
                            {
                                _totalErrorCount++;
                            }
                            
                            break;

                    }
                    Report(new ReportItem { Handler = handler.HandlerType.ToString(), Command = action, Arg = "", Trackable = timelineEvent.TrackableId });
                    if (timelineEvent.DelayAfterActual > 0)
                    {
                        Log.Trace($"DelayAfter sleeping for {timelineEvent.DelayAfterActual} ms");
                        Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, _jitterfactor));
                        
                    }
                }
            }
        }
        catch (ThreadAbortException)
        {
            throw;  //pass up
        }
        catch (Exception e)
        {
            Log.Error(e);
            _totalErrorCount++;
            Thread.Sleep(10000);  //need delay here to avoid fast error loop
        }
    }

    private void ParseHandlerArgs(TimelineHandler handler)
    {
        if (handler.HandlerArgs.ContainsKey("initial-outlook-delay"))
        {
            int.TryParse(handler.HandlerArgs["initial-outlook-delay"].ToString(), out _initialOutlookDelay);
            if (_initialOutlookDelay < 0 || _initialOutlookDelay > 5*60*1000)
            {
                _initialOutlookDelay = 0;
            }
        }
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
                
                catch (Exception ex)
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

        if (handler.HandlerArgs.ContainsKey("max-attachments-size"))
        {
            int.TryParse(handler.HandlerArgs["max-attachments-size"].ToString(), out _attachmentsMaxSize);
            if (_attachmentsMaxSize <= 0)
            {
                _attachmentsMaxSize = 10;
            }
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

    
    private bool DeleteMailItem (MailItem item)
    {
        try
        {
            item.Delete();
            Thread.Sleep(500);
            return true;
        }
        catch (ThreadAbortException)
        {
            throw;  //pass up
        }
        catch (Exception e)
        {
            Log.Trace("Outlookv2:: Error while deleting item from deleted items folder.");
            Log.Error(e);
            return false;
        }
    }

    private void CleanDeletedItems()
    {
        var folderName = GetFolder("DELETED");
        var targetFolder = this._app.Session.GetDefaultFolder(folderName);
        var folderItems = targetFolder.Items;
        var count = folderItems.Count;
        if (count == 0) return;

       
        MailItem folderItem;
        for (int i = count; i > 0; i--)
        {
            try
            {
                object item = folderItems[i];
                folderItem = item as MailItem;
                if (folderItem != null)
                {
                    //break if unsuccessful as may have reached the maximum number
                    if (!DeleteMailItem(folderItem)) break; 
                }
            }
            catch (ThreadAbortException)
            {
                throw;  //pass up
            }
            catch 
            {
                //ignore others
            }
        }
        return;
    }

    private bool MoveMailItem(MailItem item)
    {
        try
        {
            item.Move(_folderDeletedItems);
            Thread.Sleep(500);
            return true;
        }
        catch (ThreadAbortException)
        {
            throw;  //pass up
        }
        catch (Exception e)
        {
            Log.Trace("Outlookv2:: Error while moving mail item to deleted items folder.");
            Log.Error(e);
            return false;
        }
    }

    private void MoveToDeleted(string targetFolderName, bool deleteAll, bool deleteUnread)
    {
        var folderName = GetFolder(targetFolderName);
        var targetFolder = this._app.Session.GetDefaultFolder(folderName);

        var folderItems = targetFolder.Items;
        var count = folderItems.Count;
        var settings = Program.Configuration.Email;
        if (count == 0) return;

        if (!deleteAll && count <= settings.EmailsMax)
        {
            return; //nothing to do
        }
        MailItem folderItem;

        foreach (object item in folderItems)
        {
            try
            {
                folderItem = item as MailItem;
                if (folderItem == null) continue;
                if (deleteAll)
                {
                    //break if unsuccessful as may have reached the maximum number
                    if (!MoveMailItem(folderItem)) break;
                }
                else
                {
                    if (folderItem.UnRead && !deleteUnread) continue;
                    //break if unsuccessful as may have reached the maximum number
                    if (!MoveMailItem(folderItem)) break;
                    count--;
                    if (count <= settings.EmailsMax)
                    {
                        break; //finished
                    }
                }
            }
            catch (ThreadAbortException)
            {
                throw;  //pass up
            }
            catch 
            {
                //ignore others
               
            }
        }
        return;
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

            MoveToDeleted("INBOX", false, false);
            MoveToDeleted("INBOX", false, true);
            MoveToDeleted("SENT", false, true);
            MoveToDeleted("DRAFTS", true, true);
            CleanDeletedItems();
        }
        catch (ThreadAbortException)
        {
            throw;  //pass up
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
        catch (ThreadAbortException)
        {
            throw;  //pass up
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
            var folderItems = _folderInbox.Items.Restrict("[Unread]=true");
            MailItem folderItem;

            foreach (object item in folderItems)
            {
                try
                {
                    folderItem = item as MailItem;
                    if (folderItem == null) continue;
                    if (!folderItem.UnRead) continue;  //should not be needed but WTH
                    // mark as read
                    folderItem.UnRead = false;
                    folderItem.Display(false);
                    DownloadAttachments(folderItem);
                    Thread.Sleep(10000);
                    folderItem.Close(Microsoft.Office.Interop.Outlook.OlInspectorClose.olDiscard);
                    return true;
                }
                catch (ThreadAbortException)
                {
                    throw;  //pass up
                }
                catch { 
                    //ignore others
                }
            }
            folderItems = _folderInbox.Items;  //no filter this time
            var count = folderItems.Count;
            if (count > 0)
            {
                var choice = _random.Next(1, count);
                //if get here, read an email already read
                for (int i = choice; i > 0; i--)
                {
                    object item = folderItems[i];
                    try
                    {
                        folderItem = item as MailItem;
                        if (folderItem == null) continue;
                        folderItem.Display(false);
                        DownloadAttachments(folderItem);
                        Thread.Sleep(10000);
                        folderItem.Close(Microsoft.Office.Interop.Outlook.OlInspectorClose.olDiscard);
                        return true;
                    }
                    catch (ThreadAbortException)
                    {
                        throw;  //pass up
                    }
                    catch
                    {
                        //ignore others
                    }
                }
            }
        }
        catch (ThreadAbortException)
        {
            throw;  //pass up
        }
        catch (Exception e)
        {
            Log.Error(e);
            return false;
        }

        return false;
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
            var list = DenyListManager.RemoveDeniedFromList(mailItem.HTMLBody.GetHrefUrls());
            if (list.Any())
            {
                list.PickRandom().OpenUrl();
            }
        }
        catch (ThreadAbortException)
        {
            throw;  //pass up
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
                catch (ThreadAbortException)
                {
                    throw;  //pass up
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
        var settings = Program.Configuration.Email;
        string[] EmailNoReply = null;
        if (settings.EmailNoReply != null && settings.EmailNoReply != "")
        {
            EmailNoReply = settings.EmailNoReply.ToLower().Split(',');
        }



        try
        {
            var folderItems = _folderInbox.Items;
            bool replyStarted = false;
            MailItem folderItem;
            foreach (object item in folderItems)
            {
                try
                {
                    folderItem = item as MailItem;
                    if (folderItem == null) continue;
                    bool reject = false;
                    var targetEmail = folderItem.SenderEmailAddress.ToLower();
                    if (EmailNoReply != null && EmailNoReply.Length > 0)
                    {
                        foreach (string target in EmailNoReply)
                        {
                            if (targetEmail.Contains(target))
                            {
                                reject = true;
                                break;
                            }
                        }
                    }
                    if (reject)
                    {
                        Log.Trace($"Rejecting reply to address: {targetEmail} as it matches one of: {settings.EmailNoReply} ");
                        continue;
                    }

                    replyStarted = true;
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
                    Log.Trace("Outlookv2:: Reply action completed.");

                    return true;
                }
                catch (ThreadAbortException)
                {
                    throw;  //pass up
                }
                catch (Exception exc)
                {
                    Log.Error($"Outlook reply error: {exc}");
                    if (replyStarted)
                    {
                        //we found an email, and tried to reply, but an error occurred.
                        _replyErrorCount += 1;
                        return false;  
                    }
                }
            }
        }
        catch (ThreadAbortException)
        {
            throw;  //pass up
        }
        catch (Exception e)
        {
            Log.Error($"Outlook reply error: {e}");
        }
        return false;
    }

    private List<string> GetRandomFiles(string targetDir, string pattern, int count, int maxSize)
    {
        try
        {
            while (true)
            {
                if (count == 0) return null;
                //divide maxSize by count so that there is no possibility of total attachment size exceeding maxSize
                long maxSizeBytes = (maxSize * 1024 * 1024) / count; //maxSize is in MB
                string[] filelist = Directory.GetFiles(targetDir, pattern);
                if (filelist.Length == 0) return null;
                //filter files by maxSizeBytes
                List<string> filteredFiles = new List<string>();
                foreach (string file in filelist)
                {
                    try
                    {
                        FileInfo info = new FileInfo(file);
                        if (info.Length <= maxSizeBytes)
                        {
                            filteredFiles.Add(file);
                        }
                  
                    }
                    catch (ThreadAbortException)
                    {
                        throw;  //pass up
                    }
                    catch (Exception e)
                    {
                        Log.Error($"Outlook file access error during attachments: {e}");
                    }
                }
                if (filteredFiles.Count == 0) return null;         
                
                if (count == 1)
                {
                    return new List<string>() { filteredFiles[_random.Next(0, filteredFiles.Count - 1)] };
                }
                // need more than one, have to avoid duplicates, prune down
                while (true)
                {
                    if (filteredFiles.Count <= count) break;
                    var index = _random.Next(0, filteredFiles.Count - 1);
                    filteredFiles.RemoveAt(index);
                }

                return filteredFiles;
            }
        }
        catch (ThreadAbortException)
        {
            throw;  //pass up
        }
        catch {
            //ignore others
        }
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
                        attachments = GetRandomFiles(_inputDirectory, "*", numAttachments, _attachmentsMaxSize);
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
        catch (ThreadAbortException)
        {
            throw;  //pass up
        }

        catch (Exception ex)
        {
            Log.Error(ex);
        }
        Log.Trace($"Returning - wasSuccessful:{wasSuccessful}");
        return wasSuccessful;
    }
}