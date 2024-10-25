// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Ghosts.Animator.Enums
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum BiologicalSex
    {
        Female = 0,
        Male = 1
    }
}
