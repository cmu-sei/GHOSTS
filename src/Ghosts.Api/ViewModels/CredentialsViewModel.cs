// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Newtonsoft.Json;

namespace Ghosts.Api.ViewModels
{
    public class CredentialsViewModel
    {
        [JsonProperty("username")]
        [JsonRequired]
        public string UserName { get; set; }

        [JsonProperty("password")]
        [JsonRequired]
        public string Password { get; set; }
    }

    public class CredentialsViewModelValidator
    {
    }
}
