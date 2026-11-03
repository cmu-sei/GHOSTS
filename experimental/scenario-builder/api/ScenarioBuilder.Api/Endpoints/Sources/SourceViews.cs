// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Sources;

public record SourceView(int Id, SourceKind Kind, string Name, string? Origin, int Characters, string AddedBy, DateTime Added)
{
    public static SourceView From(Source s) => new(s.Id, s.Kind, s.Name, s.Origin, s.Text.Length, s.AddedBy, s.Added);
}

/// <summary>What adding any kind of source shares: only a person adds one, and it must hold some text, but not too much.</summary>
public static class SourceRules
{
    public static IResult? AgentRefused(Caller caller) => caller.IsAgent
        ? ErrorResults.Forbidden("AGENT_CANNOT_CHANGE_SOURCES", "Only a person adds or removes context. Read it with search_sources.")
        : null;

    public static async Task<IResult> Save(BuilderDb db, Scenario scenario, Caller caller, SourceKind kind, string name, string? origin, string text, CancellationToken ct)
    {
        text = SourceText.Normalize(text);
        if (text.Length == 0)
            return ErrorResults.Unprocessable("SOURCE_EMPTY", $"No text was found in {name}. A scanned PDF has none: paste its text instead.");
        if (text.Length > SourceText.MaxCharacters)
            return ErrorResults.TooLarge("SOURCE_TOO_LARGE",
                $"{name} has {text.Length:N0} characters of text; a source holds up to {SourceText.MaxCharacters:N0}. Split it, or add the parts that matter.");

        var now = DateTime.UtcNow;
        var source = new Source { ScenarioId = scenario.Id, Kind = kind, Name = name, Origin = origin, Text = text, AddedBy = caller.User, Added = now };
        db.Sources.Add(source);
        scenario.Updated = now;
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/scenarios/{scenario.Id}/sources/{source.Id}", SourceView.From(source));
    }
}
