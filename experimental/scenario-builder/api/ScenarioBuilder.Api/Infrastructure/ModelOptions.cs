// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

namespace ScenarioBuilder.Api.Infrastructure;

/// <summary>One model the agent may use: the name it asks for, and the OpenAI-compatible server and model behind it.</summary>
public sealed class ModelEntry
{
    public string Name { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;

    /// <summary>The environment variable holding the server's key. Keys never reach the browser.</summary>
    public string? ApiKeyEnv { get; set; }

    public int ContextWindow { get; set; } = 32768;
}

/// <summary>The <c>ModelServers</c> section. A local model can take minutes to read a long prompt, hence the long timeout.</summary>
public sealed class ModelOptions
{
    public const string Section = "ModelServers";

    public List<ModelEntry> Models { get; set; } = [];
    public int TimeoutMinutes { get; set; } = 30;
}
