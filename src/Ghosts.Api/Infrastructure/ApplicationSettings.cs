using System.Collections.Generic;

namespace Ghosts.Api.Infrastructure;

public class ApplicationSettings
{
    public int OfflineAfterMinutes { get; set; }
    public int LookbackRecords { get; set; }
    public string MatchMachinesBy { get; set; }
    public int CacheTime { get; set; }
    public int QueueSyncDelayInSeconds { get; set; }
    public int NotificationsQueueSyncDelayInSeconds { get; set; }
    public int ListenerPort { get; set; }

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
        public AnimationsSettings Animations { get; set; }

        public class AnimationsSettings
        {
            public bool IsEnabled { get; set; }
            public SocialGraphSettings SocialGraph { get; set; }
            public SocialBeliefSettings SocialBelief { get; set; }
            public SocialSharingSettings SocialSharing { get; set; }
            public FullAutonomySettings FullAutonomy { get; set; }
            public ChatSettings Chat { get; set; }

            public class SocialGraphSettings
            {
                public bool IsEnabled { get; set; }
                public bool IsMultiThreaded { get; set; }
                public bool IsInteracting { get; set; }
                public int TurnLength { get; set; }

                public int MaximumSteps { get; set; }

                public double ChanceOfKnowledgeTransfer { get; set; }

                public DecaySettings Decay { get; set; }

                public class DecaySettings
                {
                    public int StepsTo { get; set; }
                    public double ChanceOf { get; set; }
                }
            }

            public class SocialBeliefSettings
            {
                public bool IsEnabled { get; set; }
                public bool IsMultiThreaded { get; set; }
                public bool IsInteracting { get; set; }
                public int TurnLength { get; set; }
                public int MaximumSteps { get; set; }
            }

            public class ChatSettings
            {
                public bool IsEnabled { get; set; }
                public bool IsMultiThreaded { get; set; }
                public bool IsInteracting { get; set; }
                public int TurnLength { get; set; }
                public int MaximumSteps { get; set; }
                public bool IsSendingTimelinesToGhostsApi { get; set; }
                public int PercentReplyVsNew { get; set; }
                public Dictionary<string, int> PostProbabilities { get; set; }
                public string PostUrl { get; set; }
                public ContentEngineSettings ContentEngine { get; set; }
            }

            public class SocialSharingSettings
            {
                public bool IsEnabled { get; set; }
                public bool IsMultiThreaded { get; set; }
                public bool IsInteracting { get; set; }
                public bool IsSendingTimelinesToGhostsApi { get; set; }
                public bool IsSendingTimelinesDirectToSocializer { get; set; }
                public string PostUrl { get; set; }
                public int TurnLength { get; set; }
                public int MaximumSteps { get; set; }
                public ContentEngineSettings ContentEngine { get; set; }
            }

            public class FullAutonomySettings
            {
                public bool IsEnabled { get; set; }
                public bool IsMultiThreaded { get; set; }
                public bool IsInteracting { get; set; }
                public bool IsSendingTimelinesToGhostsApi { get; set; }
                public int TurnLength { get; set; }
                public int MaximumSteps { get; set; }
                public ContentEngineSettings ContentEngine { get; set; }
            }
        }

        public class ContentEngineSettings
        {
            public string Source { get; set; }
            public string Model { get; set; }
            public string Host { get; set; }
        }
    }
}

public class InitOptions
{
    public string AdminUsername { get; set; }
    public string AdminPassword { get; set; }
}
