// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Ghosts.Animator.Enums
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum MilitaryBranch
    {
        USAF = 0,
        USARMY = 1,
        USCG = 2,
        USMC = 3,
        USN = 4
    }
}
