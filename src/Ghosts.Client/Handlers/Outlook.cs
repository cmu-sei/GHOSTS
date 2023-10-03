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
using ReportItem = Ghosts.Domain.Code.ReportItem;

namespace Ghosts.Client.Handlers;

public class Outlook : BaseHandler
{
    private readonly Application _app;
    private readonly NameSpace _oMapiNamespace;
    private readonly MAPIFolder _folderOutbox;
    private readonly MAPIFolder _folderInbox;

    public Outlook(TimelineHandler handler)
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
        try
        {
            foreach (var timelineEvent in handler.TimeLineEvents)
            {
                Infrastructure.WorkingHours.Is(handler);

                if (timelineEvent.DelayBefore > 0)
                {
                    Log.Trace($"DelayBefore sleeping for {timelineEvent.DelayBefore} ms");
                    Thread.Sleep(timelineEvent.DelayBefore);
                }

                switch (timelineEvent.Command.ToUpper())
                {
                    default:
                        try
                        {
                            var emailConfig = new EmailConfiguration(timelineEvent.CommandArgs);
                            if (SendEmailViaOutlook(emailConfig))
                            {
                                Report(new ReportItem { Handler = handler.HandlerType.ToString(), Command = timelineEvent.Command, Arg = emailConfig.ToString(), Trackable = timelineEvent.TrackableId });
                            }
                        }
                        catch (Exception e)
                        {
                            Log.Error(e);
                        }

                        break;
                    case "REPLY":
                        try
                        {
                            var emailConfig = new EmailConfiguration(timelineEvent.CommandArgs);
                            if (ReplyViaOutlook(emailConfig))
                            {
                                Report(new ReportItem { Handler = handler.HandlerType.ToString(), Command = timelineEvent.Command, Arg = emailConfig.ToString(), Trackable = timelineEvent.TrackableId });
                            }
                        }
                        catch (Exception e)
                        {
                            Log.Error(e);
                        }
                        break;
                    case "NAVIGATE":
                        try
                        {
                            if (Navigate(timelineEvent.CommandArgs))
                            {
                                Report(new ReportItem { Handler = handler.HandlerType.ToString(), Command = timelineEvent.Command, Arg = string.Join(",", timelineEvent.CommandArgs), Trackable = timelineEvent.TrackableId });
                            }
                        }
                        catch (Exception e)
                        {
                            Log.Error(e);
                        }
                        break;
                    case "CLICKRANDOMLINK":
                        try
                        {
                            if (ClickRandomLink(timelineEvent))
                            {
                                Report(new ReportItem { Handler = handler.HandlerType.ToString(), Command = timelineEvent.Command, Arg = string.Join(",", timelineEvent.CommandArgs), Trackable = timelineEvent.TrackableId });
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
                    Thread.Sleep(timelineEvent.DelayAfter);
                }
            }
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
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

            //attachments
            if (emailConfig.Attachments.Count > 0)
            {
                //Add attachments
                foreach (string path in emailConfig.Attachments)
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