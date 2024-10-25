// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Ghosts.Domain.Code.Helpers;
using NLog;

namespace Ghosts.Domain.Code
{
    public class Link
    {
        public Link()
        {
            Priority = 0;
        }

        public Uri Url { get; set; }
        /// <summary>
        /// Higher priority is more important
        /// </summary>
        public int Priority { get; set; }

        public bool WasBrowsed { get; set; }
    }

    public class LinkManager
    {
        public static readonly Logger Log = LogManager.GetCurrentClassLogger();
        public LifoQueue<Uri> RecentlyVisited { private set; get; }
        public List<Link> Links { private set; get; }

        private Uri _baseUri;

        private readonly IEnumerable<string> _denyList;

        public LinkManager(int visitedSitesRemembered)
        {
            Links = new List<Link>();
            RecentlyVisited = new LifoQueue<Uri>(visitedSitesRemembered);
            _denyList = DenyListManager.LoadDenyList();
            Log.Trace($"Creating new link manager with visitedSitesRemembered = {visitedSitesRemembered}");
        }

        public void SetCurrent(Uri baseUri)
        {
            _baseUri = baseUri;
            Links = new List<Link>();
            Log.Trace($"Link manager reset with baseuri = {_baseUri}");
        }

        //public void AddLink(string url, int priority)
        //{
        //    if (!Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var uri))
        //    {
        //        return;
        //    }
        //    this.AddLink(uri, priority);
        //}

        public void AddLink(Uri uri, int priority)
        {
            string[] validSchemes = { "http", "https" };
            if (!validSchemes.Contains(uri.Scheme))
            {
                return;
            }

            foreach (var link in Links)
            {
                if (Uri.Compare(uri, link.Url, UriComponents.Host | UriComponents.PathAndQuery, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return;
                }
            }

            // is in deny list?
            if (DenyListManager.IsInDenyList(_denyList, uri))
                return;


            // truly a new link, add it
            try
            {
                Links.Add(new Link { Url = uri, Priority = priority });
            }
            catch (Exception e)
            {
                Log.Trace($"{uri} {e}");
            }
        }

        public Link Choose()
        {
            try
            {
                var pickList = new List<Link>();

                if (Links != null && Links.Any())
                {
                    foreach (var link in Links)
                    {
                        try
                        {
                            // give relative links priority
                            if ((link.Url.Scheme + link.Url.Host).Replace("www.", "").Equals(
                                    (_baseUri.Scheme + _baseUri.Host).Replace("www.", ""),
                                    StringComparison.InvariantCultureIgnoreCase))
                            {
                                link.Priority += 1;
                            }
                            else if (link.Url.Scheme.Equals("file", StringComparison.InvariantCultureIgnoreCase))
                            {
                                link.Priority += 1;
                            }

                            pickList.Add(link);
                        }
                        catch (Exception e)
                        {
                            Log.Trace($"{link.Url} : {e}");
                        }
                    }
                }

                if (!pickList.Any())
                {
                    return null;
                }

                Links = pickList.OrderByDescending(o => o.Priority).ToList();

                if (!Links.Any())
                {
                    return null;
                }

                foreach (var visited in RecentlyVisited)
                {
                    var itemToRemove = Links.SingleOrDefault(x => x.Url == visited);
                    if (itemToRemove != null)
                        Links.Remove(itemToRemove);
                }

                if (!Links.Any())
                {
                    return null;
                }

                var priority = Links.First().Priority;
                var chosenList = Links.Where(x => x.Priority == priority).ToArray();

                if (!chosenList.Any())
                {
                    return null;
                }

                var chosen = chosenList.PickRandom();

                if (chosen.Url.Scheme.ToLower().StartsWith("file"))
                {
                    try
                    {
                        var bUrl = _baseUri.ToString();
                        if (bUrl.EndsWith("/"))
                        {
                            bUrl = bUrl.Substring(0, bUrl.Length - 1);
                        }

                        var thisUrl = chosen.Url.ToString().Replace("file://", "");

                        thisUrl = Regex.Replace(thisUrl, "////", "//");
                        if (thisUrl.StartsWith("/"))
                        {
                            thisUrl = thisUrl.Substring(1, thisUrl.Length - 1);
                        }

                        chosen.Url = new Uri($"{bUrl}/{thisUrl}");
                    }
                    catch (Exception e)
                    {
                        Log.Trace($"{chosen.Url} : {e}");
                    }
                }

                RecentlyVisited.Add(chosen.Url);
                return chosen;
            }
            catch (Exception e)
            {
                Log.Trace(e);
                throw;
            }
        }
    }
}
