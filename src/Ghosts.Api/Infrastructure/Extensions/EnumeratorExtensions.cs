// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using NLog;

namespace ghosts.api.Infrastructure.Extensions;

public static class EnumeratorExtensions
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();

    public static string GetRandom(this IList<string> list, Random random)
    {
        try
        {
            return list.Count < 1 ? "" : list[random.Next(list.Count)];
        }
        catch (Exception e)
        {
            _log.Error(e);
            return "";
        }
    }
}
