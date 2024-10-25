// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ghosts.api.Infrastructure.Animations.AnimationDefinitions.Chat.Mattermost;

public class Channel
{
    [JsonPropertyName("id")] public string Id { get; set; }
    [JsonPropertyName("create_at")] public long CreateAt { get; set; }
    [JsonPropertyName("update_at")] public long UpdateAt { get; set; }
    [JsonPropertyName("delete_at")] public int DeleteAt { get; set; }
    [JsonPropertyName("team_id")] public string TeamId { get; set; }
    [JsonPropertyName("type")] public string Type { get; set; }
    [JsonPropertyName("display_name")] public string DisplayName { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("header")] public string Header { get; set; }
    [JsonPropertyName("purpose")] public string Purpose { get; set; }
    [JsonPropertyName("last_post_at")] public long LastPostAt { get; set; }
    [JsonPropertyName("total_msg_count")] public int TotalMsgCount { get; set; }
    [JsonPropertyName("extra_update_at")] public int ExtraUpdateAt { get; set; }
    [JsonPropertyName("creator_id")] public string CreatorId { get; set; }
    [JsonPropertyName("scheme_id")] public string SchemeId { get; set; }
    [JsonPropertyName("props")] public object Props { get; set; }
    [JsonPropertyName("group_constrained")] public bool? GroupConstrained { get; set; }
    [JsonPropertyName("shared")] public object Shared { get; set; }
    [JsonPropertyName("total_msg_count_root")] public int TotalMsgCountRoot { get; set; }
    [JsonPropertyName("policy_id")] public object PolicyId { get; set; }
    [JsonPropertyName("last_root_post_at")] public long LastRootPostAt { get; set; }
    [JsonPropertyName("team_display_name")] public string TeamDisplayName { get; set; }
    [JsonPropertyName("team_name")] public string TeamName { get; set; }
    [JsonPropertyName("team_update_at")] public long TeamUpdateAt { get; set; }
}

public class ChannelHistory
{
    public string PostId { get; set; }
    public string Message { get; set; }
    public string ChannelId { get; set; }
    public string ChannelName { get; set; }
    public string UserId { get; set; }
    public string UserName { get; set; }
    public DateTime Created { get; set; }
}

public class ChannelComparer : IEqualityComparer<Channel>
{
    public bool Equals(Channel x, Channel y)
    {
        return x.Id == y.Id;
    }

    public int GetHashCode(Channel obj)
    {
        return obj.Id.GetHashCode();
    }
}
