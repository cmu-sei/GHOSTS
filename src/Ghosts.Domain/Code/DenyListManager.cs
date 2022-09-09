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

        public static IEnumerable<string> Load()
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

        public static bool IsInList(string item)
        {
            var list = Load();
            return list.Any(x => x.StartsWith(item.GetUriHost(), StringComparison.InvariantCultureIgnoreCase));
        }

        public static IEnumerable<string> ScrubList(IEnumerable<string> list)
        {
            var denyList = Load();
            var filteredList = new List<string>();
            foreach (var item in list)
            {
                if (!denyList.Any(x => x.StartsWith(item.GetUriHost(), StringComparison.InvariantCultureIgnoreCase)))
                {
                    filteredList.Add(item);
                }
            }
            return filteredList;
        }
    }
}
