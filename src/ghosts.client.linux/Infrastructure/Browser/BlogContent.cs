// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FileHelpers;
using NLog;

namespace ghosts.client.linux.Infrastructure.Browser;

public class BlogContentManager
{
    public string Subject { private set; get; }
    public string Body { private set; get; }

    internal IList<BlogContent> Content { private set; get; }
    private static readonly Random _random = new();

    internal IList<BlogReply> Replies { private set; get; }

    private static readonly Logger _log = LogManager.GetCurrentClassLogger();

    public static void Check()
    {
        var blogContentManager = new BlogContentManager();
        blogContentManager.LoadBlogFile();
        if (blogContentManager.Content.Count < 1)
        {
            const string msg = "Blog content could not be loaded. Blog content will not be posted";
            _log.Error(msg);
            Console.WriteLine(msg);
        }
        else
        {
            var msg = $"Blog content loaded successfully with {blogContentManager.Content.Count} records found";
            _log.Info(msg);
            Console.WriteLine(msg);
        }
    }

    public BlogContentManager()
    {
        LoadBlogFile();
        LoadReplyFile();
    }
    public string BlogReplyNext()
    {
        var total = Replies.Count;

        if (total <= 0) return null;

        var o = Replies[_random.Next(0, total)];
        return o.Reply.Replace("\\n", "\n");
    }

    public void BlogContentNext()
    {

        var total = Content.Count;

        if (total <= 0)
        {
            Subject = null;
            Body = null;
            return;
        };


        var o = Content[_random.Next(0, total)];


        Subject = o.Subject.Replace("\\n", "\n");
        Body = o.Body.Replace("\\n", "\n");
    }

    public void LoadBlogFile()
    {
        try
        {
            var engine = new FileHelperEngine<BlogContent>
            {
                Encoding = Encoding.UTF8
            };
            Content = engine.ReadFile(ClientConfigurationResolver.BlogContent).ToList();
        }
        catch (Exception e)
        {
            _log.Error($"Blog content file could not be loaded: {e}");
            Content = new List<BlogContent>();
        }
    }

    public void LoadReplyFile()
    {
        try
        {
            var engine = new FileHelperEngine<BlogReply>
            {
                Encoding = Encoding.UTF8
            };
            Replies = engine.ReadFile(ClientConfigurationResolver.BlogReply).ToList();
        }

        catch (Exception e)
        {
            _log.Error($"Blog reply file could not be loaded: {e}");
            Replies = new List<BlogReply>();
        }
    }
}

[DelimitedRecord("|")]
[IgnoreEmptyLines()]
internal class BlogContent
{
    public string Id { get; set; }
    public string Subject { get; set; }
    public string Body { get; set; }
}

[DelimitedRecord("|")]
[IgnoreEmptyLines()]
internal class BlogReply
{
    public string Reply { get; set; }
}
