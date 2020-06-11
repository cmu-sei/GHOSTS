// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;

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
        private readonly string _baseUrl;
        private readonly Random _random = new Random();

        public LinkManager(string baseUrl)
        {
            Links = new List<Link>();
            _baseUrl = baseUrl;
        }

        public List<Link> Links { private set; get; }

        /// <summary>
        ///     Adds proper links — invalid links get quickly discarded
        /// </summary>
        /// <param name="url">http|s://some.link/path/etc</param>
        public void AddLink(string url)
        {
            try
            {
                Links.Add(new Link {Url = new Uri(url)});
            }
            catch
            {
            }
        }

        public Link Choose()
        {
            var baseUri = new Uri(_baseUrl);
            foreach (var link in Links)
                try
                {
                    if (!link.Url.Host.Replace("www.", "").Contains(baseUri.Host.Replace("www.", "")))
                        link.Priority += 10;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"{link.Url} : {e}");
                }

            Links = Links.OrderByDescending(o => o.Priority).ToList();

            if (Links.Count < 1)
                return null;

            var totalWeight = Convert.ToInt32(Links.Sum(o => o.Priority));

            // totalWeight is the sum of all weights
            var r = _random.Next(0, totalWeight);

            foreach (var link in Links)
            {
                if (r < link.Priority) return link;

                r -= link.Priority;
            }

            return Links.PickRandom();
        }
    }
}