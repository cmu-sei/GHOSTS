// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Ghosts.Domain.Code
{
    public class Link
    {
        public Link()
        {
            Priority = 0;
        }

        public Uri Url { get; set; }
        public int Priority { get; set; }
    }

    public class LinkManager
    {
        public List<Link> Links { private set; get; }
        
        private readonly string _baseUrl;
        private readonly Random _random = new Random();

        public LinkManager(string baseUrl)
        {
            Links = new List<Link>();
            _baseUrl = baseUrl;
        }
        
        public void AddLink(string url, int priority)
        {
            try
            {
                Links.Add(new Link {Url = new Uri(url), Priority = priority});
            }
            catch(Exception e)
            {
                Console.WriteLine($"{url} {e}");
            }
        }

        public Link Choose()
        {
            var pickList = new List<Link>();
            var baseUri = new Uri(_baseUrl);
            var schemesToIgnore = new [] {"mailto", "skype", "tel"};
            
            foreach (var link in Links)
                try
                {
                    if(schemesToIgnore.Any(s => s.StartsWith(link.Url.ToString())))
                        continue;
                    
                    // give relative links priority
                    if((link.Url.Scheme + link.Url.Host).Replace("www.", "").Equals((baseUri.Scheme + baseUri.Host).Replace("www.", ""), StringComparison.InvariantCultureIgnoreCase))
                        link.Priority += 1;
                    else if(link.Url.Scheme.Equals("file", StringComparison.InvariantCultureIgnoreCase))
                        link.Priority += 1;
                    
                    pickList.Add(link);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"{link.Url} : {e}");
                }

            Links = pickList.OrderByDescending(o => o.Priority).ToList();

            if (Links.Count < 1)
                return null;

            var priority = Links.First().Priority;
            var chosen = Links.Where(x => x.Priority == priority).PickRandom();

            if (chosen.Url.Scheme.ToLower().StartsWith("file"))
            {
                try
                {
                    var bUrl = baseUri.ToString();
                    if (bUrl.EndsWith("/"))
                        bUrl = bUrl.Substring(0, bUrl.Length - 1);
                
                    var thisUrl = chosen.Url.ToString().Replace("file://","");
                
                    thisUrl = Regex.Replace(thisUrl, "////", "//");
                    if (thisUrl.StartsWith("/"))
                        thisUrl = thisUrl.Substring(1, thisUrl.Length - 1);
                
                    chosen.Url = new Uri($"{bUrl}/{thisUrl}");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"{chosen.Url} : {e}");
                }
            }

            return chosen;
        }
   }
}