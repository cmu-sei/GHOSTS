using System.Collections.Generic;

namespace Ghosts.Api.Infrastructure.Models;

public class ApplicationSettings
{
    public int OfflineAfterMinutes { get; set; }
    public int LookbackRecords { get; set; }
    public string MatchMachinesBy { get; set; }
    public int CacheTime { get; set; }
    public int QueueSyncDelayInSeconds { get; set; }
    public int NotificationsQueueSyncDelayInSeconds { get; set; }
    public int ListenerPort { get; set; }

    /// <summary>
    /// How many random NPCs to generate when the database has none. 0 to skip.
    /// </summary>
    public int SeedNpcCount { get; set; }

    public AnimatorSettingsDetail AnimatorSettings { get; set; }
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

    public class AnimatorSettingsDetail
    {
        public string Proxy { get; set; }

        public class ContentEngineSettings
        {
            public string Source { get; set; }
            public string Model { get; set; }
            public string Host { get; set; }
            // AWS Bedrock: region only — credentials must be supplied via
            // AWS_ACCESS_KEY_ID / AWS_SECRET_ACCESS_KEY environment variables
            // or the default credential chain (IAM role, ~/.aws/credentials, etc.)
            public string AwsRegion { get; set; }
        }
    }
}

public class InitOptions
{
    public string AdminUsername { get; set; }
    public string AdminPassword { get; set; }
}
