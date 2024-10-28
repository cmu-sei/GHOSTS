// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ghosts.api.Infrastructure.Animations.AnimationDefinitions.Chat.Mattermost;

public class PostResponse
{
    public Dictionary<string, Post> Posts { get; set; }
}

public class Post
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("create_at")]
    public long CreateAt { get; set; }

    [JsonPropertyName("update_at")]
    public long UpdateAt { get; set; }

    [JsonPropertyName("edit_at")]
    public object EditAt { get; set; }

    [JsonPropertyName("delete_at")]
    public object DeleteAt { get; set; }

    [JsonPropertyName("is_pinned")]
    public bool IsPinned { get; set; }

    [JsonPropertyName("user_id")]
    public string UserId { get; set; }

    [JsonPropertyName("channel_id")]
    public string ChannelId { get; set; }

    [JsonPropertyName("root_id")]
    public string RootId { get; set; }

    [JsonPropertyName("original_id")]
    public string OriginalId { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("props")]
    public object Props { get; set; }

    [JsonPropertyName("hashtags")]
    public string Hashtags { get; set; }

    [JsonPropertyName("pending_post_id")]
    public string PendingPostId { get; set; }

    [JsonPropertyName("reply_count")]
    public object ReplyCount { get; set; }

    [JsonPropertyName("last_reply_at")]
    public object LastReplyAt { get; set; }

    [JsonPropertyName("participants")]
    public object Participants { get; set; }

    [JsonPropertyName("metadata")]
    public object Metadata { get; set; }
}
