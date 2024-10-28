// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Microsoft.AspNetCore.Http;

namespace Ghosts.Api.Infrastructure.Extensions
{
    public static class ResponseExtensions
    {
        public static void AddApplicationError(this HttpResponse response, string message)
        {
            response.Headers.Append("Application-Error", message);
            // CORS
            response.Headers.Append("access-control-expose-headers", "Application-Error");
        }
    }
}
