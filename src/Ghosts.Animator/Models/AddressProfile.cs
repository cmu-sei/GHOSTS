// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Newtonsoft.Json;

namespace Ghosts.Animator.Models
{
    public class AddressProfiles
    {
        public class InternationalAddressProfile
        {
            [JsonProperty("country")]
            public string Country { get; set; }

            [JsonProperty("geonameid")]
            public string GeoNameId { get; set; }

            [JsonProperty("name")]
            public string City { get; set; }

            [JsonProperty("subcountry")]
            public string SubCountry { get; set; }

            public override string ToString()
            {
                return $"{City}, {Country}";
            }
        }

        public class AddressProfile
        {
            public string AddressType { get; set; }
            public string Name { get; set; }
            public string Address1 { get; set; }
            public string Address2 { get; set; }
            public string City { get; set; }
            public string State { get; set; }
            public string PostalCode { get; set; }

            public override string ToString()
            {
                //TODO: clean up
                return $"{Address1} {City}, {State} {PostalCode}";
            }
        }
    }
}
