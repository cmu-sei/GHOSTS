// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

namespace ScenarioBuilder.Api.Endpoints;

/// <summary>One endpoint per file. Every implementation is found and mapped under <c>/api</c> at startup.</summary>
public interface IEndpoint
{
    RouteHandlerBuilder Register(RouteGroupBuilder group, string tag);
}

public static class EndpointBuilders
{
    public static WebApplicationBuilder AddApiEndpoints(this WebApplicationBuilder builder)
    {
        foreach (var type in EndpointTypes())
            builder.Services.AddTransient(type);
        return builder;
    }

    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("/api").DisableAntiforgery();
        using var scope = builder.ServiceProvider.CreateScope();
        foreach (var type in EndpointTypes())
        {
            var endpoint = (IEndpoint)scope.ServiceProvider.GetRequiredService(type);
            endpoint.Register(group, type.Namespace?.Split('.').Last() ?? "");
        }
        return builder;
    }

    private static IEnumerable<Type> EndpointTypes() =>
        typeof(Program).Assembly.GetTypes().Where(t => typeof(IEndpoint).IsAssignableFrom(t) && t is { IsInterface: false, IsAbstract: false });
}
