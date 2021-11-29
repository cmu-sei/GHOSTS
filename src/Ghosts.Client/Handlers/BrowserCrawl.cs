using System;
using System.Linq;
using System.Threading;
using Ghosts.Client.Infrastructure.Browser;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Ghosts.Domain.Code.Helpers;
using NLog;
using OpenQA.Selenium;

namespace Ghosts.Client.Handlers
{
    class BrowserCrawl : BaseHandler
    {
        public static readonly Logger _log = LogManager.GetCurrentClassLogger();
        public IWebDriver Driver { get; set; }
        public IJavaScriptExecutor JS { get; set; }
        private int _stickiness = 0;
        private LinkManager _linkManager;
        private int _pageBrowseCount = 0;

        internal BrowserCrawl(TimelineHandler handler, TimelineEvent timelineEvent, string site)
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

            if (handler.HandlerArgs.ContainsKey("stickiness"))
            {
                int.TryParse(handler.HandlerArgs["stickiness"], out _stickiness);
            }

            this._pageBrowseCount = 0;
            var config = RequestConfiguration.Load(site);
            this._linkManager = new LinkManager(config.Uri);
            if (config.Uri.IsWellFormedOriginalString())
            {
                MakeRequest(config);
                Report(handler.HandlerType.ToString(), timelineEvent.Command, config.ToString(),
                    timelineEvent.TrackableId);

                GetAllLinks(config, true);
                CrawlAllLinks(config, handler, timelineEvent, true);
            }
        }

        private void GetAllLinks(RequestConfiguration config, bool sameSite)
        {
            if (this._pageBrowseCount > this._stickiness)
            {
                return;
            }

            try
            {
                var links = Driver.FindElements(By.TagName("a"));
                foreach (var l in links)
                {
                    var node = l.GetAttribute("href");
                    if (string.IsNullOrEmpty(node))
                        continue;
                    node = node.ToLower();
                    if (Uri.TryCreate(node, UriKind.RelativeOrAbsolute, out var uri))
                    {
                        if (uri.GetDomain() != config.Uri.GetDomain())
                        {
                            if (!sameSite)
                                this._linkManager.AddLink(uri, 1);
                        }
                        // relative links - prefix the scheme and host 
                        else
                        {
                            this._linkManager.AddLink(uri, 2);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                _log.Trace(e);
            }
        }
        
        private void CrawlAllLinks(RequestConfiguration config, TimelineHandler handler,
            TimelineEvent timelineEvent, bool sameSite)
        {
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
                    return;
                }
                if (this._linkManager.Links.Any(x => x.Url.ToString() == link.Url.ToString() && x.WasBrowsed))
                    continue;

                config.Method = "GET";
                config.Uri = link.Url;

                MakeRequest(config);

                foreach (var l in this._linkManager.Links.Where(x => x.Url.ToString() == link.Url.ToString()))
                    l.WasBrowsed = true;
                this._pageBrowseCount += 1;
                if (this._pageBrowseCount > this._stickiness)
                {
                    return;
                }

                Report(handler.HandlerType.ToString(), timelineEvent.Command, config.ToString(),
                    timelineEvent.TrackableId);
                GetAllLinks(config, sameSite);
                CrawlAllLinks(config, handler, timelineEvent, sameSite);
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
                _log.Trace(e.Message);
            }
        }
    }
}
