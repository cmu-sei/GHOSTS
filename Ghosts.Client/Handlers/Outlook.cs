// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Ghosts.Client.Code.Email;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Microsoft.Office.Interop.Outlook;
using NLog;
using Redemption;
using Exception = System.Exception;
using MAPIFolder = Microsoft.Office.Interop.Outlook.MAPIFolder;

namespace Ghosts.Client.Handlers
{
    public class Outlook : BaseHandler
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private RDOSession _session;
        private Microsoft.Office.Interop.Outlook.Application _app;
        private NameSpace _olNamespace;
        private MAPIFolder _inboxFolder;

        public Outlook(TimelineHandler handler)
        {
            try
            {
                //redemption prep
                //tell the app where the 32 and 64 bit dlls are located
                //by default, they are assumed to be in the same folder as the current assembly and be named
                //Redemption.dll and Redemption64.dll.
                //In that case, you do not need to set the two properties below
                var currentDir = new FileInfo(this.GetType().Assembly.Location).Directory;
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
                this._app = new Microsoft.Office.Interop.Outlook.Application();
                this._olNamespace = this._app.GetNamespace("MAPI");
                this._inboxFolder = _olNamespace.GetDefaultFolder(OlDefaultFolders.olFolderInbox);
                _log.Trace("Launching Outlook");
                this._inboxFolder.Display();

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
                foreach (var timelineEvent in handler.TimeLineEvents)
                {
                    WorkingHours.Is(handler);

                    if (timelineEvent.DelayBefore > 0)
                        Thread.Sleep(timelineEvent.DelayBefore);

                    switch (timelineEvent.Command.ToUpper())
                    {
                        default:
                            try
                            {
                                var emailConfig = new EmailConfiguration(timelineEvent.CommandArgs);
                                if (SendEmailViaOutlook(emailConfig))
                                {
                                    this.Report(handler.HandlerType.ToString(), timelineEvent.Command, emailConfig.ToString());
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
                                var emailConfig = new EmailConfiguration(timelineEvent.CommandArgs);
                                if (ReplyViaOutlook(emailConfig))
                                {
                                    this.Report(handler.HandlerType.ToString(), timelineEvent.Command, emailConfig.ToString());
                                }
                            }
                            catch (Exception e)
                            {
                                _log.Error(e);
                            }
                            break;
                    }

                    if (timelineEvent.DelayAfter > 0)
                        Thread.Sleep(timelineEvent.DelayAfter);
                }
            }
            catch (Exception e)
            {
                _log.Debug(e);
            }
        }

        private bool ReplyViaOutlook(EmailConfiguration emailConfig)
        {
            try
            {
                var inboxItems = this._inboxFolder.Items;

                foreach (MailItem inboxItem in inboxItems)
                {
                    if (inboxItem.UnRead)
                    {
                        var emailReply = new EmailReplyManager();

                        //todo: use emailConfig!

                        inboxItem.HTMLBody = $"{emailReply.Reply} {Environment.NewLine}{Environment.NewLine}ORIGINAL MESSAGE --- {Environment.NewLine}{Environment.NewLine}{inboxItem.Body}"; ;
                        inboxItem.Subject = $"RE: {inboxItem.Subject}";

                        var replyMail = inboxItem.Reply();

                        //send it
                        //replyMail.Send();
                        //now using redemption

                        var rdoMail = new Redemption.SafeMailItem();
                        rdoMail.Item = replyMail;
                        rdoMail.Send();

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
            var config = Program.Configuration.Email;
            var wasSuccessful = false;

            try
            {
                //now create mail object (but we'll not send it via outlook)
                _log.Trace("Creating outlook mail item");
                var mailItem = _app.CreateItem(OlItemType.olMailItem);

                //Add subject
                if (!string.IsNullOrWhiteSpace(emailConfig.Subject))
                    mailItem.Subject = emailConfig.Subject;
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
                    foreach (var path in emailConfig.Attachments)
                    {
                        mailItem.Attachments.Add(path);
                        _log.Trace($"Adding attachment from: {path}");
                    }
                }

                if (config.SetAccountFromConfig || config.SetAccountFromLocal)
                {
                    var accounts = _app.Session.Accounts;
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

                /*
                send prep! typically we would use:
                ((_MailItem)mailItem).Send();
                now using redemption
                
                python example
                new_email = win32com.client.Dispatch('Redemption.SafeMailItem')
                new_email.Item = outlook_mail_item
                new_email.Recipients.Add(self._config['destination'])
                new_email.Recipients.ResolveAll()
                new_email.Send() 
                 */

                if (config.SaveToOutbox)
                {
                    _log.Trace("Saving mailItem to outbox...");
                    mailItem.Move(this._olNamespace.GetDefaultFolder(OlDefaultFolders.olFolderOutbox));
                    mailItem.Save();
                }

                _log.Trace("Attempting new Redemtion SafeMailItem...");
                var rdoMail = new Redemption.SafeMailItem();
                rdoMail.Item = mailItem;
                //Parse To
                if (emailConfig.To.Count > 0)
                {
                    var list = emailConfig.To.Distinct();
                    foreach (var a in list)
                    {
                        var r = rdoMail.Recipients.AddEx(a.Trim());
                        r.Resolve();
                        _log.Trace($"RdoMail TO {a.Trim()}");
                    }
                }
                else
                    throw new Exception("Must specify to-address");

                //Parse Cc
                if (emailConfig.Cc.Count > 0)
                {
                    var list = emailConfig.Cc.Distinct();
                    foreach (var a in list)
                    {
                        var r = rdoMail.Recipients.AddEx(a.Trim());
                        r.Resolve();
                        if (r.Resolved)
                            r.Type = 2; //CC
                        _log.Trace($"RdoMail CC {a.Trim()}");
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
                            r.Type = 3; //BCC
                        _log.Trace($"RdoMail BCC {a.Trim()}");
                    }
                }

                _log.Trace("Attempting to send Redemtion SafeMailItem...");
                rdoMail.Send();

                //Done
                wasSuccessful = true;

                _log.Trace("Redemtion SafeMailItem was sent successfully");

                if (config.SetForcedSendReceive)
                {
                    _log.Trace("Forcing mapi - send and receive");
                    this._olNamespace.SendAndReceive(false);
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