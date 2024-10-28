// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.IO.Compression;
using Ghosts.Client.Lite.Infrastructure.Services;
using Ghosts.Domain;
using Ghosts.Domain.Code.Helpers;
using HtmlAgilityPack;
using Newtonsoft.Json;
using NLog;
using Quartz;

namespace Ghosts.Client.Lite.Infrastructure.Handlers
{
    public class WebBrowsingJob : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            var raw = context.MergedJobDataMap["handler"].ToString();
            if (string.IsNullOrEmpty(raw))
                return;
            var handler = JsonConvert.DeserializeObject<TimelineHandler>(raw);
            if (handler == null)
                return;
            var httpHandler = new HttpHandler();

            await httpHandler.Run(handler);
        }
    }

    public class HttpHandler
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public async Task Run(TimelineHandler handler)
        {
            var rand = new Random();
            foreach (var timelineEvent in handler.TimeLineEvents)
            {
                var list = new List<object>();
                for (var i = 0; i < rand.Next(1, 10); i++)
                {
                    list.Add(timelineEvent.CommandArgs.PickRandom());
                }

                foreach (var site in list)
                {
                    await Run(handler.HandlerType, timelineEvent, site.ToString());
                }
            }
        }

        public async Task Run(HandlerType handler, TimelineEvent t, string? url)
        {
            if (string.IsNullOrEmpty(url))
            {
                _log.Trace("No url provided, returning...");
                return;
            }

            try
            {
                var client = CreateHttpClient(handler, url);
                var statusCode = 200;
                var htmlContent = string.Empty;
                try
                {
                    var response = await client.GetAsync(client.BaseAddress);
                    htmlContent = await ReadContentAsync(response.Content);
                    _log.Debug($"Http request to {client.BaseAddress} is {response.StatusCode}");
                }
                catch (HttpRequestException e)
                {
                    _log.Debug($"Http request to {client.BaseAddress} failed with {e}");
                    statusCode = e.StatusCode.HasValue ? (int)e.StatusCode.Value : -8;
                }
                catch (Exception ex)
                {
                    _log.Debug($"Http request to {client.BaseAddress} failed with {ex}");
                    statusCode = -9;
                }

                LogWriter.Timeline(new TimeLineRecord
                {
                    Command = t.Command,
                    CommandArg = url,
                    Handler = handler.ToString(),
                    Result = statusCode.ToString()
                });

                if (statusCode != 200)
                    return;

                var htmlDocument = new HtmlDocument();
                htmlDocument.LoadHtml(htmlContent);

                await ProcessLinks(htmlDocument, handler, url, "css", "//head/link", "href");
                await ProcessLinks(htmlDocument, handler, url, "js", "//script", "src");
                await ProcessLinks(htmlDocument, handler, url, "img", "//img", "src");
            }
            catch (Exception e)
            {
                _log.Error(e);
            }
        }

        private static HttpClient CreateHttpClient(HandlerType handler, string url)
        {
            var clientHandler = new HttpClientHandler
            {
                UseCookies = true,
                AllowAutoRedirect = true
            };
            var client = new HttpClient(clientHandler)
            {
                BaseAddress = new Uri(url)
            };

            client.DefaultRequestHeaders.Clear();

            switch (handler)
            {
                case HandlerType.BrowserChrome:
                    client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9");
                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 KHTML Chrome/97.0.4692.99 Safari/537.36");
                    client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
                    client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
                    client.DefaultRequestHeaders.Add("Connection", "keep-alive");
                    break;
                case HandlerType.BrowserFirefox:
                    client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:96.0) Gecko/20100101 Firefox/96.0");
                    client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
                    client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
                    client.DefaultRequestHeaders.Add("Connection", "keep-alive");
                    break;
            }

            return client;
        }

        private static async Task<string> ReadContentAsync(HttpContent content)
        {
            var encoding = content.Headers.ContentEncoding;
            var contentStream = await content.ReadAsStreamAsync();

            if (encoding.Contains("gzip"))
            {
                await using var decompressedStream = new GZipStream(contentStream, CompressionMode.Decompress);
                using var reader = new StreamReader(decompressedStream);
                return await reader.ReadToEndAsync();
            }
            else if (encoding.Contains("deflate"))
            {
                await using var decompressedStream = new DeflateStream(contentStream, CompressionMode.Decompress);
                using var reader = new StreamReader(decompressedStream);
                return await reader.ReadToEndAsync();
            }
            else
            {
                using var reader = new StreamReader(contentStream);
                return await reader.ReadToEndAsync();
            }
        }

        private async Task ProcessLinks(HtmlDocument document, HandlerType handler, string baseUrl, string logPrefix, string xpath, string attribute)
        {
            var nodes = document.DocumentNode.SelectNodes(xpath);
            if (nodes == null)
                return;

            var links = nodes.Select(node => node.GetAttributeValue(attribute, ""))
                             .Where(href => !string.IsNullOrEmpty(href))
                             .ToList();

            foreach (var link in links)
            {
                if (!baseUrl.StartsWith("http", StringComparison.InvariantCultureIgnoreCase))
                    continue;

                var client = CreateHttpClient(handler, baseUrl);
                var response = await client.GetAsync(link);
                _log.Trace($"Request to {client.BaseAddress}{logPrefix} : {response.StatusCode}");
            }
        }
    }
}
