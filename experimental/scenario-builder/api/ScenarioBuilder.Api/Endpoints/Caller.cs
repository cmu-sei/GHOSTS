// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using ScenarioBuilder.Api.Domain;

namespace ScenarioBuilder.Api.Endpoints;

/// <summary>
/// Who is calling. The user comes from the auth proxy's <c>X-Forwarded-User</c>, or is <c>dev</c> without one.
/// The agent's API client always sends <c>X-Via: agent</c>. That isn't security, since the agent acts as the
/// same user, but the model can't set headers, so the authority rules built on it hold whichever model runs.
/// </summary>
public record Caller(string User, Via Via)
{
    public const string DefaultUser = "dev";

    public bool IsAgent => Via == Via.Agent;

    public static Caller From(HttpContext context)
    {
        var user = context.Request.Headers["X-Forwarded-User"].ToString().Trim();
        var via = string.Equals(context.Request.Headers["X-Via"].ToString().Trim(), "agent", StringComparison.OrdinalIgnoreCase)
            ? Via.Agent
            : Via.Person;
        return new Caller(user.Length > 0 ? user : DefaultUser, via);
    }

    /// <summary>Admins are the users listed under <c>Admins</c> (default <c>dev</c>). The agent never is one.</summary>
    public static bool IsAdmin(Caller caller, IConfiguration config) =>
        !caller.IsAgent && (config.GetSection("Admins").Get<string[]>() ?? [DefaultUser]).Contains(caller.User);
}

/// <summary>Put on every admin endpoint: anyone else gets <c>403 NOT_ADMIN</c>.</summary>
public sealed class AdminOnly(IConfiguration config) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var caller = Caller.From(context.HttpContext);
        if (Caller.IsAdmin(caller, config))
            return await next(context);
        return caller.IsAgent
            ? ErrorResults.Forbidden("NOT_ADMIN", "The agent can't manage template libraries. An admin does that in Admin.")
            : ErrorResults.Forbidden("NOT_ADMIN", $"Only admins manage template libraries, and {caller.User} isn't one. Admins are listed under Admins in the API's settings.");
    }
}

/// <summary>Every error body. Messages are instructions to whoever acts next; the agent's tools pass them to the model.</summary>
public record ApiError(string Code, string Message);

public static class ErrorResults
{
    public static IResult NotFound(string code, string message) => Error(StatusCodes.Status404NotFound, code, message);

    public static IResult Conflict(string code, string message) => Error(StatusCodes.Status409Conflict, code, message);

    public static IResult BadRequest(string code, string message) => Error(StatusCodes.Status400BadRequest, code, message);

    public static IResult Forbidden(string code, string message) => Error(StatusCodes.Status403Forbidden, code, message);

    public static IResult NotImplemented(string code, string message) => Error(StatusCodes.Status501NotImplemented, code, message);

    public static IResult Unprocessable(string code, string message) => Error(StatusCodes.Status422UnprocessableEntity, code, message);

    public static IResult TooLarge(string code, string message) => Error(StatusCodes.Status413PayloadTooLarge, code, message);

    public static IResult Unsupported(string code, string message) => Error(StatusCodes.Status415UnsupportedMediaType, code, message);

    private static IResult Error(int status, string code, string message) => Results.Json(new ApiError(code, message), statusCode: status);
}
