// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Linq;
using System.Threading.Tasks;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Ghosts.Domain.Code.Helpers;
using OpenQA.Selenium;

namespace Ghosts.Client.Handlers;

class BrowserCrawl : BaseHandler
{
    public IWebDriver Driver { get; set; }
    public IJavaScriptExecutor JS { get; set; }
    private int _stickiness;
    private LinkManager _linkManager;
    private int _pageBrowseCount;
    private string _proxyLocalUrl = string.Empty;
    private int _siteDepthMax = 1;
    private int _siteDepthCurrent;

    internal Task Crawl(TimelineHandler handler, TimelineEvent timelineEvent, string site)
    {
        switch (handler.HandlerType)
        {
            case HandlerType.BrowserChrome:
                this.Driver = BrowserChrome.GetDriver(handler);
                break;
            case HandlerType.BrowserFirefox:
                this.Driver = BrowserFirefox.GetDriver(handler);
                break;
        }

        Console.WriteLine($"{Environment.CurrentManagedThreadId} handle: {Driver.CurrentWindowHandle}");

        if (handler.HandlerArgs.ContainsKey("stickiness"))
        {
            int.TryParse(handler.HandlerArgs["stickiness"].ToString(), out _stickiness);
        }

        if (handler.HandlerArgs.ContainsKey("crawl-site-depth"))
        {
            int.TryParse(handler.HandlerArgs["crawl-site-depth"].ToString(), out _siteDepthMax);
        }

        if (handler.HandlerArgs.ContainsKey("crawl-proxy-local-url"))
        {
            _proxyLocalUrl = handler.HandlerArgs["crawl-proxy-local-url"].ToString();
        }

        this._pageBrowseCount = 0;
        var config = RequestConfiguration.Load(handler, site);
        this._linkManager = new LinkManager(0);
        if (config.Uri.IsWellFormedOriginalString())
        {
            MakeRequest(config);
            Report(new ReportItem { Handler = handler.HandlerType.ToString(), Command = timelineEvent.Command, Arg = config.ToString(), Trackable = timelineEvent.TrackableId });
            this._siteDepthCurrent += 1;

            if (this._siteDepthCurrent >= this._siteDepthMax)
                return Task.CompletedTask;

            GetAllLinks(config, true);
            CrawlAllLinks(config, handler, timelineEvent, true);
        }

        Driver.Close();
        Driver.Quit();
        Log.Trace($"Run complete for {site}");
        return Task.CompletedTask;
    }

    private void GetAllLinks(RequestConfiguration config, bool sameSite)
    {
        Log.Trace($"Getting links for {config.Uri}...");
        var linksAdded = 0;
        if (this._pageBrowseCount > this._stickiness)
        {
            Log.Trace($"Exceeded stickiness for {config.Uri} {this._stickiness}...");
            return;
        }

        try
        {
            var isInIframe = false;
            // for use with pywb and proxy scraping
            var iframes = Driver.FindElements(By.TagName("iframe"));
            foreach (var iframe in iframes)
            {
                if (iframe.GetAttribute("id") == "replay_iframe")
                {
                    Driver.SwitchTo().Frame(iframe);
                    isInIframe = true;
                }
            }

            var links = Driver.FindElements(By.TagName("a"));

            foreach (var l in links)
            {
                var node = l.GetAttribute("href");
                if (string.IsNullOrEmpty(node))
                    continue;
                node = node.ToLower();
                if (isInIframe && !string.IsNullOrEmpty(this._proxyLocalUrl))
                    node = this._proxyLocalUrl + node;
                if (Uri.TryCreate(node, UriKind.RelativeOrAbsolute, out var uri))
                {
                    if (uri.GetDomain() != config.Uri.GetDomain())
                    {
                        if (!sameSite)
                        {
                            this._linkManager.AddLink(uri, 1);
                            linksAdded += 1;
                        }
                    }
                    // relative links - prefix the scheme and host 
                    else
                    {
                        this._linkManager.AddLink(uri, 2);
                        linksAdded += 1;
                    }
                }
            }

            if (isInIframe)
                Driver.SwitchTo().DefaultContent();

            Log.Trace($"Added {linksAdded} links for {config.Uri}");
        }
        catch (Exception e)
        {
            Log.Trace(e);
        }
    }

    private void CrawlAllLinks(RequestConfiguration config, TimelineHandler handler,
        TimelineEvent timelineEvent, bool sameSite)
    {
        Log.Trace($"Crawling links for {config.Uri}");
        if (this._linkManager?.Links == null)
        {
            return;
        }
        if (this._pageBrowseCount > this._stickiness)
        {
            return;
        }

        foreach (var link in this._linkManager.Links.Where(x => x.WasBrowsed == false)
                     .OrderByDescending(x => x.Priority))
        {
            if (this._pageBrowseCount > this._stickiness)
            {
                Log.Trace($"Exceeded stickiness for {config.Uri} {this._stickiness} (2)...");
                return;
            }

            if (this._linkManager.Links.Any(x => x.Url.ToString() == link.Url.ToString() && x.WasBrowsed))
            {
                continue;
            }

            config.Method = "GET";
            config.Uri = link.Url;

            MakeRequest(config);

            foreach (var l in this._linkManager.Links.Where(x => x.Url.ToString() == link.Url.ToString()))
            {
                l.WasBrowsed = true;
                Log.Trace($"Skipping {config.Uri} (already browsed)");
            }
            this._pageBrowseCount += 1;
            if (this._pageBrowseCount > this._stickiness)
            {
                Log.Trace($"Exceeded stickiness for {config.Uri} {this._stickiness} (3)...");
                return;
            }

            Report(new ReportItem { Handler = handler.HandlerType.ToString(), Command = timelineEvent.Command, Arg = config.ToString(), Trackable = timelineEvent.TrackableId });
            
            // if this is the last step down, there is no reason to keep digging,
            // but we don't increase the current depth count so as to allow peer
            // pages at this level to still be scraped
            if (this._siteDepthCurrent + 1 < this._siteDepthMax)
            {
                Log.Trace($"Drilling into {config.Uri}...");
                GetAllLinks(config, sameSite);
                CrawlAllLinks(config, handler, timelineEvent, sameSite);
            }
        }
    }

    private void MakeRequest(RequestConfiguration config)
    {
        try
        {
            Driver.Navigate().GoToUrl(config.Uri);
        }
        catch (Exception e)
        {
            Log.Trace($"Requst error for {config.Uri}: {e.Message}");
        }
    }
}