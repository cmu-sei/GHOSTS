// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Ghosts.Domain.Code
{
    public class Link
    {
        public Uri Url { get; set; }
        public int Priority { get; set; }
        
        public Link()
        {
            Priority = 0;
        }
    }

    public class LinkManager
    {
        public List<Link> Links { private set; get; }
        private readonly Random _random = new Random();
        private readonly string _baseUrl;

        public LinkManager(string baseUrl)
        {
            Links = new List<Link>();
            this._baseUrl = baseUrl;
        }

        /// <summary>
        /// Adds proper links — invalid links get quickly discarded
        /// </summary>
        /// <param name="url">http|s://some.link/path/etc</param>
        public void AddLink(string url)
        {
            try
            {
                this.Links.Add(new Link {Url = new Uri(url)});
            }
            catch { }
        }

        public Link Choose()
        {
            var baseUri = new Uri(this._baseUrl);
            foreach(var link in this.Links)
            {
                try
                {
                    if (!link.Url.Host.Replace("www.","").Contains(baseUri.Host.Replace("www.","")))
                        link.Priority += 10;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"{link.Url} : {e}");
                }
            }
            
            this.Links = this.Links.OrderByDescending(o=>o.Priority).ToList();

            if (this.Links.Count < 1)
                return null;
            
            var totalWeight = Convert.ToInt32(this.Links.Sum(o => o.Priority));
            
            // totalWeight is the sum of all weights
            var r = _random.Next(0, totalWeight);

            foreach (var link in this.Links)
            {
                if (r < link.Priority)
                {
                    return link;
                }

                r -= link.Priority;
            }

            return Links.PickRandom();
        }
    }
}