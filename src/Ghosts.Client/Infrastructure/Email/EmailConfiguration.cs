// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;
using System.IO;
using System.Linq;
using Ghosts.Domain.Code;
using Ghosts.Domain.Code.Helpers;
using Newtonsoft.Json;
using NLog;

namespace Ghosts.Client.Infrastructure.Email;

/// <summary>
/// "CurrentUser|Other:string FromEmailAddress",
/// "Random|Other:string ToEmailAddress - comma separate multiples",
/// "Random|Other:string CcEmailAddress - comma separate multiples",
/// "Random|Other:string BccEmailAddress - comma separate multiples",
/// "Random|Other:string Subject",
/// "Random|Other:string Body",
/// "PlainText|RTF|HTML enum BodyType",
/// "string Attachments - comma separate multiples"
/// </summary>
public class EmailConfiguration
{
    public enum EmailBodyType
    {
        PlainText,
        RTF,
        HTML
    }

    private static readonly Logger _log = LogManager.GetCurrentClassLogger();

    public Guid Id { get; }
    /// <summary>
    /// email address sending from (ex: "me@somewhere.com") -- this account must exist in Outlook. Only one email address is allowed!
    /// </summary>
    public string From { get; }
    /// <summary>
    /// email address sending to. Can be multiple. In that case separate with commas (ex: "recipient@gmail.com", or "recipient1@mail.mil,recipient2@mail.mil")
    /// </summary>
    public List<string> To { get; }
    public List<string> Cc { get; }
    public List<string> Bcc { get; }
    public string Subject { get; }
    public string Body { get; }
    /// <summary>
    /// if not null, must be a list of absolute file paths to attach to the email
    /// </summary>
    public List<string> Attachments { get; }
    public EmailBodyType BodyType { get; }

    public EmailConfiguration(IList<object> args)
    {
        _log.Trace($"Building email configuration from timeline {JsonConvert.SerializeObject(args)}...");

        var settings = Program.Configuration.Email;
        var emailConfigArray = args;
        if (emailConfigArray.Count != 8)
        {
            throw new Exception(
                $"Incorrect number of email config array items - got {emailConfigArray.Count}, expected 8");
        }

        this.Id = Guid.NewGuid();
        this.To = new List<string>();
        this.Cc = new List<string>();
        this.Bcc = new List<string>();
        this.Attachments = new List<string>();

        this.From = emailConfigArray[0].ToString();

        // just use the first account we find already registered in outlook
        //if (this.From.Equals("CurrentUser", StringComparison.CurrentCultureIgnoreCase))
        //{
        //    this.From = $"{Environment.UserName}@{System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().DomainName}";
        //}

        this.To = ParseEmail(emailConfigArray[1].ToString(), settings.RecipientsToMin, settings.RecipientsToMax);
        this.Cc = ParseEmail(emailConfigArray[2].ToString(), settings.RecipientsCcMin, settings.RecipientsCcMax);
        this.Bcc = ParseEmail(emailConfigArray[3].ToString(), settings.RecipientsBccMin, settings.RecipientsBccMax);

        var emailContent = new EmailContentManager();

        this.Subject = emailConfigArray[4].ToString();

        if (this.Subject.Equals("random", StringComparison.InvariantCultureIgnoreCase))
        {
            this.Subject = emailContent.Subject;
        }

        this.Body = emailConfigArray[5].ToString();
        if (this.Body.Equals("random", StringComparison.InvariantCultureIgnoreCase))
        {
            this.Body = emailContent.Body;

            this.Body += GetFooter();
        }

        this.BodyType = EmailBodyType.PlainText;


        if (!string.IsNullOrEmpty(emailConfigArray[6].ToString()))
        {
            emailConfigArray[6] = emailConfigArray[6].ToString().Trim();
            if (emailConfigArray[6].ToString().Equals("HTML", StringComparison.InvariantCultureIgnoreCase))
                this.BodyType = EmailBodyType.HTML;
            else if (emailConfigArray[6].ToString().Equals("RTF", StringComparison.InvariantCultureIgnoreCase))
                this.BodyType = EmailBodyType.RTF;
        }

        if (!string.IsNullOrEmpty(emailConfigArray[7].ToString()))
        {
            var a = emailConfigArray[7].ToString().Split(Convert.ToChar(","));
            foreach (var o in a)
            {
                if (File.Exists(o))
                    this.Attachments.Add(o);
                else
                    _log.Debug($"Can't add attachment {o} - file was not found");
            }
        }

        _log.Trace($"Built email configuration from timeline {JsonConvert.SerializeObject(this)}...");
    }

