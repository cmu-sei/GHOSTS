// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using FileHelpers;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ghosts.Client.Infrastructure.Browser;

public class BlogContentManager
{
    public string Subject { private set; get; }
    public string Body { private set; get; }

    internal IList<BlogContent> Content { private set; get; }
    private static readonly Random _random = new Random();

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
        var total = this.Replies.Count;

        if (total <= 0) return null;
           
        BlogReply o = this.Replies[_random.Next(0, total)];
        return o.Reply.Replace("\\n", "\n");
    }

    public void BlogContentNext()
    {

        var total = this.Content.Count;

        if (total <= 0)
        {
            this.Subject = null;
            this.Body = null;
            return;
        };


        var o = this.Content[_random.Next(0, total)];
            

        this.Subject = o.Subject.Replace("\\n","\n");
        this.Body = o.Body.Replace("\\n", "\n");
    }

    public void LoadBlogFile()
    {
        try
        {
            var engine = new FileHelperEngine<BlogContent>();
            engine.Encoding = Encoding.UTF8;
            this.Content = engine.ReadFile(ClientConfigurationResolver.BlogContent).ToList();
        }
        catch (Exception e)
        {
            _log.Error($"Blog content file could not be loaded: {e}");
            this.Content = new List<BlogContent>();
        }
    }

    public void LoadReplyFile()
    {
        try
        {
            var engine = new FileHelperEngine<BlogReply>();
            engine.Encoding = Encoding.UTF8;
            this.Replies = engine.ReadFile(ClientConfigurationResolver.BlogReply).ToList();
        }
                
        catch (Exception e)
        {
            _log.Error($"Blog reply file could not be loaded: {e}");
            this.Replies = new List<BlogReply>();
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