// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using Ghosts.Client.Infrastructure;
using Ghosts.Domain;
using Newtonsoft.Json;
using NLog;

namespace Ghosts.Client.Universal.Infrastructure;

internal static class OfficeHelpers
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();

    internal static bool ShouldOpenExisting(TimelineHandler handler)
    {
        if (handler.HandlerArgs.TryGetValue("workingset", out var key))
        {
            try
            {
                dynamic obj = JsonConvert.DeserializeObject(key.ToString() ?? string.Empty);
                if (obj != null)
                {
                    var max = Convert.ToInt32(obj.max);
                    var maxAgeInHours = Convert.ToInt32(obj["max-age-in-hours"]);
                    var currentDocCount = FileListing.GetFileCount(handler.HandlerType, maxAgeInHours);
                    if (currentDocCount > max)
                    {
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
            }
        }

        return false;
    }
}
