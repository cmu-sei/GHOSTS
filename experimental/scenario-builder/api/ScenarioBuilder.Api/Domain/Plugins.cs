// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

namespace ScenarioBuilder.Api.Domain;

public record PluginInfo(string Id, string Name, string Description);

/// <summary>The apps a scenario can be sent to. An admin connects each one by its API's address.</summary>
public static class Plugins
{
    public static readonly IReadOnlyList<PluginInfo> All =
    [
        new("blueprint", "Blueprint", "Plan the exercise’s events and timeline."),
        new("cite", "CITE", "Score and track each team’s response."),
        new("gallery", "Gallery", "Deliver injects and news to participants."),
        new("player", "Player", "Give each team its exercise workspace."),
        new("steamfitter", "Steamfitter", "Run scripted tasks on range machines."),
        new("ghosts", "GHOSTS", "Bring the range to life with simulated users.")
    ];

    public static PluginInfo? Find(string id) => All.FirstOrDefault(p => p.Id == id);
}
