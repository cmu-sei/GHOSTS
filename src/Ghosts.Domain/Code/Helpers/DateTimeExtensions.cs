// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.IO;

namespace Ghosts.Domain.Code.Helpers
{
    public static class DateTimeExtensions
    {
        public static bool IsOlderThanHours(string filename, int hours)
        {
            var threshold = DateTime.Now.AddHours(-hours);
            return File.GetCreationTime(filename) <= threshold;
        }
    }
}
