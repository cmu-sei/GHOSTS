// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;
using System.Text.Json.Serialization;

#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.

namespace ghosts.api.Infrastructure.Animations.AnimationDefinitions.Chat.Mattermost;

public class Team
{
    [JsonPropertyName("id")] public string Id { get; set; }
    [JsonPropertyName("create_at")] public long CreateAt { get; set; }
    [JsonPropertyName("update_at")] public long UpdateAt { get; set; }
    [JsonPropertyName("delete_at")] public long DeleteAt { get; set; }
    [JsonPropertyName("display_name")] public string DisplayName { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("description")] public string Description { get; set; }
    [JsonPropertyName("email")] public string Email { get; set; }
    [JsonPropertyName("type")] public string Type { get; set; }
    [JsonPropertyName("company_name")] public string CompanyName { get; set; }
    [JsonPropertyName("allowed_domains")] public string AllowedDomains { get; set; }
    [JsonPropertyName("invite_id")] public string InviteId { get; set; }

    [JsonPropertyName("allow_open_invite")] public bool? AllowOpenInvite { get; set; }

    [JsonPropertyName("scheme_id")] public string SchemeId { get; set; }

    [JsonPropertyName("group_constrained")] public bool? GroupConstrained { get; set; }

    [JsonPropertyName("policy_id")] public string? PolicyId { get; set; } // Nullable for "null" JSON values

    [JsonPropertyName("cloud_limits_archived")] public bool? CloudLimitsArchived { get; set; }
}

public class TeamComparer : IEqualityComparer<Team>
{
    public bool Equals(Team x, Team y)
    {
        return x.Id == y.Id;
    }

    public int GetHashCode(Team obj)
    {
        return obj.Id.GetHashCode();
    }
}
