// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using HtmlAgilityPack;

namespace ghosts.client.linux.handlers
{
    public class Curl : BaseHandler
    {
        private string Result { get; set; }
        private readonly TimelineHandler _handler;
        private readonly int _stickiness;
        private readonly int _depthMin = 1;
        private readonly int _depthMax = 10;
        private int _wait = 500;
        private string _currentHost;
        private readonly string _currentUserAgent;

        public Curl(TimelineHandler handler)
        {
            // setup
            _handler = handler;

            if (_handler.HandlerArgs.TryGetValue("stickiness", out var v1))
            {
                int.TryParse(v1.ToString(), out _stickiness);
            }
            if (_handler.HandlerArgs.TryGetValue("stickiness-depth-min", out var v2))
            {
                int.TryParse(v2.ToString(), out _depthMin);
            }
            if (_handler.HandlerArgs.TryGetValue("stickiness-depth-max", out var v3))
            {
                int.TryParse(v3.ToString(), out _depthMax);
            }

            _currentUserAgent = UserAgentManager.Get();

            _log.Trace($"Spawning Curl with stickiness {_stickiness}/{_depthMin}/{_depthMax}...");

            // run
            try
            {
                if (_handler.Loop)
                {
                    while (true)
                    {
                        Ex();
                    }
                }

                Ex();
            }
            catch (Exception e)
            {
                _log.Error(e);
            }
        }

        private void Ex()
        {
            foreach (var timelineEvent in _handler.TimeLineEvents)
            {
                WorkingHours.Is(_handler);

                if (timelineEvent.DelayBeforeActual > 0)
                    Thread.Sleep(timelineEvent.DelayBeforeActual);

                switch (timelineEvent.Command)
                {
                    default:
                        Command(timelineEvent.Command);

                        foreach (var cmd in timelineEvent.CommandArgs.Where(cmd => !string.IsNullOrEmpty(cmd.ToString())))
                        {
                            Command(cmd.ToString());
                        }

                        break;
                }

                if (timelineEvent.DelayAfterActual <= 0) continue;

                _wait = timelineEvent.DelayAfterActual;
                Thread.Sleep(timelineEvent.DelayAfterActual);
            }
        }

        private void Command(string command)
        {
            try
            {
                var escapedArgs = command; //.Replace("\"", "\\\"");

                try
                {
                    var uri = new Uri(escapedArgs);
                    _currentHost = $"{uri.Scheme}://{uri.Host}";
                }
                catch (Exception e)
                {
                    _log.Debug(e);
                }

                if (!escapedArgs.Contains("--user-agent ") && !escapedArgs.Contains("-A"))
                {
                    escapedArgs += $" -A \"{_currentUserAgent}\"";
                }

                Console.WriteLine($"curl {escapedArgs}");

                var p = new Process
                {
                    EnableRaisingEvents = false,
                    StartInfo =
                    {
                        FileName = "curl",
                        Arguments = escapedArgs,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                p.Start();

                while (!p.StandardOutput.EndOfStream)
                {
                    Result += p.StandardOutput.ReadToEnd();
                }

                Report(new ReportItem { Handler = HandlerType.Curl.ToString(), Command = escapedArgs, Result = Result });
                DeepBrowse();
            }
            catch (Exception exc)
            {
                _log.Debug(exc);
            }
        }

        /// <summary>
        /// continued browse on this site...
        /// </summary>
        private void DeepBrowse()
        {
            if (_stickiness <= 0) return;
            var random = new Random();
            if (random.Next(100) >= _stickiness) return;

            // some percentage of the time, we should stay on this site
            var loops = random.Next(_depthMin, _depthMax);
            for (var loopNumber = 0; loopNumber < loops; loopNumber++)
            {
                try
                {
                    //get all links from results
                    var doc = new HtmlDocument();
                    doc.LoadHtml(Result.ToLower());

                    var nodes = doc.DocumentNode.SelectNodes("//a");
                    if (nodes == null || nodes.Count == 0)
                    {
                        return;
                    }

                    var linkManager = new LinkManager(0);
                    foreach (var node in nodes)
                    {
                        if (!node.HasAttributes
                            || node.Attributes["href"] == null
                            || string.IsNullOrEmpty(node.Attributes["href"].Value)
                            || node.Attributes["href"].Value.StartsWith("//", StringComparison.CurrentCultureIgnoreCase))
                        {
                            //skip, these seem ugly
                        }
                        // http|s links
                        else if (node.Attributes["href"].Value.StartsWith("http", StringComparison.CurrentCultureIgnoreCase))
                        {
                            linkManager.AddLink(new Uri(node.Attributes["href"].Value.ToLower()), 1);
                        }
                        // relative links - prefix the scheme and host
                        else
                        {
                            linkManager.AddLink(new Uri($"{_currentHost}{node.Attributes["href"].Value.ToLower()}"), 2);
                        }
                    }

                    var link = linkManager.Choose();
                    if (link == null)
                    {
                        return;
                    }
                    var href = link.Url.ToString();

                    if (!string.IsNullOrEmpty(href))
                    {
                        Result = "";
                        Command(href);
                    }
                }
                catch (Exception e)
                {
                    _log.Error(e);
                }

                if (_wait > 0)
                {
                    Thread.Sleep(_wait);
                }
            }
        }
    }
}
