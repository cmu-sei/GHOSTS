// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ghosts.Domain.Code.Helpers;
using NLog;

namespace Ghosts.Domain.Code
{
    public static class DenyListManager
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public static IEnumerable<string> LoadDenyList()
        {
            try
            {
                return File.ReadAllLines(ApplicationDetails.ConfigurationFiles.DenyList);
            }
            catch (Exception e)
            {
                _log.Trace($"Cannot load deny list with exception: {e}");
                return new List<string>();
            }
        }

        public static bool IsInDenyList(string itemToEvaluate)
        {
            var denyList = LoadDenyList();
            return EvaluateItemAgainstDenyList(denyList, itemToEvaluate);
        }

        public static bool IsInDenyList(IEnumerable<string> denyList, string itemToEvaluate)
        {
            return EvaluateItemAgainstDenyList(denyList, itemToEvaluate);
        }

        public static bool IsInDenyList(IEnumerable<string> denyList, Uri uri)
        {
            var itemToEvaluate = uri.ToString();
            if (itemToEvaluate.Contains("#"))
            {
                itemToEvaluate = itemToEvaluate.Substring(0, uri.ToString().IndexOf("#", StringComparison.InvariantCultureIgnoreCase));
            }
            if (itemToEvaluate.Contains("?"))
            {
                itemToEvaluate = itemToEvaluate.Substring(0, uri.ToString().IndexOf("?", StringComparison.InvariantCultureIgnoreCase));
            }
            return EvaluateItemAgainstDenyList(denyList, itemToEvaluate);
        }

        public static IEnumerable<string> RemoveDeniedFromList(IEnumerable<string> listToEvaluate)
        {
            var originalList = listToEvaluate.ToArray();

            try
            {
                var denyList = LoadDenyList().ToArray();
                var filteredList = new List<string>();
                foreach (var itemToEvaluate in listToEvaluate)
                {
                    if (!EvaluateItemAgainstDenyList(denyList, itemToEvaluate))
                    {
                        filteredList.Add(itemToEvaluate);
                    }
                }
                return filteredList;
            }
            catch (Exception e)
            {
                _log.Trace($"Could not remove from list: {e}");
                return originalList;
            }
        }

        private static bool EvaluateItemAgainstDenyList(IEnumerable<string> denyList, string itemToEvaluate)
        {
            foreach (var denyItem in denyList)
            {
                if (ShouldDeny(denyItem, itemToEvaluate))
                    return true;
            }
            return false;
        }

        private static bool ShouldDeny(string denyRule, string itemToEvaluate)
        {
            itemToEvaluate = itemToEvaluate.CleanUrl();

            if (itemToEvaluate.Equals(denyRule))
            {
                Console.WriteLine("Matches exact");
                return true;
            }

            if (denyRule.Contains("*"))
            {
                if (denyRule.Count(x => x == '*') > 1)
                {
                    if (itemToEvaluate.Contains(denyRule.Replace("*", "")))
                    {
                        Console.WriteLine("Matches (*/* deny rule");
                        return true;
                    }
                }
                else if (denyRule.EndsWith("*"))
                {
                    if (itemToEvaluate.StartsWith(denyRule.Replace("*", ""),
                            StringComparison.InvariantCultureIgnoreCase))
                    {
                        Console.WriteLine("Matches (end/*) deny rule");
                        return true;
                    }
                }
                else if (denyRule.StartsWith("*"))
                {
                    if (itemToEvaluate.EndsWith(denyRule.Replace("*", ""),
                            StringComparison.InvariantCultureIgnoreCase))
                    {
                        Console.WriteLine("Matches (*/deny) deny rule");
                        return true;
                    }
                }
            }

            var o = itemToEvaluate.Equals(denyRule.Replace("*", ""), StringComparison.InvariantCultureIgnoreCase);
            if (o)
            {
                Console.WriteLine("Matches full (*/deny) deny rule");
            }
            return o;
        }
    }
}
