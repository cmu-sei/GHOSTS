// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Client.Infrastructure.Email;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Microsoft.Office.Interop.Outlook;
using NLog;
using Redemption;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Exception = System.Exception;
using MAPIFolder = Microsoft.Office.Interop.Outlook.MAPIFolder;

namespace Ghosts.Client.Handlers
{
    public class Outlook : BaseHandler
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private RDOSession _session;
        private Microsoft.Office.Interop.Outlook.Application _app;
        private NameSpace _oMapiNamespace;
        private MAPIFolder _folderInbox;
        private MAPIFolder _folderOutbox;
        private MAPIFolder _folderSent;

        public Outlook(TimelineHandler handler)
        {
            try
            {
                //redemption prep
                //tell the app where the 32 and 64 bit dlls are located
                //by default, they are assumed to be in the same folder as the current assembly and be named
                //Redemption.dll and Redemption64.dll.
                //In that case, you do not need to set the two properties below
                DirectoryInfo currentDir = new FileInfo(GetType().Assembly.Location).Directory;
                RedemptionLoader.DllLocation64Bit = Path.GetFullPath(currentDir + @"\lib\redemption64.dll");
                RedemptionLoader.DllLocation32Bit = Path.GetFullPath(currentDir + @"\lib\redemption.dll");
                //Create a Redemption object and use it
                _log.Trace("Creating new RDO session");
                _session = RedemptionLoader.new_RDOSession();
                _log.Trace("Attempting RDO session logon...");
                _session.Logon(Missing.Value, Missing.Value, Missing.Value, Missing.Value, Missing.Value, Missing.Value);
            }
            catch (Exception e)
            {
                _log.Error($"RDO load error: {e}");
            }

            try
            {
                _app = new Microsoft.Office.Interop.Outlook.Application();
                _oMapiNamespace = _app.GetNamespace("MAPI");
                _folderInbox = _oMapiNamespace.GetDefaultFolder(OlDefaultFolders.olFolderInbox);
                _folderOutbox = _oMapiNamespace.GetDefaultFolder(OlDefaultFolders.olFolderOutbox);
                _folderSent = _oMapiNamespace.GetDefaultFolder(OlDefaultFolders.olFolderSentMail);
                _log.Trace("Launching Outlook");
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
                _log.Error(e);
            }
        }

        public void ExecuteEvents(TimelineHandler handler)
        {
            try
            {
                foreach (TimelineEvent timelineEvent in handler.TimeLineEvents)
                {
                    WorkingHours.Is(handler);

                    if (timelineEvent.DelayBefore > 0)
                    {
                        Thread.Sleep(timelineEvent.DelayBefore);
                    }

                    switch (timelineEvent.Command.ToUpper())
                    {
                        default:
                            try
                            {
                                EmailConfiguration emailConfig = new EmailConfiguration(timelineEvent.CommandArgs);
                                if (SendEmailViaOutlook(emailConfig))
                                {
                                    Report(handler.HandlerType.ToString(), timelineEvent.Command, emailConfig.ToString());
                                }
                            }
                            catch (Exception e)
                            {
                                _log.Error(e);
                            }

                            break;
                        case "REPLY":
                            try
                            {
                                EmailConfiguration emailConfig = new EmailConfiguration(timelineEvent.CommandArgs);
                                if (ReplyViaOutlook(emailConfig))
                                {
                                    Report(handler.HandlerType.ToString(), timelineEvent.Command, emailConfig.ToString());
                                }
                            }
                            catch (Exception e)
                            {
                                _log.Error(e);
                            }
                            break;
                        case "NAVIGATE":
                            try
                            {
                                if (Navigate(timelineEvent.CommandArgs))
                                {
                                    Report(handler.HandlerType.ToString(), timelineEvent.Command, string.Join(",", timelineEvent.CommandArgs));
                                }
                            }
                            catch (Exception e)
                            {
                                _log.Error(e);
                            }
                            break;
                    }

                    if (timelineEvent.DelayAfter > 0)
                    {
                        Thread.Sleep(timelineEvent.DelayAfter);
                    }
                }
            }
            catch (Exception e)
            {
                _log.Debug(e);
            }
        }

