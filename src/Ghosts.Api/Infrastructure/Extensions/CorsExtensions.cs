// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Configuration;

namespace Ghosts.Api.Infrastructure.Extensions
{
    public static class CorsPolicyExtensions
    {
        public static CorsOptions UseConfiguredCors(
            this CorsOptions builder,
            IConfiguration section
        )
        {
            var policy = new CorsPolicyOptions();
            section.Bind(policy);
            builder.AddPolicy("default", policy.Build());
            return builder;
        }
    }

    public class CorsPolicyOptions
    {
        public string[] Origins { get; set; }
        public string[] Methods { get; set; }
        public string[] Headers { get; set; }
        public bool AllowAnyOrigin { get; set; }
        public bool AllowAnyMethod { get; set; }
        public bool AllowAnyHeader { get; set; }
        public bool SupportsCredentials { get; set; }

        public CorsPolicy Build()
        {
            var policy = new CorsPolicyBuilder();
            if (AllowAnyOrigin)
                policy.AllowAnyOrigin();
            else
                policy.WithOrigins(Origins);

            if (AllowAnyHeader)
                policy.AllowAnyHeader();
            else
                policy.WithHeaders(Headers);

            if (AllowAnyMethod)
                policy.AllowAnyMethod();
            else
                policy.WithMethods(Methods);

            if (SupportsCredentials)
                policy.AllowCredentials();
            else
                policy.DisallowCredentials();

            return policy.Build();
        }
    }
}
