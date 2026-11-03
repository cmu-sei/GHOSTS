// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Sources;

public record AddTextSource(string? Name, string? Text);

/// <summary>POST /scenarios/{id}/sources/text — pasted text, under a name.</summary>
public class AddTextEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapPost("/scenarios/{id:int}/sources/text", async (int id, AddTextSource body, HttpContext http, BuilderDb db, CancellationToken ct) =>
            {
                if (await db.Scenarios.FindAsync([id], ct) is not { } scenario)
                    return ScenarioState.Missing(id);
                var caller = Caller.From(http);
                if (SourceRules.AgentRefused(caller) is { } refused)
                    return refused;
                var name = body.Name?.Trim();
                if (string.IsNullOrEmpty(name))
                    return ErrorResults.BadRequest("NAME_REQUIRED", "Give the text a name, such as where it came from.");
                if (string.IsNullOrWhiteSpace(body.Text))
                    return ErrorResults.BadRequest("TEXT_REQUIRED", "Paste the text to add.");
                return await SourceRules.Save(db, scenario, caller, SourceKind.Text, name, null, body.Text, ct);
            })
            .WithTags(tag);
}
