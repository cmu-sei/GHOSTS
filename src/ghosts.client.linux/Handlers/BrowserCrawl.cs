// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Linq;
using System.Threading.Tasks;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Ghosts.Domain.Code.Helpers;
using OpenQA.Selenium;

namespace ghosts.client.linux.handlers
{
    class BrowserCrawl : BaseHandler
    {
        public IWebDriver Driver { get; set; }
        public IJavaScriptExecutor JS { get; set; }
        private int _stickiness = 0;
        private LinkManager _linkManager;
        private int _pageBrowseCount = 0;
        private string _proxyLocalUrl = string.Empty;
        private int _siteDepthMax = 1;
        private int _siteDepthCurrent = 0;

        internal Task Crawl(TimelineHandler handler, TimelineEvent timelineEvent, string site)
        {
            switch (handler.HandlerType)
            {
                case HandlerType.BrowserChrome:
                    Driver = BrowserChrome.GetDriver(handler);
                    break;
                case HandlerType.BrowserFirefox:
                    Driver = BrowserFirefox.GetDriver(handler);
                    break;
            }

            Console.WriteLine($"{Environment.CurrentManagedThreadId} handle: {Driver.CurrentWindowHandle}");

            if (handler.HandlerArgs.TryGetValue("stickiness", out var v1))
            {
                int.TryParse(v1.ToString(), out _stickiness);
            }

            if (handler.HandlerArgs.TryGetValue("crawl-site-depth", out var v2))
            {
                int.TryParse(v2.ToString(), out _siteDepthMax);
            }

            if (handler.HandlerArgs.TryGetValue("crawl-proxy-local-url", out var v3))
            {
                _proxyLocalUrl = v3.ToString();
            }

            _pageBrowseCount = 0;
            var config = RequestConfiguration.Load(handler, site);
            _linkManager = new LinkManager(0);
            if (config.Uri.IsWellFormedOriginalString())
            {
                MakeRequest(config);
                Report(new ReportItem { Handler = handler.HandlerType.ToString(), Command = timelineEvent.Command, Arg = config.ToString(), Trackable = timelineEvent.TrackableId });
                _siteDepthCurrent += 1;

                if (_siteDepthCurrent >= _siteDepthMax)
                    return Task.CompletedTask;

                GetAllLinks(config, true);
                CrawlAllLinks(config, handler, timelineEvent, true);
            }

            Driver.Close();
            Driver.Quit();
            _log.Trace($"Run complete for {site}");
            return Task.CompletedTask;
        }

        private void GetAllLinks(RequestConfiguration config, bool sameSite)
        {
            _log.Trace($"Getting links for {config.Uri}...");
            var linksAdded = 0;
            if (_pageBrowseCount > _stickiness)
            {
                _log.Trace($"Exceeded stickiness for {config.Uri} {_stickiness}...");
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
                        _log.Trace("replay_iframe found. Made that the focus...");
                    }
                    _log.Trace($"Iframe found: {iframe.GetAttribute("id")}");
                }

                var links = Driver.FindElements(By.TagName("a"));

                foreach (var l in links)
                {
                    var node = l.GetAttribute("href");
                    if (string.IsNullOrEmpty(node))
                        continue;
                    node = node.ToLower();
                    if (isInIframe && !string.IsNullOrEmpty(_proxyLocalUrl))
                        node = _proxyLocalUrl + node;
                    if (Uri.TryCreate(node, UriKind.RelativeOrAbsolute, out var uri))
                    {
                        if (uri.GetDomain() != config.Uri.GetDomain())
                        {
                            if (!sameSite)
                            {
                                _linkManager.AddLink(uri, 1);
                                linksAdded += 1;
                            }
                        }
                        // relative links - prefix the scheme and host
                        else
                        {
                            _linkManager.AddLink(uri, 2);
                            linksAdded += 1;
                        }
                    }
                }

                if (isInIframe)
                {
                    Driver.SwitchTo().DefaultContent();
                    _log.Trace("Switched back to main window focus");
                }

                _log.Trace($"Added {linksAdded} links for {config.Uri}");
            }
            catch (Exception e)
            {
                _log.Trace(e);
            }
        }

        private void CrawlAllLinks(RequestConfiguration config, TimelineHandler handler,
            TimelineEvent timelineEvent, bool sameSite)
        {
            _log.Trace($"Crawling links for {config.Uri}");
            if (_linkManager?.Links == null)
            {
                return;
            }
            if (_pageBrowseCount > _stickiness)
            {
                return;
            }

            foreach (var link in _linkManager.Links.Where(x => x.WasBrowsed == false)
                .OrderByDescending(x => x.Priority))
            {
                if (_pageBrowseCount > _stickiness)
                {
                    _log.Trace($"Exceeded stickiness for {config.Uri} {_stickiness} (2)...");
                    return;
                }

                if (_linkManager.Links.Any(x => x.Url.ToString() == link.Url.ToString() && x.WasBrowsed))
                {
                    continue;
                }

                config.Method = "GET";
                config.Uri = link.Url;

                MakeRequest(config);

                foreach (var l in _linkManager.Links.Where(x => x.Url.ToString() == link.Url.ToString()))
                {
                    l.WasBrowsed = true;
                    _log.Trace($"Skipping {config.Uri} (already browsed)");
                }
                _pageBrowseCount += 1;
                if (_pageBrowseCount > _stickiness)
                {
                    _log.Trace($"Exceeded stickiness for {config.Uri} {_stickiness} (3)...");
                    return;
                }

                Report(new ReportItem { Handler = handler.HandlerType.ToString(), Command = timelineEvent.Command, Arg = config.ToString(), Trackable = timelineEvent.TrackableId });

                // if this is the last step down, there is no reason to keep digging,
                // but we don't increase the current depth count so as to allow peer
                // pages at this level to still be scraped
                if (_siteDepthCurrent + 1 < _siteDepthMax)
                {
                    _log.Trace($"Drilling into {config.Uri}...");
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
                _log.Trace($"Requst error for {config.Uri}: {e.Message}");
            }
        }
    }
}
