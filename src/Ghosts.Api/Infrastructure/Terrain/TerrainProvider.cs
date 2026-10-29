// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;

namespace Ghosts.Api.Infrastructure.Terrain;

/// <summary>An execution's terrain status, as GHOSTS shows it. TerrainService derives it from GraySpace's job and instance states.</summary>
public static class TerrainStatus
{
    public const string Queued = "queued";
    public const string Building = "building";
    public const string Ready = "ready";
    public const string Failed = "failed";
    public const string TearingDown = "tearing-down";
    public const string Deleted = "deleted";

    public static bool InProgress(string status) => status is Queued or Building or TearingDown;
}

/// <summary>Raised when GraySpace answers with an error or can't be reached, carrying its HTTP status (0 if none) and its words.</summary>
public class TerrainProviderException(int status, string code, string message) : Exception($"Terrain provider error {status} {code}: {message}")
{
    public int Status { get; } = status;
    public string Code { get; } = code;

    /// <summary>The provider's own words: FastAPI's detail, or why it could not be reached.</summary>
    public string Detail { get; } = message;

    /// <summary>The findings GraySpace refused a request for, when it gave them (POST /instances answers 422 with detail.findings).</summary>
    public System.Collections.Generic.List<Finding> Findings { get; init; }
}

/// <summary>
/// Where GraySpace is and how GHOSTS talks to it. Empty BaseUrl means none is configured, and every terrain call fails
/// with not_configured. The Aspire AppHost points BaseUrl at GHOSTS's own simulator: http://localhost:5000/stubs/grayspace
/// </summary>
public class TerrainProviderOptions
{
    public const string Section = "TerrainProvider";

    /// <summary>GraySpace's address, without /api/v1.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>GraySpace's GRAYSPACE_API_TOKEN, sent as Authorization: Bearer.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Where enclave hosts download the GHOSTS client archive (the ghosts-client role's client_url).</summary>
    public string ClientUrl { get; set; } = string.Empty;

    /// <summary>The GHOSTS API root as enclave hosts reach it over the management network (the role's api_url).</summary>
    public string GhostsApiUrl { get; set; } = string.Empty;

    /// <summary>Serve the in-memory GraySpace simulator at /stubs/grayspace/api/v1.</summary>
    public bool EnableStub { get; set; }

    /// <summary>Seconds a simulated build takes, so polling sees its stages.</summary>
    public int StubBuildSeconds { get; set; } = 30;
}
