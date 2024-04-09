// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Ghosts.Animator.Enums
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum DegreeLevel
    {
        GED = 0,
        HSDiploma = 1,
        Associates = 2,
        Bachelors = 3,
        Masters = 4,
        Doctorate = 5,
        Professional = 6,
        None = 7
    }
}
