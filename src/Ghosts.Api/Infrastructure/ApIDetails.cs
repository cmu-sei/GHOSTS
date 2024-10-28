// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.IO;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Ghosts.Api.Infrastructure
{
    public static class ApiDetails
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public enum Roles
        {
            Admin = 0
        }

        public static void LoadConfiguration()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json");

            var config = builder.Build();
            var appConfig = new ApplicationSettings();
            config.GetSection("ApplicationSettings").Bind(appConfig);

            var initConfig = new InitOptions();
            config.GetSection("InitSettings").Bind(initConfig);

            Program.ApplicationSettings = appConfig;
        }
    }
}