        private bool Navigate(IEnumerable<object> config)
        {
            var hasErrors = true;

            try
            {
                MAPIFolder f;
                foreach (var configuredFolder in config)
                {
                    try
                    {
                        var folderName = GetFolder(configuredFolder.ToString().Trim());
                        f = this._app.Session.GetDefaultFolder(folderName); //OlDefaultFolders.olFolderOutbox
                        f.Display();
                        _log.Trace($"Folder displayed: {folderName}");
                        Thread.Sleep(5000);
                    }
                    catch (Exception e)
                    {
                        _log.Trace($"Could not navigate to folder: {configuredFolder}");
                    }
                    this.CloseExplorers();
                }

                f = this._app.Session.GetDefaultFolder(OlDefaultFolders.olFolderInbox);
                f.Display();
                _log.Trace($"Folder displayed: outbox");
                Thread.Sleep(5000);

                this.CloseExplorers();
            }
            catch (Exception exc)
            {
                _log.Debug(exc);
                hasErrors = false;
            }


            return hasErrors;
        }

        private OlDefaultFolders GetFolder(string folder)
        {
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
            _log.Trace($"Explorer count: {explorerCount}");
            if (explorerCount > 0)
            {
                //# MS Program APIs are 1-indexed.
                for (var i = 1; i < explorerCount + 1; i++)
                {
                    try
                    {
                        this._app.Explorers[i].Close();
                    }
                    catch { }
                }
            }
        }

