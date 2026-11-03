// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Microsoft.Extensions.Options;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Model;

/// <summary>GET /model/models — the configured models, in the OpenAI list shape, with each one's context window.</summary>
public class ModelsEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapGet("/model/models", (IOptions<ModelOptions> options) => new
            {
                @object = "list",
                data = options.Value.Models.Select(m => new { id = m.Name, @object = "model", context_window = m.ContextWindow })
            })
            .WithTags(tag);
}