    private string GetFooter()
    {
        if (!File.Exists(ApplicationDetails.ConfigurationFiles.EmailsFooter))
        {
            return $"{Environment.NewLine}{Environment.NewLine}CONFIDENTIALITY NOTICE: This e-mail message, including any attachments, may contain information that is protected by the DoD Privacy Act. This e-mail transmission is intended solely for the addressee(s). If you are not the intended recipient, you are hereby notified that you are not authorized to read, print, retain, copy, disclose, distribute, or use this message, any part of it, or any attachments. If you have received this message in error, please immediately notify the sender by telephone or return e-mail and delete this message and any attachments from your system without reading or saving in any manner. You can obtain additional information about the DoD Privacy Act at http://dpclo.defense.gov/privacy. Thank you.{Environment.NewLine}Timestamp: {DateTime.Now} ID: {this.Id}";
        }

        var email = "";
        var firstName = "";
        var lastName = "";
        var samAccountName = "";
        var name = "";
        try
        {
            var user = UserPrincipal.Current;
            email = user.EmailAddress;
            firstName = user.GivenName;
            lastName = user.Surname;
            samAccountName = user.SamAccountName;
            name = user.Name;
        }
        catch (Exception e)
        {
            _log.Trace($"Can't get current userprinciple for the email footer, skipping... {e}");
        }

        var f = File.ReadAllText(ApplicationDetails.ConfigurationFiles.EmailsFooter);
        f = f.Replace("{{from}}", this.From);
        f = f.Replace("{{now}}", DateTime.Now.ToLongDateString());
        f = f.Replace("{{id}}", this.Id.ToString());
        f = f.Replace("{{email}}", email);
        f = f.Replace("{{samaccountname}}", samAccountName);
        f = f.Replace("{{firstname}}", firstName);
        f = f.Replace("{{lastname}}", lastName);
        f = f.Replace("{{name}}", name);
        return f;
    }

    public override string ToString()
    {
        return $"Sending email from: {this.From} to: {string.Join(",", this.To)} cc: {string.Join(",", this.Cc)} bcc: {string.Join(",", this.Bcc)}";
    }

    private static List<string> ParseEmail(string raw, int min, int max)
    {
        _log.Trace($"Parsing email - raw {raw} min {min} max {max}");
        var list = new List<string>();
        if (string.IsNullOrEmpty(raw)) raw = "";

        var rnd = new Random();
        var numberOfRecipients = rnd.Next(min, max + 1);

        if (numberOfRecipients < 1)
        {
            _log.Trace("numberOfRecipients was less than 1, exiting...");
            return list;
        }

        if (raw.StartsWith("random", StringComparison.InvariantCultureIgnoreCase))
        {
            //add domain
            var emails = EmailListManager.GetDomainList();
            _log.Trace($"Building domain email list: {emails.Count}...");

            for (var i = 0; i < numberOfRecipients; i++)
                list.Add(emails.PickRandom());

            //add outside
            var numberOfOutsideRecipients = rnd.Next(Program.Configuration.Email.RecipientsOutsideMin, Program.Configuration.Email.RecipientsOutsideMax + 1);
            var outsideEmails = EmailListManager.GetOutsideList();
            _log.Trace($"Building outside email list: {outsideEmails.Count}...");
            for (var i = 0; i < numberOfOutsideRecipients; i++)
                list.Add(outsideEmails.PickRandom());
        }
        else
        {
            var a = raw.Split(Convert.ToChar(","));
            _log.Trace($"Building non-random list: {a.Length}...");
            list.AddRange(a.Where(IsValidEmail));
        }

        _log.Trace($"List generated: {list.Count}...");
        return list;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var address = new System.Net.Mail.MailAddress(email);
            return address.Address == email;
        }
        catch
        {
            _log.Trace($"Invalid email address: {email}...");
            return false;
        }
    }
}