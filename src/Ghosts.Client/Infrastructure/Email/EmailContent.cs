// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FileHelpers;
using Ghosts.Domain.Code;
using Ghosts.Domain.Code.Helpers;
using NLog;

namespace Ghosts.Client.Infrastructure.Email;

public class EmailContentManager
{
    public string Subject { private set; get; }
    public string Body { private set; get; }
    public ClientConfiguration Configuration { private set; get; }
    private static readonly Random _random = new Random();

    internal IList<EmailContent> Content { private set; get; }

    private static readonly Logger _log = LogManager.GetCurrentClassLogger();

    public static void Check()
    {
        var emailContentManager = new EmailContentManager();
        emailContentManager.LoadEmailFile();
        if (emailContentManager.Content.Count < 1)
        {
            const string msg = "Email content could not be loaded. Emails will not be sent";
            _log.Error(msg);
            Console.WriteLine(msg);
        }
        else
        {
            var msg = $"Email content loaded successfully with {emailContentManager.Content.Count} records found";
            _log.Info(msg);
            Console.WriteLine(msg);
        }
    }

    public EmailContentManager()
    {
        LoadEmailFile();

        var total = this.Content.Count;

        if (total <= 0) return;

        var o = this.Content[_random.Next(0, total)];
        this.Configuration = Program.Configuration;

        this.Subject = ReplaceTokens(o.Subject);
        this.Body = Parse(o.Body);
    }

    public void LoadEmailFile()
    {
        try
        {
            var engine = new FileHelperEngine<EmailContent>();
            this.Content = engine.ReadFile(ClientConfigurationResolver.EmailContent).ToList();
        }
        catch (Exception e)
        {
            _log.Error($"email content file could not be loaded: {e}");
            this.Content = new List<EmailContent>();
        }
    }

    private string ReplaceTokens(string s)
    {
        var tokens = Configuration.EmailContent;
        foreach (var token in tokens)
        {
            var t = $"<{token.Key}/>";
            s = s.Replace(t, token.Value);
        }
        return s;
    }

    private string Parse(string x)
    {
        //foreach (var item in this.Configuration.EmailContent)
        //    x = x.ReplaceCaseInsensitive($"<{item.Key}/>", item.Value);

        var line = 0;
        var s = new StringBuilder();
        foreach (var word in x.Split(Convert.ToChar(" ")))
        {
            if (line > 0)
            {
                var splits = new string[] { "from:", "Original", "UNCLASSIFIED" };
                foreach (var split in splits)
                {
                    if (word.StartsWith(split, StringComparison.InvariantCultureIgnoreCase))
                    {
                        s.AppendLine(" ");
                        s.AppendLine(" ");
                    }
                }

                splits = new string[] { "subject:", "to:", "date:", "sent:", "timestamp:", "subject", "Timestamp:", "ID:" };
                foreach (var split in splits)
                {
                    if (word.StartsWith(split, StringComparison.InvariantCultureIgnoreCase))
                        s.AppendLine(" ");
                }
            }

            s.Append(word).Append(" ");
            line++;
        }

        var tokens = Configuration.EmailContent;
        foreach (KeyValuePair<string, string> token in tokens)
        {
            var t = $"<{token.Key}/>";
            s.Replace(t, token.Value);
        }

        var o = s.ToString().Replace("\\n", Environment.NewLine).Trim('"').Trim(' ').Trim('"');
        o = o.RemoveFirstLines(3);

        if (o.StartsWith("Subject:", StringComparison.InvariantCultureIgnoreCase))
            o = o.Remove(0, 8).Trim();
        if (o.StartsWith("Subject", StringComparison.InvariantCultureIgnoreCase))
            o = o.Remove(0, 7).Trim();
        if (o.StartsWith("RE:", StringComparison.InvariantCultureIgnoreCase))
            o = o.Remove(0, 3).Trim();
        if (o.StartsWith("fw:", StringComparison.InvariantCultureIgnoreCase))
            o = o.Remove(0, 3).Trim();
        if (o.StartsWith("fwD:", StringComparison.InvariantCultureIgnoreCase))
            o = o.Remove(0, 4).Trim();
        return o.Trim();
    }
}

public class EmailReplyManager
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    public string Reply { private set; get; }
    private static readonly Random _random = new Random();

    public EmailReplyManager()
    {
        try
        {
            var engine = new FileHelperEngine<EmailReply>();

            //does file exist
            if(!File.Exists(ClientConfigurationResolver.EmailReply))
                throw new FileNotFoundException($"Email reply file not found at {ClientConfigurationResolver.EmailReply}");

            // To Read Use:
            var list = engine.ReadFile(ClientConfigurationResolver.EmailReply);
            var total = list.Count();

            var o = list[_random.Next(0, total)];
            this.Reply = o.Reply;
        }
        catch (Exception exc)
        {
            _log.Debug(exc);
            this.Reply = string.Empty;
        }
    }
}

[DelimitedRecord("|")]
internal class EmailContent
{
    public string Id { get; set; }
    public string Subject { get; set; }
    public string Body { get; set; }
}

[DelimitedRecord("|")]
internal class EmailReply
{
    public string Reply { get; set; }
}