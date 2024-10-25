// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Newtonsoft.Json;

namespace Ghosts.Api.Infrastructure.Extensions;

public static class JsonExtensions
{
    public static bool ContainsInvalidUnicode<T>(this T o)
    {
        var jsonString = JsonConvert.SerializeObject(o);
        return jsonString.Contains("\\u0000");
    }
}
