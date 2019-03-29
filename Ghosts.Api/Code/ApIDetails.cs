// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace Ghosts.Api.Code
{
    public static class ApiDetails
    {
        public static string Version => Assembly.GetExecutingAssembly().GetName().Version.ToString();

        public static class Jwt
        {
            public static class JwtClaimIdentifiers
            {
                public const string Rol = "rol", Id = "id";
            }

            public static class JwtClaims
            {
                public const string ApiAccess = "api_access";
            }
        }

        public static void LoadConfiguration()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json");

            var config = builder.Build();
            var appConfig = new ClientOptions();
            config.GetSection("ClientSettings").Bind(appConfig);

            var initConfig = new InitOptions();
            config.GetSection("InitOptions").Bind(initConfig);

            Program.ClientConfig = appConfig;
            Program.InitConfig = initConfig;
        }

        public enum Roles
        {
            Admin = 0
        }

        public class ClientOptions
        {
            public int OfflineAfterMinutes { get; set; }
            public int LookbackRecords { get; set; }
            public bool IsMatchingIdByName { get; set; }
            public int CacheTime { get; set; }
            public int QueueSyncDelayInSeconds { get; set; }
            public int NotificationsQueueSyncDelayInSeconds { get; set; }
            public int ListenerPort { get; set; }

            public GroupingOptions Grouping { get; set; }

            public class GroupingOptions
            {
                public int GroupDepth { get; set; }
                public string GroupName { get; set; }
                public List<char> GroupDelimiters { get; set; }
                public List<GroupingDefinitionOption> GroupingDefinition { get; set; }
                public class GroupingDefinitionOption
                {
                    public string Value { get; set; }
                    public Dictionary<string, string> Replacements { get; set; }
                    public string Direction { get; set; }
                }
            }
        }

        public class InitOptions
        {
            public string AdminUsername { get; set; }
            public string AdminPassword { get; set; }
        }
    }
}