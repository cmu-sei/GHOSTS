// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HtmlAgilityPack;

namespace Ghosts.Domain.Code.Helpers
{
    public static class UriExtensions
    {
        public static string GetDomain(this Uri uri)
        {
            var a = uri.Host.Split('.');
            return a.GetUpperBound(0) < 2 ? uri.Host : $"{a[a.GetUpperBound(0) - 1]}.{a[a.GetUpperBound(0)]}";
        }

        public static string GetUriHost(this string uriString)
        {
            try
            {
                if (!uriString.Contains(Uri.SchemeDelimiter))
                {
                    uriString = string.Concat(Uri.UriSchemeHttp, Uri.SchemeDelimiter, uriString);
                }
                return new Uri(uriString).Host;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return uriString;
            }
        }

        public static string CleanUrl(this string url)
        {
            var index = url.IndexOf("#");
            if (index >= 0)
                url = url.Substring(0, index);
            index = url.IndexOf("?");
            if (index >= 0)
                url = url.Substring(0, index);
            return url;
        }

        public static IEnumerable<string> GetHrefUrls(this string input)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(input);

            return doc.DocumentNode.SelectNodes("//a[@href]")//this xpath selects all anchor tags
                .Select(p => p.Attributes["href"].Value);
        }

        //TODO:clean this up
        public static void OpenUrl(this string url)
        {
            try
            {
                Process.Start(url);
            }
            catch
            {
                //ignore
            }
        }
    }
}
