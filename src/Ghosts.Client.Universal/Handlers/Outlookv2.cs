// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Client.Universal.Infrastructure;
using Ghosts.Client.Universal.Infrastructure.Email;
using Microsoft.Office.Interop.Outlook;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Ghosts.Domain.Code.Helpers;
using System.Reflection;
using Redemption;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Search;
using MimeKit;
using Newtonsoft.Json;
using ReportItem = Ghosts.Domain.Code.ReportItem;

namespace Ghosts.Client.Universal.Handlers;

public class Outlookv2(Timeline timeline, TimelineHandler handler, CancellationToken token)
    : BaseHandler(timeline, handler, token)
{
    private Microsoft.Office.Interop.Outlook.Application _app;
    private NameSpace _oMapiNamespace;
    private RDOFolder _folderOutbox;
    private RDOFolder _folderInbox;
    private RDOFolder _folderDeletedItems;
    private RDOFolder _folderSentMail;
    private RDOFolder _folderJunk;
    private Microsoft.Office.Interop.Outlook.MAPIFolder _folderInboxMapi;
    private RDOFolder _folderDrafts;


    // Primary action probabilities
    private int _readProbability = 25;
    private int _deleteProbability = 25;
    private int _createProbability = 25;
    private int _replyProbability = 25;

    // Additional action probabilities
    private int _attachmentProbability;
    private int _saveAttachmentProbability;
    private int _downloadProbability = 0; //when reading, probability to download an attachment
    private int _clickProbability = 0;   //when reading, probability to click on a link

    // Jitter
    private int _jitterFactor;

    // Action/error tracking
    private int _actionCount;
    private int _replyErrorCount;
    private int _totalErrorCount;
    private const int _replyErrorThreshold = 10;
    private const int _restartThreshold = 20;

    // SMTP/IMAP configuration
    private string _smtpHost;
    private int _smtpPort = 587;
    private bool _smtpUseSsl = true;
    private string _imapHost;
    private int _imapPort = 993;
    private bool _imapUseSsl = true;
    private string _username;
    private string _password;

    // File/attachment settings
    private string _inputDirectory;
    private string _outputDirectory;
    private int _attachmentsMin = 1;
    private int _attachmentsMax = 3;
    private int _attachmentsMaxSize = 10;

    // Domain email list for random recipient selection
    private List<string> _domainEmailList;

    // Timing
    private int _initialDelay = 180000;
    private bool _firstRun = true;

    private readonly string[] _actionList = { "read", "reply", "create", "delete" };

   protected override Task RunOnce()
    {
        if (!OperatingSystem.IsWindows())
        {
            _log.Info("Outlookv2 handler automation is not currently supported on this OS");
            return Task.CompletedTask;
        }
        try
        {
            _log.Trace($"Outlookv2:: Making RDO session");    
            MakeRdoSession();
        }
        
        catch (System.Exception e)
        {
            _log.Error($"Outlookv2:: RDO session creation error {e}");
            return Task.CompletedTask;
        }

        try
        {
            ParseHandlerArgs();
        }
        catch (System.Exception e)
        {
            if (e is ThreadAbortException || e is ThreadInterruptedException || e is OperationCanceledException)
            {
                throw;
            }
            _log.Trace("Outlookv2:: Error parsing handler arguments.");
            _log.Error(e);
            return Task.CompletedTask;
        }


        try
        {
            InitialDisplay();

            if (handler.Loop)
            {
                while (true)
                {
                    ExecuteEvents();
                    if (_totalErrorCount > _restartThreshold )
                    {
                        _totalErrorCount = 0;
                        _log.Trace("Outlookv2:: Total successive error count exceeded threshold, Outlookv2 restarting.");
                        //doRestart();
                    }
                }
            }
            else
            {
                ExecuteEvents();
            }
        }
        catch (System.Exception e)
        {
            if (e is ThreadAbortException || e is ThreadInterruptedException || e is OperationCanceledException)
            {
                throw;
            }
            _log.Error(e);
        }
        return Task.CompletedTask;
    }

    public void ExecuteEvents()
    {
        
        foreach (var timelineEvent in Handler.TimeLineEvents)
        {
            Token.ThrowIfCancellationRequested();

            // If too many reply errors, redistribute reply probability to create
            if (_replyProbability > 0 && _replyErrorCount > _replyErrorThreshold)
            {
                _createProbability += _replyProbability;
                _replyProbability = 0;
                _log.Trace("Outlookv2:: Too many reply errors, added reply probability to create, and halted future reply actions.");
            }

            var probabilityList = new[] { _readProbability, _replyProbability, _createProbability, _deleteProbability };
            var action = SelectActionFromProbabilities(probabilityList, _actionList);

            // Prime the inbox with creates for the first 5 actions
            if (_actionCount < 5) action = "create";

            if (action == null)
            {
                _log.Trace("Outlookv2:: No action this cycle.");
                continue;
            }

            WorkingHours.Is(Handler);

            if (timelineEvent.DelayBeforeActual > 0)
            {
                var delayBefore = Jitter.JitterFactorDelay(timelineEvent.DelayBeforeActual, _jitterFactor);
                _log.Trace($"Outlookv2:: DelayBefore sleeping for {delayBefore} ms");
                if (Token.WaitHandle.WaitOne(delayBefore)) Token.ThrowIfCancellationRequested();
                
            }

            _log.Trace($"Outlookv2:: Performing action {action}.");
            var success = false;
            EmailConfiguration emailConfig;

            try
            {
                switch (action)
                {
                    case "create":
                        emailConfig = new EmailConfiguration(timelineEvent.CommandArgs, _domainEmailList);
                        if (SendEmailViaOutlook(emailConfig))
                        {
                            _log.Trace("Outlookv2:: Created email");
                            _totalErrorCount = 0;  //zero on success
                        }
                        else
                        {
                            _totalErrorCount++;
                        }
                           
                        break;
                    case "reply":
                        emailConfig = new EmailConfiguration(timelineEvent.CommandArgs, _domainEmailList);
                        if (ReplyViaOutlook(emailConfig))
                        {
                            
                            _log.Trace("Outlookv2:: Replied email");
                            _totalErrorCount = 0;  //zero on success
                        }
                        else
                        {
                            _totalErrorCount++;
                        }
                        
                        break;
                    case "read":
                        if (ReadViaOutlook(timelineEvent))
                        {
                            _log.Trace("Outlookv2:: Read email");
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
                            _log.Trace("Outlookv2:: Deleted email");
                            _totalErrorCount = 0;  //zero on success
                        }
                        else
                        {
                            _totalErrorCount++;
                        }
                        break;
                }
            }
            catch (System.Exception e)
            {
                if (e is ThreadAbortException || e is ThreadInterruptedException || e is OperationCanceledException)
                {
                    throw;
                }
                _log.Error($"Outlookv2:: Error during action '{action}': {e.Message}");
                _log.Debug(e);
            }

            if (success)
            {
                _totalErrorCount = 0;
            }
            else
            {
                _totalErrorCount++;
            }

            Report(new ReportItem
            {
                Handler = Handler.HandlerType.ToString(),
                Command = action,
                Arg = string.Empty,
                Trackable = timelineEvent.TrackableId
            });

            if (timelineEvent.DelayAfterActual > 0)
            {
                var delayAfter = Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, _jitterFactor);
                _log.Trace($"Outlookv2:: DelayAfter sleeping for {delayAfter} ms");
                if (Token.WaitHandle.WaitOne(delayAfter)) Token.ThrowIfCancellationRequested();
            }

            if (_actionCount < 100) _actionCount++;
        }

        if (_totalErrorCount > _restartThreshold)
        {
            _log.Trace("Outlookv2:: Total successive error count exceeded threshold, resetting counts.");
            _totalErrorCount = 0;
            _replyErrorCount = 0;
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
            _log.Trace("Creating new RDO session");
            var session = RedemptionLoader.new_RDOSession();
            _log.Trace("Attempting RDO session logon...");
            session.Logon(Missing.Value, Missing.Value, Missing.Value, Missing.Value, Missing.Value, Missing.Value);
            _folderInbox = session.GetDefaultFolder(rdoDefaultFolders.olFolderInbox);
            _folderOutbox = session.GetDefaultFolder(rdoDefaultFolders.olFolderOutbox);
            _folderDeletedItems = session.GetDefaultFolder(rdoDefaultFolders.olFolderDeletedItems);
            _folderDrafts = session.GetDefaultFolder(rdoDefaultFolders.olFolderDrafts);
            _folderSentMail = session.GetDefaultFolder(rdoDefaultFolders.olFolderSentMail);
            _folderJunk = session.GetDefaultFolder(rdoDefaultFolders.olFolderJunk);
        }

        catch (System.Exception e)
        {
            if (e is ThreadAbortException || e is ThreadInterruptedException || e is OperationCanceledException)
            {
                throw;
            }
            _log.Error($"Outlookv2:: RDO load error {e}");
        }

    }

    public void InitialDisplay()
    {
        _app = new Microsoft.Office.Interop.Outlook.Application();
        _oMapiNamespace = _app.GetNamespace("MAPI");
        _folderInboxMapi = _oMapiNamespace.GetDefaultFolder(OlDefaultFolders.olFolderInbox);      
        _log.Trace("Launching Outlook");
        _folderInboxMapi.Display();
    }

    private async Task<bool> CreateEmail(TimelineEvent timelineEvent)
    {
        try
        {
            var emailConfig = new EmailConfiguration(timelineEvent.CommandArgs, _domainEmailList);
            _log.Trace($"Outlookv2:: Creating email - {emailConfig}");

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(string.Empty, _username));

            foreach (var to in emailConfig.To)
                message.To.Add(new MailboxAddress(string.Empty, to.Trim()));
            foreach (var cc in emailConfig.Cc)
                message.Cc.Add(new MailboxAddress(string.Empty, cc.Trim()));
            foreach (var bcc in emailConfig.Bcc)
                message.Bcc.Add(new MailboxAddress(string.Empty, bcc.Trim()));

            message.Subject = emailConfig.Subject;

            var bodyBuilder = new BodyBuilder();

            switch (emailConfig.BodyType)
            {
                case EmailConfiguration.EmailBodyType.HTML:
                    bodyBuilder.HtmlBody = emailConfig.Body;
                    break;
                case EmailConfiguration.EmailBodyType.RTF:
                case EmailConfiguration.EmailBodyType.PlainText:
                default:
                    bodyBuilder.TextBody = emailConfig.Body;
                    break;
            }

            // Handle attachments from config
            var attachments = emailConfig.Attachments;

            // If no configured attachments, check probability for random attachments
            if (attachments.Count == 0 && _attachmentProbability > 0 && _random.Next(0, 100) <= _attachmentProbability)
            {
                var numAttachments = _random.Next(_attachmentsMin, _attachmentsMax + 1);
                if (numAttachments > 0)
                {
                    attachments = GetRandomFiles(_inputDirectory, "*", numAttachments, _attachmentsMaxSize);
                }
            }

            if (attachments != null && attachments.Count > 0)
            {
                foreach (var path in attachments)
                {
                    if (File.Exists(path))
                    {
                        bodyBuilder.Attachments.Add(path);
                        _log.Trace($"Outlookv2:: Adding attachment: {path}");
                    }
                }
            }

            message.Body = bodyBuilder.ToMessageBody();

            using var smtpClient = new MailKit.Net.Smtp.SmtpClient();
            await smtpClient.ConnectAsync(_smtpHost, _smtpPort, _smtpUseSsl, Token);
            await smtpClient.AuthenticateAsync(_username, _password, Token);
            await smtpClient.SendAsync(message, Token);
            await smtpClient.DisconnectAsync(true, Token);

            _log.Trace("Outlookv2:: Email created and sent successfully.");
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (System.Exception e)
        {
            _log.Error($"Outlookv2:: Error creating email: {e.Message}");
            _log.Debug(e);
            return false;
        }
    }

    private async Task<bool> ReplyToEmail(TimelineEvent timelineEvent)
    {
        try
        {
            var settings = Program.Configuration.Email;
            string[] emailNoReply = null;
            if (!string.IsNullOrEmpty(settings.EmailNoReply))
            {
                emailNoReply = settings.EmailNoReply.ToLower().Split(',');
            }

            using var imapClient = new ImapClient();
            await imapClient.ConnectAsync(_imapHost, _imapPort, _imapUseSsl, Token);
            await imapClient.AuthenticateAsync(_username, _password, Token);

            var inbox = imapClient.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadWrite, Token);

            var uids = await inbox.SearchAsync(SearchQuery.NotSeen, Token);
            if (uids.Count == 0)
            {
                _log.Trace("Outlookv2:: No unread messages to reply to.");
                await imapClient.DisconnectAsync(true, Token);
                return false;
            }

            foreach (var uid in uids)
            {
                var originalMessage = await inbox.GetMessageAsync(uid, Token);
                var senderAddress = originalMessage.From.Mailboxes.FirstOrDefault()?.Address?.ToLower();

                if (string.IsNullOrEmpty(senderAddress)) continue;

                // Check no-reply filter
                var reject = false;
                if (emailNoReply != null)
                {
                    foreach (var target in emailNoReply)
                    {
                        if (senderAddress.Contains(target.Trim()))
                        {
                            reject = true;
                            break;
                        }
                    }
                }

                if (reject)
                {
                    _log.Trace($"Outlookv2:: Rejecting reply to address: {senderAddress} as it matches no-reply filter.");
                    continue;
                }

                // Build reply
                var emailReply = new EmailReplyManager();
                var replyMessage = new MimeMessage();
                replyMessage.From.Add(new MailboxAddress(string.Empty, _username));
                replyMessage.To.Add(new MailboxAddress(string.Empty, senderAddress));
                replyMessage.Subject = $"RE: {originalMessage.Subject}";

                // Set In-Reply-To and References headers
                if (!string.IsNullOrEmpty(originalMessage.MessageId))
                {
                    replyMessage.InReplyTo = originalMessage.MessageId;
                    foreach (var id in originalMessage.References)
                        replyMessage.References.Add(id);
                    replyMessage.References.Add(originalMessage.MessageId);
                }

                // Build quoted body
                using (var quoted = new StringWriter())
                {
                    quoted.WriteLine(emailReply.Reply);
                    quoted.WriteLine();
                    quoted.WriteLine();
                    var sentOn = originalMessage.Date.ToString("f");
                    quoted.WriteLine($"On {sentOn}, {senderAddress} wrote:");
                    var originalBody = originalMessage.TextBody ?? string.Empty;
                    using (var reader = new StringReader(originalBody))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            quoted.Write("> ");
                            quoted.WriteLine(line);
                        }
                    }

                    replyMessage.Body = new TextPart("plain") { Text = quoted.ToString() };
                }

                // Send reply via SMTP
                using var smtpClient = new MailKit.Net.Smtp.SmtpClient();
                await smtpClient.ConnectAsync(_smtpHost, _smtpPort, _smtpUseSsl, Token);
                await smtpClient.AuthenticateAsync(_username, _password, Token);
                await smtpClient.SendAsync(replyMessage, Token);
                await smtpClient.DisconnectAsync(true, Token);

                // Mark original as read
                await inbox.AddFlagsAsync(uid, MessageFlags.Seen, true, Token);

                _log.Trace("Outlookv2:: Reply action completed.");
                await imapClient.DisconnectAsync(true, Token);
                return true;
            }

            await imapClient.DisconnectAsync(true, Token);
            _log.Trace("Outlookv2:: No suitable messages found to reply to.");
            return false;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (System.Exception e)
        {
            _log.Error($"Outlookv2:: Error replying to email: {e.Message}");
            _log.Debug(e);
            _replyErrorCount++;
            return false;
        }
    }

    private async Task<bool> ReadEmail()
    {
        try
        {
            using var imapClient = new ImapClient();
            await imapClient.ConnectAsync(_imapHost, _imapPort, _imapUseSsl, Token);
            await imapClient.AuthenticateAsync(_username, _password, Token);

            var inbox = imapClient.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadWrite, Token);

            var uids = await inbox.SearchAsync(SearchQuery.NotSeen, Token);

            if (uids.Count == 0)
            {
                _log.Trace("Outlookv2:: No unread messages to read.");
                await imapClient.DisconnectAsync(true, Token);
                return false;
            }

            var uid = uids[0];
            var message = await inbox.GetMessageAsync(uid, Token);

            // Mark as read
            await inbox.AddFlagsAsync(uid, MessageFlags.Seen, true, Token);
            _log.Trace($"Outlookv2:: Read message from {message.From} subject '{message.Subject}'");

            // Optionally save attachments
            if (_saveAttachmentProbability > 0 && message.Attachments.Any())
            {
                foreach (var attachment in message.Attachments)
                {
                    if (_random.Next(0, 100) > _saveAttachmentProbability) continue;

                    var fileName = attachment.ContentDisposition?.FileName ?? attachment.ContentType.Name ?? "attachment";
                    var outPath = Path.Combine(_outputDirectory, fileName);

                    if (File.Exists(outPath))
                        File.Delete(outPath);

                    using var stream = File.Create(outPath);
                    if (attachment is MimePart part)
                    {
                        await part.Content.DecodeToAsync(stream, Token);
                    }

                    _log.Trace($"Outlookv2:: Saved attachment to {outPath}");
                }
            }

            await imapClient.DisconnectAsync(true, Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (System.Exception e)
        {
            _log.Error($"Outlookv2:: Error reading email: {e.Message}");
            _log.Debug(e);
            return false;
        }
    }

    private async Task<bool> DeleteEmail()
    {
        try
        {
            var settings = Program.Configuration.Email;
            if (settings.EmailsMax <= 0)
            {
                _log.Trace("Outlookv2:: EmailsMax in application.json must be > 0 for deletion to occur.");
                return true;
            }

            using var imapClient = new ImapClient();
            await imapClient.ConnectAsync(_imapHost, _imapPort, _imapUseSsl, Token);
            await imapClient.AuthenticateAsync(_username, _password, Token);

            var inbox = imapClient.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadWrite, Token);

            var allUids = await inbox.SearchAsync(SearchQuery.All, Token);
            if (allUids.Count <= settings.EmailsMax)
            {
                _log.Trace($"Outlookv2:: Inbox count ({allUids.Count}) is within threshold ({settings.EmailsMax}), no deletion needed.");
                await imapClient.DisconnectAsync(true, Token);
                return true;
            }

            // Delete oldest messages beyond the threshold
            var countToDelete = allUids.Count - settings.EmailsMax;
            var uidsToDelete = allUids.Take(countToDelete).ToList();

            // Try to move to Trash folder, otherwise mark as deleted
            var trashFolder = (await imapClient.GetFoldersAsync(imapClient.PersonalNamespaces[0], cancellationToken: Token))
                .FirstOrDefault(f => f.Attributes.HasFlag(FolderAttributes.Trash));

            if (trashFolder != null)
            {
                await inbox.MoveToAsync(uidsToDelete, trashFolder, Token);
                _log.Trace($"Outlookv2:: Moved {uidsToDelete.Count} messages to Trash.");
            }
            else
            {
                await inbox.AddFlagsAsync(uidsToDelete, MessageFlags.Deleted, true, Token);
                await inbox.ExpungeAsync(Token);
                _log.Trace($"Outlookv2:: Marked {uidsToDelete.Count} messages as deleted and expunged.");
            }

            await imapClient.DisconnectAsync(true, Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (System.Exception e)
        {
            _log.Error($"Outlookv2:: Error deleting email: {e.Message}");
            _log.Debug(e);
            return false;
        }
    }

    private void ParseHandlerArgs()
    {
        var args = Handler.HandlerArgs;
        if (args == null) return;

        if (args.ContainsKey("initial-outlook-delay"))
        {
            int.TryParse(args["initial-outlook-delay"].ToString(), out _initialDelay);
            if (_initialDelay < 0) _initialDelay = 0;
        }

        if (args.ContainsKey("read-probability"))
        {
            int.TryParse(args["read-probability"].ToString(), out _readProbability);
            if (!CheckProbabilityVar("read-probability", _readProbability))
                _readProbability = 0;
        }

        if (args.ContainsKey("delete-probability"))
        {
            int.TryParse(args["delete-probability"].ToString(), out _deleteProbability);
            if (!CheckProbabilityVar("delete-probability", _deleteProbability))
                _deleteProbability = 0;
        }

        if (args.ContainsKey("create-probability"))
        {
            int.TryParse(args["create-probability"].ToString(), out _createProbability);
            if (!CheckProbabilityVar("create-probability", _createProbability))
                _createProbability = 0;
        }

        if (args.ContainsKey("reply-probability"))
        {
            int.TryParse(args["reply-probability"].ToString(), out _replyProbability);
            if (!CheckProbabilityVar("reply-probability", _replyProbability))
                _replyProbability = 0;
        }

        var totalProbability = _readProbability + _deleteProbability + _createProbability + _replyProbability;
        if (totalProbability > 100 || totalProbability == 0)
        {
            _log.Trace($"Outlookv2:: Probability sum is {totalProbability}, using defaults.");
            _readProbability = 25;
            _deleteProbability = 10;
            _createProbability = 40;
            _replyProbability = 25;
        }

        if (args.ContainsKey("attachment-probability"))
        {
            int.TryParse(args["attachment-probability"].ToString(), out _attachmentProbability);
            if (!CheckProbabilityVar("attachment-probability", _attachmentProbability))
                _attachmentProbability = 0;
        }

        if (args.ContainsKey("save-attachment-probability"))
        {
            int.TryParse(args["save-attachment-probability"].ToString(), out _saveAttachmentProbability);
            if (!CheckProbabilityVar("save-attachment-probability", _saveAttachmentProbability))
                _saveAttachmentProbability = 0;
        }

        if (args.ContainsKey("delay-jitter"))
        {
            _jitterFactor = Jitter.JitterFactorParse(args["delay-jitter"].ToString());
            if (_jitterFactor < 0) _jitterFactor = 0;
        }

        if (handler.HandlerArgs.ContainsKey("download-probability"))
        {
            int.TryParse(handler.HandlerArgs["download-probability"].ToString(), out _downloadProbability);
            if (!CheckProbabilityVar(handler.HandlerArgs["download-probability"].ToString(), _downloadProbability))
            {
                _downloadProbability = 0;
            }
        }

        if (handler.HandlerArgs.ContainsKey("click-probability"))
        {
            int.TryParse(handler.HandlerArgs["click-probability"].ToString(), out _clickProbability);
            if (!CheckProbabilityVar(handler.HandlerArgs["click-probability"].ToString(), _clickProbability))
            {
                _clickProbability = 0;
            }
        }

        if (args.ContainsKey("input-directory"))
        {
            var targetDir = Environment.ExpandEnvironmentVariables(args["input-directory"].ToString());
            if (Directory.Exists(targetDir))
                _inputDirectory = targetDir;
            else
                _log.Trace($"Outlookv2:: Input directory {targetDir} does not exist, using downloads directory.");
        }

        _inputDirectory ??= KnownFolders.GetDownloadFolderPath();

        if (args.ContainsKey("output-directory"))
        {
            var targetDir = Environment.ExpandEnvironmentVariables(args["output-directory"].ToString());
            if (!Directory.Exists(targetDir))
            {
                try
                {
                    Directory.CreateDirectory(targetDir);
                    _outputDirectory = targetDir;
                }
                catch (System.Exception e)
                {
                    if (e is ThreadAbortException || e is ThreadInterruptedException || e is OperationCanceledException)
                    {
                        throw;
                    }
                    _log.Trace($"Outlookv2:: Output directory {targetDir} cannot be created: {e.Message}");
                }
            }
            else
            {
                _outputDirectory = targetDir;
            }
        }

        _outputDirectory ??= KnownFolders.GetDownloadFolderPath();

        if (args.ContainsKey("min-attachments"))
        {
            int.TryParse(args["min-attachments"].ToString(), out _attachmentsMin);
            if (_attachmentsMin < 0) _attachmentsMin = 1;
        }

        if (args.ContainsKey("max-attachments"))
        {
            int.TryParse(args["max-attachments"].ToString(), out _attachmentsMax);
            if (_attachmentsMax < 0) _attachmentsMax = 3;
        }

        if (_attachmentsMax < _attachmentsMin) _attachmentsMax = _attachmentsMin;

        if (args.ContainsKey("max-attachments-size"))
        {
            int.TryParse(args["max-attachments-size"].ToString(), out _attachmentsMaxSize);
            if (_attachmentsMaxSize <= 0) _attachmentsMaxSize = 10;
        }

        if (args.ContainsKey("domain-addresses"))
        {
            try
            {
                _domainEmailList = JsonConvert.DeserializeObject<List<string>>(args["domain-addresses"].ToString());
            }
            catch (System.Exception e)
            {
                if (e is ThreadAbortException || e is ThreadInterruptedException || e is OperationCanceledException)
                {
                    throw;
                }
                _log.Trace("Outlookv2:: Error parsing domain-addresses list, argument ignored.");
                _domainEmailList = null;
                _log.Error(e);
            }
        }
    }

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
            // Use MAPI for this
            var folderItems = _folderInboxMapi.Items;
            bool replyStarted = false;
            MailItem folderItem;
            foreach (object item in folderItems)
            {
                try
                {
                    folderItem = item as MailItem;
                    if (folderItem == null) continue;
                    bool reject = false;
                    // read sender/body via Redemption to avoid Outlook's security prompt
                    var safeFolderItem = new SafeMailItem { Item = folderItem };
                    var targetEmail = safeFolderItem.SenderEmailAddress.ToLower();
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
                        _log.Trace($"Rejecting reply to address: {targetEmail} as it matches one of: {settings.EmailNoReply} ");
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
                        quoted.WriteLine($"On {folderItem.SentOn:f}, {safeFolderItem.SenderEmailAddress} wrote:");
                        using (var reader = new StringReader(safeFolderItem.Body))
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

                    var r = rdoMail.Recipients.AddEx(safeFolderItem.SenderEmailAddress);
                    r.Resolve();
                    rdoMail.Recipients.ResolveAll();
                    rdoMail.Send();

                    var mapiUtils = new MAPIUtils();
                    mapiUtils.DeliverNow();

                    // mark as read
                    folderItem.UnRead = false;

                    if (config.SetForcedSendReceive)
                    {
                        _log.Trace("Forcing mapi - send and receive, then sleeping for 3000");
                        _oMapiNamespace.SendAndReceive(false);
                        if (Token.WaitHandle.WaitOne(3000)) Token.ThrowIfCancellationRequested();
                    }
                    _log.Trace("Outlookv2:: Reply action completed.");

                    return true;
                }
                catch (System.Exception e)
                {
                    if (e is ThreadAbortException || e is ThreadInterruptedException || e is OperationCanceledException)
                    {
                        throw;
                    }
                    _log.Error($"Outlook reply error: {e}");
                    if (replyStarted)
                    {
                        //we found an email, and tried to reply, but an error occurred.
                        _replyErrorCount += 1;
                        return false;  
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            if (e is ThreadAbortException || e is ThreadInterruptedException || e is OperationCanceledException)
            {
                throw;
            }
            _log.Error($"Outlook reply error: {e}");
        }
        return false;
    }

    private List<string> GetRandomFiles(string targetDir, string pattern, int count, int maxSize)
    {
        try
        {
            if (count == 0 || string.IsNullOrEmpty(targetDir) || !Directory.Exists(targetDir))
                return null;

            // Divide maxSize by count so total attachment size cannot exceed maxSize
            long maxSizeBytes = (maxSize * 1024L * 1024L) / count;

            var filelist = Directory.GetFiles(targetDir, pattern);
            if (filelist.Length == 0) return null;

            // Filter files by maxSizeBytes
            var filteredFiles = new List<string>();
            foreach (var file in filelist)
            {
                try
                {
                    var info = new FileInfo(file);
                    if (info.Length <= maxSizeBytes)
                        filteredFiles.Add(file);
                }
                catch (System.Exception e)
                {
                    if (e is ThreadAbortException || e is ThreadInterruptedException || e is OperationCanceledException)
                    {
                        throw;
                    }
                    _log.Error($"Outlookv2:: File access error during attachment selection: {e.Message}");
                }
            }

            if (filteredFiles.Count == 0) return null;

            if (count == 1)
            {
                return new List<string> { filteredFiles[_random.Next(0, filteredFiles.Count)] };
            }

            // Need more than one, avoid duplicates by pruning down
            while (filteredFiles.Count > count)
            {
                var index = _random.Next(0, filteredFiles.Count);
                filteredFiles.RemoveAt(index);
            }

            return filteredFiles;
        }
        catch (System.Exception e)
        {
            _log.Error($"Outlookv2:: Error in GetRandomFiles: {e.Message}");
            return null;
        }
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
                    throw new System.Exception("Bad email body type: " + emailConfig.BodyType);
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
                    _log.Trace($"RdoMail TO {a.Trim()}");
                }
            }
            else
            {
                throw new System.Exception("Must specify to-address");
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
                    {
                        r.Type = 3; //BCC
                    }

                    _log.Trace($"RdoMail BCC {a.Trim()}");
                }
            }

            rdoMail.Recipients.ResolveAll();

            _log.Trace("Attempting to send Redemtion SafeMailItem...");
            rdoMail.Send();

            var mapiUtils = new MAPIUtils();
            mapiUtils.DeliverNow();

            //Done
            wasSuccessful = true;

            _log.Trace("Redemtion SafeMailItem was sent successfully");

            if (config.SetForcedSendReceive)
            {
                //_log.Trace("Forcing mapi - send and receive, then sleeping for 3000");
                //_oMapiNamespace.SendAndReceive(false);
                //if (Token.WaitHandle.WaitOne(3000)) Token.ThrowIfCancellationRequested();
            }
        }
        catch (System.Exception e)
        {
            if (e is ThreadAbortException || e is ThreadInterruptedException || e is OperationCanceledException)
            {
                throw;
            }
            _log.Error(e);
        }
        _log.Trace($"Returning - wasSuccessful:{wasSuccessful}");
        return wasSuccessful;
    }

    private List<string> DownloadAttachments(MailItem folderItem)
    {
        List<string> retval = null;

        if (_saveAttachmentProbability == 0) return retval;

        try
        {
            if (folderItem.Attachments.Count > 0)
            {
                
                for (int i = 1; i <= folderItem.Attachments.Count; i++)
                {
                    if (_random.Next(0, 100) <= _saveAttachmentProbability)
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
        catch (System.Exception e)
        {
            if (e is ThreadAbortException || e is ThreadInterruptedException || e is OperationCanceledException)
            {
                throw;
            }
            _log.Trace("Outlookv2::  error while dowloading attachments");
            _log.Error(e);
        }

        return retval;  //eventually may want to do something with these attachments
    }

    private void HandleAttachmentsAndLinks(MailItem folderItem, TimelineEvent timelineEvent)
    {
        var savedFiles = DownloadAttachments(folderItem);

        // Run saved attachments based on download-probability
        if (savedFiles != null && _downloadProbability > 0)
        {
            foreach (var filePath in savedFiles)
            {
                if (_random.Next(0, 100) <= _downloadProbability)
                {
                    try
                    {
                        _log.Trace($"Outlookv2:: Executing attachment {filePath}");
                        System.Diagnostics.Process.Start(filePath);
                        Report(new ReportItem { Handler = "Outlookv2", Command = "execute-attachment", Arg = filePath, Trackable = timelineEvent.TrackableId });
                    }
                    catch (System.Exception e)
                    {
                        if (e is ThreadAbortException || e is ThreadInterruptedException || e is OperationCanceledException)
                        {
                            throw;
                        }
                        _log.Trace($"Outlookv2:: Error executing attachment {filePath}");
                        _log.Error(e);
                    }
                }
            }
        }

        // Click a link in the email based on click-probability
        if (_clickProbability > 0 && _random.Next(0, 100) <= _clickProbability)
        {
            try
            {
                // read HTMLBody via Redemption to avoid Outlook's security prompt
                var safeItem = new SafeMailItem { Item = folderItem };
                if (folderItem.BodyFormat == OlBodyFormat.olFormatHTML && safeItem.HTMLBody.Contains("<a href="))
                {
                    var list = DenyListManager.RemoveDeniedFromList(safeItem.HTMLBody.GetHrefUrls());
                    if (list.Any())
                    {
                        var url = list.PickRandom();
                        _log.Trace($"Outlookv2:: Clicking link {url}");
                        url.OpenUrl();
                        Report(new ReportItem { Handler = "Outlookv2", Command = "click-link", Arg = url, Trackable = timelineEvent.TrackableId });
                    }
                }
            }
            catch (System.Exception e)
            {
                if (e is ThreadAbortException || e is ThreadInterruptedException || e is OperationCanceledException)
                {
                    throw;
                }
                _log.Trace("Outlookv2:: Error clicking link in email");
                _log.Error(e);
            }
        }
    }

    private bool ReadViaOutlook(TimelineEvent timelineEvent)
    {
        try
        {
            // Use MAPI for this
            var folderItems = _folderInboxMapi.Items.Restrict("[Unread]=true");
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
                    HandleAttachmentsAndLinks(folderItem, timelineEvent);
                    if (Token.WaitHandle.WaitOne(10000)) Token.ThrowIfCancellationRequested();
                    folderItem.Close(Microsoft.Office.Interop.Outlook.OlInspectorClose.olDiscard);
                    return true;
                }
                catch (System.Exception e)
                {
                    if (e is ThreadAbortException || e is ThreadInterruptedException || e is OperationCanceledException)
                    {
                        throw;
                    }
                    //ignore others
                }
            }
            folderItems = _folderInboxMapi.Items;  //no filter this time
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
                        HandleAttachmentsAndLinks(folderItem, timelineEvent);
                        if (Token.WaitHandle.WaitOne(10000)) Token.ThrowIfCancellationRequested();
                        folderItem.Close(Microsoft.Office.Interop.Outlook.OlInspectorClose.olDiscard);
                        return true;
                    }
                    catch (System.Exception e)
                    {
                        if (e is ThreadAbortException || e is ThreadInterruptedException || e is OperationCanceledException)
                        {
                            throw;
                        }
                        //ignore others
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            if (e is ThreadAbortException || e is ThreadInterruptedException || e is OperationCanceledException)
            {
                throw;
            }
            _log.Error(e);
            return false;
        }

        return false;
    }

    private RDOFolder GetFolder(string folder)
    {
        _log.Trace(folder.ToUpper());
        switch (folder.ToUpper())
        {
            default:
                return  _folderInbox;
            case "OUTBOX":
                return _folderOutbox;
            case "DRAFTS":
                return _folderDrafts;
            case "SENT":
                return _folderSentMail;
            case "DELETED":
            case "DELETEDITEMS":
                return _folderDeletedItems;
            case "JUNK":
                return _folderJunk;
        }
    }

    private bool MoveMailItem(RDOMail item)
    {
        try
        {
            item.Move(_folderDeletedItems);
            if (Token.WaitHandle.WaitOne(500)) Token.ThrowIfCancellationRequested();
            return true;
        }
        catch (System.Exception e)
        {
            if (e is ThreadAbortException || e is ThreadInterruptedException || e is OperationCanceledException)
            {
                throw;
            }
            _log.Trace("Outlookv2:: Error while moving mail item to deleted items folder.");
            _log.Error(e);
            return false;
        }
    }

    private bool DeleteMailItem (RDOMail item)
    {
        try
        {
            item.Delete();
            if (Token.WaitHandle.WaitOne(500)) Token.ThrowIfCancellationRequested();
            return true;
        }
        catch (System.Exception e)
        {
            if (e is ThreadAbortException || e is ThreadInterruptedException || e is OperationCanceledException)
            {
                throw;
            }
            _log.Trace("Outlookv2:: Error while deleting item from deleted items folder.");
            _log.Error(e);
            return false;
        }
    }

    private void CleanDeletedItems()
    {
        var targetFolder = GetFolder("DELETED");
        var folderItems = targetFolder.Items;
        var count = folderItems.Count;
        if (count == 0) return;

        for (int i = count; i > 0; i--)
        {
            try
            {
                object item = folderItems[i];
                var folderItem = item as RDOMail;
                if (folderItem != null)
                {
                    //break if unsuccessful as may have reached the maximum number
                    if (!DeleteMailItem(folderItem)) break; 
                }
            }
            catch (System.Exception e)
            {
                if (e is ThreadAbortException || e is ThreadInterruptedException || e is OperationCanceledException)
                {
                    throw;
                }
                //ignore others
            }
        }
        return;
    }

    private void MoveToDeleted(string targetFolderName, bool deleteAll, bool deleteUnread)
    {
        var targetFolder =  GetFolder(targetFolderName);

        var folderItems = targetFolder.Items;
        var count = folderItems.Count;
        var settings = Program.Configuration.Email;
        if (count == 0) return;

        if (!deleteAll && count <= settings.EmailsMax)
        {
            return; //nothing to do
        }
        
        _log.Trace($"Outlookv2: Beginning deletion in folder {targetFolderName}, found {count} items.");
        foreach (object item in folderItems)
        {
            try
            {
                var folderItem = item as RDOMail;
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
            catch (System.Exception e)
            {
                if (e is ThreadAbortException || e is ThreadInterruptedException || e is OperationCanceledException)
                {
                    throw;
                }
                _log.Error(e);
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
                _log.Trace("Outlookv2: EmailsMax in application.json must be set greater than 0 for cleanup/deletion operation to occur.");
                return true;
            }

            MoveToDeleted("INBOX", false, false);
            MoveToDeleted("INBOX", false, true);
            MoveToDeleted("SENT", false, true);
            MoveToDeleted("DRAFTS", true, true);
            CleanDeletedItems();
        }
        catch (System.Exception e)
        {
            if (e is ThreadAbortException || e is ThreadInterruptedException || e is OperationCanceledException)
            {
                throw;
            }
            _log.Error(e);
            return false;
        }

        return true;
    }

}
