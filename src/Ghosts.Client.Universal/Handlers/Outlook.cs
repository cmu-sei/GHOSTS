// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Client.Universal.Infrastructure.Email;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Search;
using MailKit.Security;
using MimeKit;
using ReportItem = Ghosts.Domain.Code.ReportItem;

namespace Ghosts.Client.Universal.Handlers;

public class Outlook(Timeline timeline, TimelineHandler handler, CancellationToken token)
    : BaseHandler(timeline, handler, token)
{
    protected override async Task RunOnce()
    {
        // Read SMTP/IMAP settings from HandlerArgs
        if (!Handler.HandlerArgs.TryGetValue("smtp-host", out var smtpHostObj) ||
            string.IsNullOrWhiteSpace(smtpHostObj?.ToString()))
        {
            _log.Error("Outlook handler: smtp-host not configured in HandlerArgs, cannot proceed");
            return;
        }

        var smtpHost = smtpHostObj.ToString();
        var smtpPort = 587;
        if (Handler.HandlerArgs.TryGetValue("smtp-port", out var smtpPortObj))
            int.TryParse(smtpPortObj.ToString(), out smtpPort);

        var smtpUseSsl = true;
        if (Handler.HandlerArgs.TryGetValue("smtp-use-ssl", out var smtpSslObj))
            bool.TryParse(smtpSslObj.ToString(), out smtpUseSsl);

        var imapHost = smtpHost;
        if (Handler.HandlerArgs.TryGetValue("imap-host", out var imapHostObj) &&
            !string.IsNullOrWhiteSpace(imapHostObj?.ToString()))
            imapHost = imapHostObj.ToString();

        var imapPort = 993;
        if (Handler.HandlerArgs.TryGetValue("imap-port", out var imapPortObj))
            int.TryParse(imapPortObj.ToString(), out imapPort);

        var imapUseSsl = true;
        if (Handler.HandlerArgs.TryGetValue("imap-use-ssl", out var imapSslObj))
            bool.TryParse(imapSslObj.ToString(), out imapUseSsl);

        var username = string.Empty;
        if (Handler.HandlerArgs.TryGetValue("username", out var usernameObj))
            username = usernameObj?.ToString() ?? string.Empty;

        var password = string.Empty;
        if (Handler.HandlerArgs.TryGetValue("password", out var passwordObj))
            password = passwordObj?.ToString() ?? string.Empty;

        var jitterFactor = 0;
        if (Handler.HandlerArgs.TryGetValue("delay-jitter", out var jitterObj))
            jitterFactor = Jitter.JitterFactorParse(jitterObj.ToString());

        foreach (var timelineEvent in Handler.TimeLineEvents)
        {
            Token.ThrowIfCancellationRequested();

            WorkingHours.Is(Handler);

            if (timelineEvent.DelayBeforeActual > 0)
                Thread.Sleep(timelineEvent.DelayBeforeActual);

            try
            {
                switch (timelineEvent.Command?.ToUpper())
                {
                    case "REPLY":
                        await HandleReply(smtpHost, smtpPort, smtpUseSsl, imapHost, imapPort, imapUseSsl, username, password, timelineEvent);
                        break;
                    case "READ":
                        await HandleRead(imapHost, imapPort, imapUseSsl, username, password, timelineEvent);
                        break;
                    default:
                        await HandleSend(smtpHost, smtpPort, smtpUseSsl, username, password, timelineEvent);
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                _log.Error($"Outlook handler error during {timelineEvent.Command}: {e}");
            }

            if (timelineEvent.DelayAfterActual > 0)
                Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, jitterFactor));
        }
    }

    private async Task HandleSend(string smtpHost, int smtpPort, bool smtpUseSsl,
        string username, string password, TimelineEvent timelineEvent)
    {
        var emailConfig = new EmailConfiguration(timelineEvent.CommandArgs);
        _log.Trace($"Outlook sending email: {emailConfig}");

        var message = BuildMimeMessage(emailConfig);

        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(smtpHost, smtpPort,
            smtpUseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None, Token);
        await smtp.AuthenticateAsync(username, password, Token);
        await smtp.SendAsync(message, Token);
        await smtp.DisconnectAsync(true, Token);

        _log.Trace($"Outlook email sent successfully to {string.Join(",", emailConfig.To)}");
        Report(new ReportItem
        {
            Handler = Handler.HandlerType.ToString(),
            Command = timelineEvent.Command,
            Arg = emailConfig.ToString(),
            Trackable = timelineEvent.TrackableId
        });
    }

    private async Task HandleReply(string smtpHost, int smtpPort, bool smtpUseSsl,
        string imapHost, int imapPort, bool imapUseSsl,
        string username, string password, TimelineEvent timelineEvent)
    {
        using var imap = new ImapClient();
        await imap.ConnectAsync(imapHost, imapPort, imapUseSsl, Token);
        await imap.AuthenticateAsync(username, password, Token);

        var inbox = imap.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadWrite, Token);

        var uids = await inbox.SearchAsync(SearchQuery.NotSeen, Token);
        if (!uids.Any())
        {
            _log.Trace("Outlook REPLY: no unread messages found");
            await imap.DisconnectAsync(true, Token);
            return;
        }

        var uid = uids.First();
        var original = await inbox.GetMessageAsync(uid, Token);

        // Compose reply
        var emailReply = new EmailReplyManager();
        var replyBody = new StringWriter();
        replyBody.WriteLine(emailReply.Reply);
        replyBody.WriteLine();
        replyBody.WriteLine();
        replyBody.WriteLine($"On {original.Date:f}, {original.From} wrote:");
        using (var reader = new StringReader(original.TextBody ?? string.Empty))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                replyBody.Write("> ");
                replyBody.WriteLine(line);
            }
        }

        var reply = new MimeMessage();
        reply.From.Add(new MailboxAddress(username, username));
        if (original.ReplyTo.Count > 0)
            reply.To.AddRange(original.ReplyTo);
        else
            reply.To.AddRange(original.From);
        reply.Subject = $"RE: {original.Subject}";
        reply.InReplyTo = original.MessageId;
        reply.Body = new TextPart("plain") { Text = replyBody.ToString() };

        // Send reply via SMTP
        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(smtpHost, smtpPort,
            smtpUseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None, Token);
        await smtp.AuthenticateAsync(username, password, Token);
        await smtp.SendAsync(reply, Token);
        await smtp.DisconnectAsync(true, Token);

        // Mark original as read
        await inbox.AddFlagsAsync(uid, MessageFlags.Seen, true, Token);
        await imap.DisconnectAsync(true, Token);

        _log.Trace($"Outlook replied to message from {original.From}");
        Report(new ReportItem
        {
            Handler = Handler.HandlerType.ToString(),
            Command = timelineEvent.Command,
            Arg = $"Reply to {original.From} RE: {original.Subject}",
            Trackable = timelineEvent.TrackableId
        });
    }

    private async Task HandleRead(string imapHost, int imapPort, bool imapUseSsl,
        string username, string password, TimelineEvent timelineEvent)
    {
        using var imap = new ImapClient();
        await imap.ConnectAsync(imapHost, imapPort, imapUseSsl, Token);
        await imap.AuthenticateAsync(username, password, Token);

        var inbox = imap.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadWrite, Token);

        var uids = await inbox.SearchAsync(SearchQuery.NotSeen, Token);
        if (!uids.Any())
        {
            _log.Trace("Outlook READ: no unread messages found");
            await imap.DisconnectAsync(true, Token);
            return;
        }

        var uid = uids.First();
        var message = await inbox.GetMessageAsync(uid, Token);

        // Mark as read
        await inbox.AddFlagsAsync(uid, MessageFlags.Seen, true, Token);
        await imap.DisconnectAsync(true, Token);

        _log.Trace($"Outlook read message: {message.Subject} from {message.From}");
        Report(new ReportItem
        {
            Handler = Handler.HandlerType.ToString(),
            Command = timelineEvent.Command,
            Arg = $"Read: {message.Subject} from {message.From}",
            Trackable = timelineEvent.TrackableId
        });
    }

    private static MimeMessage BuildMimeMessage(EmailConfiguration emailConfig)
    {
        var message = new MimeMessage();

        message.From.Add(new MailboxAddress(emailConfig.From, emailConfig.From));

        foreach (var to in emailConfig.To)
            message.To.Add(new MailboxAddress(to, to));
        foreach (var cc in emailConfig.Cc)
            message.Cc.Add(new MailboxAddress(cc, cc));
        foreach (var bcc in emailConfig.Bcc)
            message.Bcc.Add(new MailboxAddress(bcc, bcc));

        message.Subject = emailConfig.Subject;

        // Build body
        var textPart = emailConfig.BodyType switch
        {
            EmailConfiguration.EmailBodyType.HTML => new TextPart("html") { Text = emailConfig.Body },
            EmailConfiguration.EmailBodyType.RTF => new TextPart("rtf") { Text = emailConfig.Body },
            _ => new TextPart("plain") { Text = emailConfig.Body }
        };

        if (emailConfig.Attachments.Count > 0)
        {
            var multipart = new Multipart("mixed");
            multipart.Add(textPart);

            foreach (var attachmentPath in emailConfig.Attachments)
            {
                var attachment = new MimePart()
                {
                    Content = new MimeContent(File.OpenRead(attachmentPath)),
                    ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                    ContentTransferEncoding = ContentEncoding.Base64,
                    FileName = Path.GetFileName(attachmentPath)
                };
                multipart.Add(attachment);
            }

            message.Body = multipart;
        }
        else
        {
            message.Body = textPart;
        }

        return message;
    }
}
