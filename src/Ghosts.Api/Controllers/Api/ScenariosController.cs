// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.ScenarioDocuments;
using Ghosts.Api.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.Annotations;

namespace Ghosts.Api.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class ScenariosController : ControllerBase
{
    private readonly IScenarioService _scenarioService;
    private readonly INpcService _npcService;
    private readonly IScenarioDryRunService _dryRun;
    private readonly IHttpClientFactory _clients;
    private readonly CurrentUser _user;
    private readonly ILogger<ScenariosController> _logger;

    public ScenariosController(
        IScenarioService scenarioService,
        INpcService npcService,
        IScenarioDryRunService dryRun,
        IHttpClientFactory clients,
        CurrentUser user,
        ILogger<ScenariosController> logger)
    {
        _scenarioService = scenarioService;
        _npcService = npcService;
        _dryRun = dryRun;
        _clients = clients;
        _user = user;
        _logger = logger;
    }

    /// <summary>Every published scenario, and the caller's own drafts (I2).</summary>
    // GET: api/scenarios
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ScenarioDto>>> GetScenarios(CancellationToken ct)
    {
        try
        {
            var scenarios = await _scenarioService.GetAllAsync(ct);
            return Ok(scenarios.Where(s => s.IsVisibleTo(_user.Name)).Select(MapToDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting scenarios");
            return StatusCode(500, new { error = "Error retrieving scenarios" });
        }
    }

    // GET: api/scenarios/5
    [HttpGet("{id}")]
    public async Task<ActionResult<ScenarioDto>> GetScenario(int id, CancellationToken ct)
    {
        try
        {
            var scenario = await _scenarioService.GetByIdAsync(id, ct);
            // A draft is shown to its author alone (I2), by id as in the list.
            return scenario.IsVisibleTo(_user.Name) ? Ok(MapToDto(scenario)) : NotFound();
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting scenario {ScenarioId}", id);
            return StatusCode(500, new { error = "Error retrieving scenario" });
        }
    }

    // POST: api/scenarios
    [HttpPost]
    public async Task<ActionResult<ScenarioDto>> CreateScenario(CreateScenarioDto dto, CancellationToken ct)
    {
        try
        {
            var scenario = await _scenarioService.CreateAsync(dto, ct);
            return CreatedAtAction(nameof(GetScenario), new { id = scenario.Id }, MapToDto(scenario));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating scenario");
            return StatusCode(500, new { error = "Error creating scenario" });
        }
    }

    /// <summary>
    /// The scenario as a canonical scenario document (schema v1) — the authored specification, with
    /// no database ids, no timestamps and no run state. Two exports of an unchanged scenario are
    /// byte-identical, and POST api/scenarios/import accepts what this emits. A scenario imported from
    /// a document returns that document until its rows are edited; after an edit, or when it was built
    /// any other way, it returns a document derived from its rows. With ?derived=true it always returns
    /// the derived form, which is how a difference between the two can be seen.
    /// </summary>
    // GET: api/scenarios/5/document
    [HttpGet("{id}/document")]
    [Produces("application/json")]
    public async Task<IActionResult> GetScenarioDocument(int id, [FromQuery] bool derived, CancellationToken ct)
    {
        try
        {
            if (!await _scenarioService.IsVisibleAsync(id, _user.Name, ct)) return NotFound();
            return Content(await _scenarioService.ExportDocumentAsync(id, derived, ct), "application/json");
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting scenario {ScenarioId}", id);
            return StatusCode(500, new { error = "Error exporting scenario document" });
        }
    }

    /// <summary>
    /// The scenario document as an exercise plan, in Markdown (B4): the review only a person can do, rendered
    /// by the server from the document GET {id}/document returns, never written by a model. The same plan the
    /// schema tool's render command produces.
    /// </summary>
    // GET: api/scenarios/5/document/plan
    [HttpGet("{id}/document/plan")]
    [Produces("text/markdown")]
    public async Task<IActionResult> GetScenarioPlan(int id, CancellationToken ct)
    {
        try
        {
            if (!await _scenarioService.IsVisibleAsync(id, _user.Name, ct)) return NotFound();
            var text = await _scenarioService.ExportDocumentAsync(id, false, ct);
            var plan = ScenarioPlan.Render(JsonNode.Parse(text).AsObject(), $"scenario {id}'s document, hash `{ScenarioAuthoringTools.Hash(text)}`");
            return Content(plan, "text/markdown");
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rendering the plan of scenario {ScenarioId}", id);
            return StatusCode(500, new { error = "Error rendering the exercise plan" });
        }
    }

    /// <summary>
    /// The approved version (A5): the document the scenario was last imported from, whether its rows were
    /// edited since, and the change list of that edit. 404 when no document was ever imported.
    /// </summary>
    // GET: api/scenarios/5/document/approved
    [HttpGet("{id}/document/approved")]
    public async Task<IActionResult> GetApprovedDocument(int id, CancellationToken ct)
    {
        try
        {
            if (!await _scenarioService.IsVisibleAsync(id, _user.Name, ct)) return NotFound();
            var approved = await _scenarioService.ApprovedDocumentAsync(id, ct);
            return approved == null ? NotFound(new { error = $"Scenario {id} was never imported from a document." }) : Ok(approved);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Publishes a draft: it becomes visible to everyone and can be deployed. Only its author may publish
    /// it, and only when the scenario's document, as export returns it now, validates with 0 errors.
    /// There is no way back to draft.
    /// </summary>
    // POST: api/scenarios/5/publish
    [HttpPost("{id}/publish")]
    public async Task<IActionResult> PublishScenario(int id, CancellationToken ct)
    {
        Scenario scenario;
        try
        {
            scenario = await _scenarioService.GetByIdAsync(id, ct);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }

        if (scenario.PublishedAt != null)
            return Conflict(new { error = $"Scenario {id} is already published." });
        if (scenario.Author != _user.Name)
            return StatusCode(403, new { error = $"Only the author of this draft, {scenario.Author}, can publish it." });

        var document = JsonNode.Parse(await _scenarioService.ExportDocumentAsync(id, false, ct)) as JsonObject;
        var result = await ScenarioDocumentValidator.ValidateAsync(document, _clients, ct);
        if (!result.IsValid)
            return Conflict(new
            {
                error = $"The scenario's document has {result.Errors} errors, so it cannot be published.",
                validation = Findings(result.Findings)
            });

        return Ok(MapToDto(await _scenarioService.PublishAsync(id, ct)));
    }

    /// <summary>
    /// Validates a scenario document and returns the findings. Writes nothing, ever: this is the
    /// endpoint an authoring agent calls, and it has to be safe to call on a draft. With
    /// ?dryRun=true it also creates the scenario and generates its population inside a transaction
    /// that is always rolled back, so the findings include what loading it would actually do.
    /// </summary>
    // POST: api/scenarios/validate
    [HttpPost("validate")]
    [Consumes("application/json")]
    public async Task<IActionResult> ValidateScenarioDocument([FromQuery] bool dryRun, CancellationToken ct)
    {
        var (document, parseFailure) = await ReadDocument(ct);
        if (parseFailure != null) return Ok(Findings([parseFailure]));

        var result = await ScenarioDocumentValidator.ValidateAsync(document, _clients, ct);
        var findings = result.Findings.ToList();

        if (dryRun && result.IsValid)
        {
            findings.AddRange(await _dryRun.RunAsync(document, ct));
        }
        else if (dryRun)
        {
            findings.Add(ScenarioFinding.Note(4, "DRYRUN_SKIPPED", string.Empty,
                "The dry run did not run: the document has errors, and loading a document that cannot be imported says nothing."));
        }

        return Ok(Findings(findings));
    }

    /// <summary>
    /// Creates a scenario from a scenario document (schema v1). It runs the same validator as POST
    /// validate and refuses, writing nothing, on any finding of severity "error". On success the
    /// response carries the validator's findings for this run. What no column holds goes into the rows'
    /// extras, and the document itself is kept whole beside the rows with those findings, so
    /// GET {id}/document returns what was imported.
    /// </summary>
    // POST: api/scenarios/import
    [HttpPost("import")]
    [Consumes("application/json")]
    public async Task<IActionResult> ImportScenarioDocument(CancellationToken ct)
    {
        var (document, parseFailure) = await ReadDocument(ct);
        if (parseFailure != null) return BadRequest(Findings([parseFailure]));

        var result = await ScenarioDocumentValidator.ValidateAsync(document, _clients, ct);
        if (!result.IsValid) return BadRequest(Findings(result.Findings));

        try
        {
            var scenario = await _scenarioService.ImportDocumentAsync(document, result.Findings, ct);
            var body = ImportedBody(MapToDto(scenario), result.Findings);
            return CreatedAtAction(nameof(GetScenario), new { id = scenario.Id }, body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing scenario document");
            return StatusCode(500, new { error = "Error importing scenario document" });
        }
    }

    /// <summary>
    /// The created scenario, with every field ScenarioDto already exposes, plus "findings". Built as
    /// a plain object referencing dto's properties directly — not by round-tripping through
    /// System.Text.Json — because this API's output formatter is Newtonsoft (Program.cs
    /// AddNewtonsoftJson), which has no special case for System.Text.Json.Nodes.JsonObject and would
    /// reflect over its public shape instead of its contents. Additive: any caller reading today's
    /// fields is unaffected.
    /// </summary>
    private static object ImportedBody(ScenarioDto dto, IReadOnlyList<ScenarioFinding> findings) => new
    {
        dto.Id,
        dto.Name,
        dto.Description,
        dto.CreatedAt,
        dto.UpdatedAt,
        dto.ScenarioParameters,
        dto.TechnicalEnvironment,
        dto.GameMechanics,
        dto.Timeline,
        dto.BuilderStatus,
        findings = ScenarioDocumentValidator.Ordered(findings)
    };

    /// <summary>The request body as a document, or the tier-1 finding that says why it is not one.</summary>
    private async Task<(JsonObject Document, ScenarioFinding Failure)> ReadDocument(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var text = await reader.ReadToEndAsync(ct);

        try
        {
            return JsonNode.Parse(text) is JsonObject document
                ? (document, null)
                : (null, ScenarioFinding.Err(1, "SCHEMA_NOT_AN_OBJECT", string.Empty,
                    "A scenario document must be a JSON object."));
        }
        catch (JsonException ex)
        {
            return (null, ScenarioFinding.Err(1, "SCHEMA_NOT_JSON", string.Empty, ex.Message));
        }
    }

    private static object Findings(IReadOnlyList<ScenarioFinding> findings) => new
    {
        valid = findings.All(f => f.Severity != ScenarioFinding.Error),
        errors = findings.Count(f => f.Severity == ScenarioFinding.Error),
        warnings = findings.Count(f => f.Severity == ScenarioFinding.Warning),
        findings = ScenarioDocumentValidator.Ordered(findings)
    };

    // PUT: api/scenarios/5
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateScenario(int id, UpdateScenarioDto dto, CancellationToken ct)
    {
        try
        {
            await _scenarioService.UpdateAsync(id, dto, ct);
            return NoContent();
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating scenario {ScenarioId}", id);
            return StatusCode(500, new { error = "Error updating scenario" });
        }
    }

    // DELETE: api/scenarios/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteScenario(int id, CancellationToken ct)
    {
        try
        {
            await _scenarioService.DeleteAsync(id, ct);
            return NoContent();
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting scenario {ScenarioId}", id);
            return StatusCode(500, new { error = "Error deleting scenario" });
        }
    }

    /// <summary>
    /// Get all NPCs associated with a specific scenario
    /// </summary>
    /// <param name="scenarioId">The scenario ID</param>
    /// <returns>List of NPCs bound to the scenario</returns>
    [ProducesResponseType(typeof(ActionResult<IEnumerable<NpcRecord>>), (int)HttpStatusCode.OK)]
    [SwaggerResponse((int)HttpStatusCode.OK, Type = typeof(ActionResult<IEnumerable<NpcRecord>>))]
    [SwaggerOperation("GetScenarioNpcs")]
    [HttpGet("{scenarioId}/npcs")]
    public async Task<ActionResult<IEnumerable<NpcRecord>>> GetScenarioNpcs(int scenarioId)
    {
        try
        {
            var npcs = await _npcService.GetByScenarioId(scenarioId);
            return Ok(npcs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting NPCs for scenario {ScenarioId}", scenarioId);
            return StatusCode(500, new { error = "Error retrieving scenario NPCs" });
        }
    }

    // Helper methods
    private static ScenarioDto MapToDto(Scenario scenario)
    {
        return new ScenarioDto(
            scenario.Id,
            scenario.Name,
            scenario.Description,
            scenario.CreatedAt,
            scenario.UpdatedAt,
            scenario.ScenarioParameters != null ? new ScenarioParametersDto(
                scenario.ScenarioParameters.Nations.Select(n => new NationDto(n.Name, n.Alignment)).ToList(),
                scenario.ScenarioParameters.ThreatActors.Select(ta => new ThreatActorDto(
                    ta.Name,
                    ta.Type,
                    ta.Capability,
                    ta.Ttps?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>(),
                    ScenarioExtras.Read<AdversaryExtrasDto>(ta.Extras)
                )).ToList(),
                scenario.ScenarioParameters.Injects.Select(i => new InjectDto(i.Trigger, i.Title, ScenarioExtras.Read<EventExtrasDto>(i.Extras))).ToList(),
                scenario.ScenarioParameters.UserPools.Select(up => new UserPoolDto(up.Role, up.Count, ScenarioExtras.Read<PoolExtrasDto>(up.Extras))).ToList(),
                scenario.ScenarioParameters.Objectives,
                scenario.ScenarioParameters.PoliticalContext,
                scenario.ScenarioParameters.RulesOfEngagement,
                scenario.ScenarioParameters.VictoryConditions,
                scenario.ScenarioParameters.WorkflowBindings.Select(wb => new ScenarioWorkflowBindingDto(
                    wb.WorkflowRef, wb.DisplayName, wb.Cron, wb.Enabled)).ToList()
            ) : null,
            scenario.TechnicalEnvironment != null ? new TechnicalEnvironmentDto(
                scenario.TechnicalEnvironment.NetworkTopology,
                scenario.TechnicalEnvironment.Services,
                scenario.TechnicalEnvironment.Assets,
                JsonSerializer.Deserialize<List<string>>(scenario.TechnicalEnvironment.Defenses ?? "[]") ?? new List<string>(),
                scenario.TechnicalEnvironment.Vulnerabilities.Select(v => new VulnerabilityDto(v.Asset, v.Cve, v.Severity,
                    ScenarioExtras.Read<VulnerabilityExtrasDto>(v.Extras))).ToList()
            ) : null,
            scenario.GameMechanics != null ? new GameMechanicsDto(
                scenario.GameMechanics.TimelineType,
                scenario.GameMechanics.DurationHours,
                scenario.GameMechanics.AdjudicationType,
                scenario.GameMechanics.EscalationLadder,
                scenario.GameMechanics.BranchingLogic,
                new TelemetryDto(
                    scenario.GameMechanics.CollectLogs,
                    scenario.GameMechanics.CollectNetwork,
                    scenario.GameMechanics.CollectEndpoint,
                    scenario.GameMechanics.CollectChat
                ),
                scenario.GameMechanics.PerformanceMetrics
            ) : null,
            scenario.ScenarioTimeline != null ? new TimelineDto(
                scenario.ScenarioTimeline.ExerciseDuration,
                scenario.ScenarioTimeline.ScenarioTimelineEvents.Select(e => new TimelineEventDto(e.Time, e.Number, e.Assigned, e.Description, e.Status,
                    !string.IsNullOrEmpty(e.ObjectiveIds) ? JsonSerializer.Deserialize<List<int>>(e.ObjectiveIds) : new List<int>(),
                    e.TriggerKind.ToString(),
                    e.Schedule,
                    e.TriggerCondition,
                    e.ExecutionType.ToString().ToLowerInvariant(),
                    e.WorkflowId,
                    ScenarioExtras.Read<EventExtrasDto>(e.Extras)
                )).ToList()
            ) : null,
            scenario.BuilderStatus ?? "None",
            scenario.Author,
            scenario.PublishedAt,
            ScenarioExtras.Read<ScenarioExtrasDto>(scenario.Extras)
        );
    }

}