        private bool ReplyViaOutlook(EmailConfiguration emailConfig)
        {
            ClientConfiguration.EmailSettings config = Program.Configuration.Email;

            try
            {
                Items folderItems = _folderSent.Items;

                foreach (MailItem folderItem in folderItems)
                {
                    if (folderItem.UnRead)
                    {
                        EmailReplyManager emailReply = new EmailReplyManager();


                        folderItem.HTMLBody =
                            $"{emailReply.Reply} {Environment.NewLine}{Environment.NewLine}ORIGINAL MESSAGE --- {Environment.NewLine}{Environment.NewLine}{folderItem.Body}";
                        folderItem.Subject = $"RE: {folderItem.Subject}";

                        MailItem replyMail = folderItem.Reply();
                        replyMail.Move(_folderSent);
                        
                        SafeMailItem rdoMail = new Redemption.SafeMailItem
                        {
                            Item = replyMail
                        };
                        
                        rdoMail.Recipients.ResolveAll();
                        rdoMail.Send();

                        var mapiUtils = new Redemption.MAPIUtils();
                        mapiUtils.DeliverNow(0, 0);

                        if (config.SetForcedSendReceive)
                        {
                            _log.Trace("Forcing mapi - send and receive");
                            _oMapiNamespace.SendAndReceive(false);
                            Thread.Sleep(3000);
                        }

                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                _log.Error($"Outlook reply error: {e}");
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
                _log.Trace("Creating outlook mail item");
                dynamic mailItem = _app.CreateItem(OlItemType.olMailItem);

                //Add subject
                if (!string.IsNullOrWhiteSpace(emailConfig.Subject))
                {
                    mailItem.Subject = emailConfig.Subject;
                }

                _log.Trace($"Setting message subject to: {mailItem.Subject}");

                //Set message body according to type of message
                switch (emailConfig.BodyType)
                {
                    case EmailConfiguration.EmailBodyType.HTML:
                        mailItem.HTMLBody = emailConfig.Body;
                        _log.Trace($"Setting message HTMLBody to: {emailConfig.Body}");
                        break;
                    case EmailConfiguration.EmailBodyType.RTF:
                        mailItem.RTFBody = emailConfig.Body;
                        _log.Trace($"Setting message RTFBody to: {emailConfig.Body}");
                        break;
                    case EmailConfiguration.EmailBodyType.PlainText:
                        mailItem.Body = emailConfig.Body;
                        _log.Trace($"Setting message Body to: {emailConfig.Body}");
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
                        _log.Trace($"Adding attachment from: {path}");
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

                    //TODO: if no from account found, just use first one found to send - but should ghosts do this?
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
                        _log.Trace($"Sending via {acc.DisplayName}");
                        //Use this account to send the e-mail
                        mailItem.SendUsingAccount = acc;
                    }
                }
                
                if (config.SaveToOutbox)
                {
                    _log.Trace("Saving mailItem to outbox...");
                    mailItem.Move(_folderOutbox);
                    mailItem.Save();
                }
                
                _log.Trace("Attempting new Redemtion SafeMailItem...");
                SafeMailItem rdoMail = new Redemption.SafeMailItem
                {
                    Item = mailItem
                };
                //Parse To
                if (emailConfig.To.Count > 0)
                {
                    System.Collections.Generic.IEnumerable<string> list = emailConfig.To.Distinct();
                    foreach (string a in list)
                    {
                        SafeRecipient r = rdoMail.Recipients.AddEx(a.Trim());
                        r.Resolve();
                        _log.Trace($"RdoMail TO {a.Trim()}");
                    }
                }
                else
                {
                    throw new Exception("Must specify to-address");
                }

                //Parse Cc
                if (emailConfig.Cc.Count > 0)
                {
                    System.Collections.Generic.IEnumerable<string> list = emailConfig.Cc.Distinct();
                    foreach (string a in list)
                    {
                        SafeRecipient r = rdoMail.Recipients.AddEx(a.Trim());
                        r.Resolve();
                        if (r.Resolved)
                        {
                            r.Type = 2; //CC
                        }

                        _log.Trace($"RdoMail CC {a.Trim()}");
                    }
                }

                if (emailConfig.Bcc.Count > 0)
                {
                    System.Collections.Generic.IEnumerable<string> list = emailConfig.Bcc.Distinct();
                    foreach (string a in list)
                    {
                        SafeRecipient r = rdoMail.Recipients.AddEx(a.Trim());
                        r.Resolve();
                        if (r.Resolved)
                        {
                            r.Type = 3; //BCC
                        }

                        _log.Trace($"RdoMail BCC {a.Trim()}");
                    }
                }

                /*
                    outlook_mail_item = self._outlook.outlook_application.CreateItem(win32com.client.constants.olMailItem)
                    outlook_mail_item = outlook_mail_item.Move(outbox)

                    outlook_mail_item.Subject = subject
                    outlook_mail_item.Body = body
                    outlook_mail_item.Save()

                    for file_ in self._config['attachments']:
                        outlook_mail_item.Attachments.Add(file_)

                    # Need to use Redemption to actually get it to send correctly.
                    new_email = win32com.client.Dispatch('Redemption.SafeMailItem')
                    new_email.Item = outlook_mail_item
                    new_email.Recipients.Add(self._config['destination'])
                    new_email.Recipients.ResolveAll()
                    new_email.Send()
                 */


                rdoMail.Recipients.ResolveAll();

                _log.Trace("Attempting to send Redemtion SafeMailItem...");
                rdoMail.Send();

                var mapiUtils = new Redemption.MAPIUtils();
                mapiUtils.DeliverNow();

                //Done
                wasSuccessful = true;

                _log.Trace("Redemtion SafeMailItem was sent successfully");

                if (config.SetForcedSendReceive)
                {
                    _log.Trace("Forcing mapi - send and receive");
                    _oMapiNamespace.SendAndReceive(false);
                    Thread.Sleep(3000);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex);
            }
            _log.Trace($"Returning - wasSuccessful:{wasSuccessful}");
            return wasSuccessful;
        }
    }
}