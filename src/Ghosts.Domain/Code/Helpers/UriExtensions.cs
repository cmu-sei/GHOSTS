using System;

namespace Ghosts.Domain.Code.Helpers
{
    public static class UriExtensions
    {
        public static string GetDomain(this Uri uri)
        {
            var a = uri.Host.Split('.');
            return a.GetUpperBound(0) < 2 ? uri.Host : $"{a[a.GetUpperBound(0) - 1]}.{a[a.GetUpperBound(0)]}";
        }
    }
}
