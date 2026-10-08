// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Ghosts.Api.Controllers.Api;

/// <summary>
/// Scenario authoring: a session, its turns, its record, and the import. No more open than the rest of the
/// API (I1): a scenario's sessions are shown to whoever the scenario is shown to (I2). A turn runs in the
/// background and reports through the Scenario Builder's hub; the session's record is what the page reads
/// when the turn ends.
/// </summary>
[ApiController]
[Route("api/scenario-authoring")]
public class ScenarioAuthoringController(IScenarioAuthoringService authoring, ScenarioAuthoringRunner runner,
    IOptions<ScenarioAuthoringOptions> options, IScenarioService scenarios, CurrentUser user) : ControllerBase
{
    public class SessionRequest
    {
        /// <summary>The Scenario Builder's scenario, which the import replaces. Without one, the import makes a new scenario.</summary>
        public int? ScenarioId { get; set; }

        /// <summary>One of the configured models' ids. Without one, the session uses the default.</summary>
        public string Model { get; set; }

        /// <summary>One of the model's effort levels (H3). Without one, the model's default.</summary>
        public string Effort { get; set; }
    }

    public class TurnRequest
    {
        public string Message { get; set; }
    }

    public class ImportRequest
    {
        public string Hash { get; set; }
        public bool Again { get; set; }
        public bool Replace { get; set; }
    }

    /// <summary>The models a new session may use, and the one it uses by default.</summary>
    // GET: api/scenario-authoring/models
    [HttpGet("models")]
    public IActionResult GetModels() => Ok(new { model = options.Value.Model, models = options.Value.Models });

    // POST: api/scenario-authoring/sessions
    [HttpPost("sessions")]
    public async Task<IActionResult> CreateSession([FromBody] SessionRequest request, CancellationToken ct)
    {
        try
        {
            if (request?.ScenarioId is int scenarioId && !await scenarios.IsVisibleAsync(scenarioId, user.Name, ct))
                return NotFound(new { error = $"No scenario {scenarioId}." });
            var session = await authoring.CreateSessionAsync(request?.ScenarioId, request?.Model, request?.Effort, ct);
            return Ok(new { id = session.Id, model = session.Model, effort = session.Effort, scenarioId = session.ScenarioId });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>The readiness dashboard for a scenario with no session yet: what the conversation will ask.</summary>
    // GET: api/scenario-authoring/readiness?scenarioId=5
    [HttpGet("readiness")]
    public async Task<IActionResult> GetReadiness([FromQuery] int scenarioId, CancellationToken ct) =>
        await scenarios.IsVisibleAsync(scenarioId, user.Name, ct)
            ? Ok(await authoring.GetReadinessAsync(scenarioId, ct))
            : NotFound(new { error = $"No scenario {scenarioId}." });

    /// <summary>A scenario's sessions, newest first.</summary>
    // GET: api/scenario-authoring/sessions?scenarioId=5
    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions([FromQuery] int scenarioId, CancellationToken ct) =>
        await scenarios.IsVisibleAsync(scenarioId, user.Name, ct)
            ? Ok(await authoring.SessionsAsync(scenarioId, ct))
            : NotFound(new { error = $"No scenario {scenarioId}." });

    /// <summary>
    /// Starts one turn and returns at once. The turn is not tied to the request: a browser that goes away
    /// does not cancel it; its own limit does. Its progress and its end come through the hub.
    /// </summary>
    // POST: api/scenario-authoring/sessions/{id}/turns
    [HttpPost("sessions/{id:guid}/turns")]
    public async Task<IActionResult> RunTurn(Guid id, [FromBody] TurnRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request?.Message)) return BadRequest(new { error = "A turn needs a message." });
        var session = await authoring.FindSessionAsync(id, ct);
        if (session == null) return NotFound(new { error = $"No authoring session {id}." });

        return runner.TryStart(id, session.ScenarioId, request.Message)
            ? Accepted(new { sessionId = id })
            : Conflict(new { error = $"A turn is already running in session {id}." });
    }

    /// <summary>The session's turns and their results, its documents' counts, its model calls, and its token totals.</summary>
    // GET: api/scenario-authoring/sessions/{id}
    [HttpGet("sessions/{id:guid}")]
    public async Task<IActionResult> GetSession(Guid id, CancellationToken ct)
    {
        var session = await authoring.GetSessionAsync(id, runner.IsRunning(id), ct);
        return session == null ? NotFound() : Ok(session);
    }

    /// <summary>
    /// One validated document, with its findings and change list. Returning it records that the developer
    /// was shown it (A3).
    /// </summary>
    // GET: api/scenario-authoring/sessions/{id}/documents/{hash}
    [HttpGet("sessions/{id:guid}/documents/{hash}")]
    public async Task<IActionResult> GetDocument(Guid id, string hash, CancellationToken ct)
    {
        var document = await authoring.GetDocumentAsync(id, hash, ct);
        return document == null ? NotFound() : Ok(document);
    }

    /// <summary>
    /// One validated document as an exercise plan in Markdown, rendered by the server from the document (B4).
    /// Returning it records that the developer was shown the document (A3), as the document endpoint does.
    /// </summary>
    // GET: api/scenario-authoring/sessions/{id}/documents/{hash}/plan
    [HttpGet("sessions/{id:guid}/documents/{hash}/plan")]
    [Produces("text/markdown")]
    public async Task<IActionResult> GetPlan(Guid id, string hash, CancellationToken ct)
    {
        var plan = await authoring.GetPlanAsync(id, hash, ct);
        return plan == null ? NotFound() : Content(plan, "text/markdown");
    }

    /// <summary>The text of a source chunk a reply cites (J2).</summary>
    // GET: api/scenario-authoring/sessions/{id}/chunks/{chunkId}
    [HttpGet("sessions/{id:guid}/chunks/{chunkId:int}")]
    public async Task<IActionResult> GetChunk(Guid id, int chunkId, CancellationToken ct)
    {
        var chunk = await authoring.GetChunkAsync(id, chunkId, ct);
        return chunk == null ? NotFound() : Ok(chunk);
    }

    /// <summary>
    /// Imports the session's latest document, when it is the one named, validated with 0 errors and shown.
    /// Anything else is refused with the reason. A hash imported before needs "again": true, and replacing
    /// what the Scenario Builder's scenario holds needs "replace": true.
    /// </summary>
    // POST: api/scenario-authoring/sessions/{id}/import
    [HttpPost("sessions/{id:guid}/import")]
    public async Task<IActionResult> Import(Guid id, [FromBody] ImportRequest request, CancellationToken ct)
    {
        if (runner.IsRunning(id)) return Conflict(new { error = $"A turn is running in session {id}; import when it ends." });
        try
        {
            var result = await authoring.ImportAsync(id, request?.Hash, request?.Again ?? false, request?.Replace ?? false, ct);
            var body = new
            {
                result.Imported,
                result.ScenarioId,
                result.Hash,
                result.Reason,
                result.NeedsConfirmation,
                result.Confirm,
                result.Report
            };
            return result.Imported ? Ok(body) : StatusCode(StatusCodes.Status409Conflict, body);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (AuthoringSessionBusyException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }
}
