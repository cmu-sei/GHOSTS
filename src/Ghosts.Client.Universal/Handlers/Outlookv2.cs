// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Client.Universal.Infrastructure;
using Ghosts.Client.Universal.Infrastructure.Email;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Ghosts.Domain.Code.Helpers;
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
    // Primary action probabilities
    private int _readProbability = 25;
    private int _deleteProbability = 25;
    private int _createProbability = 25;
    private int _replyProbability = 25;

    // Additional action probabilities
    private int _attachmentProbability;
    private int _saveAttachmentProbability;

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

    protected override async Task RunOnce()
    {
        ParseHandlerArgs();

        if (string.IsNullOrWhiteSpace(_smtpHost))
        {
            _log.Error("Outlookv2:: No smtp-host configured. Cannot proceed.");
            return;
        }

        if (_firstRun)
        {
            _log.Trace($"Outlookv2:: Initial delay sleeping for {_initialDelay} ms");
            await Task.Delay(_initialDelay, Token);
            _firstRun = false;
        }

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
                await Task.Delay(delayBefore, Token);
            }

            _log.Trace($"Outlookv2:: Performing action {action}.");
            var success = false;

            try
            {
                switch (action)
                {
                    case "create":
                        success = await CreateEmail(timelineEvent);
                        break;
                    case "reply":
                        success = await ReplyToEmail(timelineEvent);
                        break;
                    case "read":
                        success = await ReadEmail();
                        break;
                    case "delete":
                        success = await DeleteEmail();
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
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
                await Task.Delay(delayAfter, Token);
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
        catch (Exception e)
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
        catch (Exception e)
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
        catch (Exception e)
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
        catch (Exception e)
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

        if (args.ContainsKey("smtp-host"))
            _smtpHost = args["smtp-host"].ToString();

        if (args.ContainsKey("smtp-port"))
            int.TryParse(args["smtp-port"].ToString(), out _smtpPort);

        if (args.ContainsKey("smtp-use-ssl"))
            bool.TryParse(args["smtp-use-ssl"].ToString(), out _smtpUseSsl);

        if (args.ContainsKey("imap-host"))
            _imapHost = args["imap-host"].ToString();

        if (args.ContainsKey("imap-port"))
            int.TryParse(args["imap-port"].ToString(), out _imapPort);

        if (args.ContainsKey("imap-use-ssl"))
            bool.TryParse(args["imap-use-ssl"].ToString(), out _imapUseSsl);

        if (args.ContainsKey("username"))
            _username = args["username"].ToString();

        if (args.ContainsKey("password"))
            _password = args["password"].ToString();

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
                catch (Exception ex)
                {
                    _log.Trace($"Outlookv2:: Output directory {targetDir} cannot be created: {ex.Message}");
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
            catch (Exception e)
            {
                _log.Trace("Outlookv2:: Error parsing domain-addresses list, argument ignored.");
                _domainEmailList = null;
                _log.Error(e);
            }
        }
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
                catch (Exception e)
                {
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
        catch (Exception e)
        {
            _log.Error($"Outlookv2:: Error in GetRandomFiles: {e.Message}");
            return null;
        }
    }
}
