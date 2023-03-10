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
                _log.Trace(e);
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

        public static IEnumerable<string> RemoveDeniedFromList(IEnumerable<string> listToEvaluate)
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
            if (denyRule.Contains("*"))
            {
                if (denyRule.Count(x => x == '*') > 1)
                {
                    if (itemToEvaluate.Contains(denyRule.Replace("*", "").GetUriHost()))
                        return true;
                }
                else if (denyRule.EndsWith("*"))
                {
                    if (itemToEvaluate.EndsWith(denyRule.Replace("*", "").GetUriHost(), StringComparison.InvariantCultureIgnoreCase))
                        return true;
                }
                else if (denyRule.StartsWith("*"))
                {
                    if (itemToEvaluate.StartsWith(denyRule.Replace("*", "").GetUriHost(), StringComparison.InvariantCultureIgnoreCase))
                        return true;
                }
            }

            return itemToEvaluate.StartsWith(denyRule.Replace("*", "").GetUriHost(), StringComparison.InvariantCultureIgnoreCase);
        }
    }
}
