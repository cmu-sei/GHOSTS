using System;

namespace ghosts.api.Areas.Animator.Infrastructure.Extensions;

public static class DateTimeExtensions
{
    public static DateTime ToDateTime(this long unixMilliseconds)
    {
        var epochStart = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var postCreationDate = epochStart.AddMilliseconds(unixMilliseconds);
        return postCreationDate;
    }
}