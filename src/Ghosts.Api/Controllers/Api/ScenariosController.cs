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
    private readonly ILogger<ScenariosController> _logger;

    public ScenariosController(
        IScenarioService scenarioService,
        INpcService npcService,
        IScenarioDryRunService dryRun,
        IHttpClientFactory clients,
        ILogger<ScenariosController> logger)
    {
        _scenarioService = scenarioService;
        _npcService = npcService;
        _dryRun = dryRun;
        _clients = clients;
        _logger = logger;
    }

    // GET: api/scenarios
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ScenarioDto>>> GetScenarios(CancellationToken ct)
    {
        try
        {
            var scenarios = await _scenarioService.GetAllAsync(ct);
            return Ok(scenarios.Select(MapToDto));
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
            return Ok(MapToDto(scenario));
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
    /// a document returns that document; one built any other way returns a document derived from its
    /// rows. With ?derived=true it always returns the derived form, which is how the difference — what
    /// the columns cannot hold, reported by STORAGE_LOSSY — can be seen.
    /// </summary>
    // GET: api/scenarios/5/document
    [HttpGet("{id}/document")]
    [Produces("application/json")]
    public async Task<IActionResult> GetScenarioDocument(int id, [FromQuery] bool derived, CancellationToken ct)
    {
        try
        {
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
    /// Validates a scenario document and returns the findings. Writes nothing, ever: this is the
    /// endpoint an authoring agent calls, and it has to be safe to call on a draft. With
    /// ?dryRun=true it also loads and compiles the document inside a transaction that is always
    /// rolled back, so the findings include what loading it would actually do.
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
    /// response also carries a STORAGE_LOSSY finding for each top-level path the document populated
    /// that has no column — this describes what the import just did, so only import reports it;
    /// validate writes nothing and has nothing to describe. The document itself is kept whole beside
    /// the rows, with the findings of this run, so GET {id}/document returns what was imported rather
    /// than what the columns can rebuild.
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
            // Everything this import knows about the document goes into its validation record: what the
            // validator found, and what the columns could not hold.
            var lossy = StorageLossAnalyzer.Analyze(document);
            var scenario = await _scenarioService.ImportDocumentAsync(document, [.. result.Findings, .. lossy], ct);
            var body = ImportedBody(MapToDto(scenario), lossy);
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
            if (dto.Timeline?.Events != null)
            {
                foreach (var e in dto.Timeline.Events)
                    _logger.LogWarning("Event #{Num} objectiveIds={ObjIds}", e.Number, e.ObjectiveIds != null ? string.Join(",", e.ObjectiveIds) : "none");
            }
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
                    ta.Ttps?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>()
                )).ToList(),
                scenario.ScenarioParameters.Injects.Select(i => new InjectDto(i.Trigger, i.Title)).ToList(),
                scenario.ScenarioParameters.UserPools.Select(up => new UserPoolDto(up.Role, up.Count)).ToList(),
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
                scenario.TechnicalEnvironment.Vulnerabilities.Select(v => new VulnerabilityDto(v.Asset, v.Cve, v.Severity)).ToList()
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
                    e.WorkflowId
                )).ToList()
            ) : null,
            scenario.BuilderStatus ?? "None"
        );
    }

}
