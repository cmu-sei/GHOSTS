// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json.Serialization;

namespace ghosts.api.Infrastructure.Animations.AnimationDefinitions.Chat.Mattermost;

public class User
{
    [JsonPropertyName("id")] public string Id { get; set; }
    [JsonPropertyName("create_at")] public object CreateAt { get; set; }
    [JsonPropertyName("update_at")] public object UpdateAt { get; set; }
    [JsonPropertyName("delete_at")] public object DeleteAt { get; set; }
    [JsonPropertyName("username")] public string Username { get; set; }
    [JsonPropertyName("first_name")] public string FirstName { get; set; }
    [JsonPropertyName("last_name")] public string LastName { get; set; }
    [JsonPropertyName("nickname")] public string Nickname { get; set; }
    [JsonPropertyName("email")] public string Email { get; set; }
    [JsonPropertyName("email_verified")] public object EmailVerified { get; set; }
    [JsonPropertyName("auth_service")] public string AuthService { get; set; }
    [JsonPropertyName("roles")] public string Roles { get; set; }
    [JsonPropertyName("locale")] public string Locale { get; set; }
    [JsonPropertyName("notify_props")] public object NotifyProps { get; set; }
    [JsonPropertyName("props")] public object Props { get; set; }

    [JsonPropertyName("last_password_update")]
    public object LastPasswordUpdate { get; set; }

    [JsonPropertyName("last_picture_update")]
    public object LastPictureUpdate { get; set; }

    [JsonPropertyName("failed_attempts")] public object FailedAttempts { get; set; }
    [JsonPropertyName("mfa_active")] public object MfaActive { get; set; }
    [JsonPropertyName("timezone")] public object Timezone { get; set; }

    [JsonPropertyName("terms_of_service_id")]
    public string TermsOfServiceId { get; set; }

    [JsonPropertyName("terms_of_service_create_at")]
    public object TermsOfServiceCreateAt { get; set; }
}

public class UserCreate
{
    public string Email { get; set; }
    public string Username { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Nickname { get; set; }
    public string Password { get; set; }

    public object ToObject()
    {
        return new
        {
            email = Email,
            username = Username,
            first_name = FirstName,
            last_name = LastName,
            nickname = Nickname,
            password = Password
        };
    }
}
