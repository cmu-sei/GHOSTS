// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Ghosts.Api.Models
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum StatusType
    {
        Active = 0,
        Deleted = -9
    }
}