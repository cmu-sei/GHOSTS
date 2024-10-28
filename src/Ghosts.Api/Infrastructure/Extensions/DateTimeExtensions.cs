// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;

namespace ghosts.api.Infrastructure.Extensions;

public static class DateTimeExtensions
{
    public static DateTime ToDateTime(this long unixMilliseconds)
    {
        var epochStart = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var postCreationDate = epochStart.AddMilliseconds(unixMilliseconds);
        return postCreationDate;
    }
}
