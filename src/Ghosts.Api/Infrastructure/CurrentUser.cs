// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Ghosts.Api.Infrastructure
{
    /// <summary>
    /// The user making the request, as the deployment's authenticating proxy names them in a header
    /// (Identity:UserHeader, X-Forwarded-User by default). GHOSTS has no sign-in of its own, so without a
    /// proxy everyone is "anonymous" and shares one set of drafts. It decides what is shown, not what is
    /// allowed: anyone who reaches the API directly can set the header.
    /// </summary>
    public class CurrentUser(IHttpContextAccessor accessor, IConfiguration configuration)
    {
        public const string Anonymous = "anonymous";

        public string Name
        {
            get
            {
                var header = configuration["Identity:UserHeader"] ?? "X-Forwarded-User";
                var value = accessor.HttpContext?.Request.Headers[header].ToString();
                return string.IsNullOrWhiteSpace(value) ? Anonymous : value.Trim();
            }
        }
    }
}
