// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NLog;

namespace Ghosts.Api.Controllers.Api
{
    [Route("api/scenarios/{scenarioId}/builder")]
    [ApiController]
    public class ScenarioBuilderController(
        IScenarioSourceService sourceService,
        IScenarioGraphService graphService,
        IScenarioExtractionService extractionService,
        IScenarioEnrichmentService enrichmentService,
        IScenarioCompilerService compilerService,
        ApplicationDbContext dbContext) : ControllerBase
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        // ──────────────────────────────────────────────
        // Sources
        // ──────────────────────────────────────────────

        [HttpGet("sources")]
        public async Task<ActionResult> GetSources(int scenarioId, CancellationToken ct)
        {
            try
            {
                var sources = await sourceService.GetByScenarioAsync(scenarioId, ct);
                var dtos = sources.ConvertAll(s => new ScenarioSourceDto(
                    s.Id, s.Name, s.SourceType, s.MimeType,
                    s.OriginalFileName, s.FileSizeBytes, s.Status,
                    s.ErrorMessage, s.CreatedAt, s.Chunks?.Count ?? 0,
                    s.SourceType == "Url" ? s.OriginalFileName : (s.Content?.Length > 100 ? s.Content[..100] + "…" : s.Content ?? "")));
                return Ok(dtos);
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"Error getting sources for scenario {scenarioId}");
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("sources/{id}")]
        public async Task<ActionResult> GetSource(int scenarioId, int id, CancellationToken ct)
        {
            try
            {
                var source = await sourceService.GetByIdAsync(id, ct);
                return Ok(new ScenarioSourceDto(
                    source.Id, source.Name, source.SourceType, source.MimeType,
                    source.OriginalFileName, source.FileSizeBytes, source.Status,
                    source.ErrorMessage, source.CreatedAt, source.Chunks?.Count ?? 0,
                    source.SourceType == "Url" ? source.OriginalFileName : (source.Content?.Length > 100 ? source.Content[..100] + "…" : source.Content ?? "")));
            }
            catch (InvalidOperationException)
            {
                return NotFound();
            }
        }

        [HttpPost("sources/text")]
        public async Task<ActionResult> AddTextSource(int scenarioId, [FromBody] CreateScenarioSourceTextDto dto, CancellationToken ct)
        {
            try
            {
                var source = await sourceService.AddTextAsync(scenarioId, dto, ct);
                return CreatedAtAction(nameof(GetSource), new { scenarioId, id = source.Id },
                    new ScenarioSourceDto(source.Id, source.Name, source.SourceType, source.MimeType,
                        source.OriginalFileName, source.FileSizeBytes, source.Status,
                        source.ErrorMessage, source.CreatedAt, source.Chunks?.Count ?? 0,
                        source.Content?.Length > 100 ? source.Content[..100] + "…" : source.Content ?? ""));
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error adding text source");
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("sources/url")]
        public async Task<ActionResult> AddUrlSource(int scenarioId, [FromBody] CreateScenarioSourceUrlDto dto, CancellationToken ct)
        {
            try
            {
                var source = await sourceService.AddUrlAsync(scenarioId, dto, ct);
                return CreatedAtAction(nameof(GetSource), new { scenarioId, id = source.Id },
                    new ScenarioSourceDto(source.Id, source.Name, source.SourceType, source.MimeType,
                        source.OriginalFileName, source.FileSizeBytes, source.Status,
                        source.ErrorMessage, source.CreatedAt, source.Chunks?.Count ?? 0,
                        source.OriginalFileName ?? ""));
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error adding URL source");
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("sources/file")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult> UploadFileSource(int scenarioId, IFormFile file, CancellationToken ct)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BadRequest("No file provided");

                using var ms = new MemoryStream();
                await file.CopyToAsync(ms, ct);
                var fileData = ms.ToArray();

                // Extract text content from file (plain text for now)
                string textContent;
                using (var reader = new StreamReader(file.OpenReadStream()))
                {
                    textContent = await reader.ReadToEndAsync(ct);
                }

                var source = await sourceService.UploadFileAsync(
                    scenarioId, file.FileName, file.ContentType, fileData, textContent, ct);

                return CreatedAtAction(nameof(GetSource), new { scenarioId, id = source.Id },
                    new ScenarioSourceDto(source.Id, source.Name, source.SourceType, source.MimeType,
                        source.OriginalFileName, source.FileSizeBytes, source.Status,
                        source.ErrorMessage, source.CreatedAt, source.Chunks?.Count ?? 0,
                        source.Content?.Length > 100 ? source.Content[..100] + "…" : source.Content ?? ""));
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error uploading file source");
                return StatusCode(500, ex.Message);
            }
        }

        [HttpDelete("sources/{id}")]
        public async Task<ActionResult> DeleteSource(int scenarioId, int id, CancellationToken ct)
        {
            try
            {
                await sourceService.DeleteAsync(id, ct);
                return NoContent();
            }
            catch (InvalidOperationException)
            {
                return NotFound();
            }
        }

        [HttpGet("sources/{sourceId}/chunks")]
        public async Task<ActionResult> GetChunks(int scenarioId, int sourceId, CancellationToken ct)
        {
            try
            {
                var chunks = await sourceService.GetChunksAsync(sourceId, ct);
                var dtos = chunks.ConvertAll(c => new ScenarioSourceChunkDto(
                    c.Id, c.SourceId, c.ChunkIndex, c.Content,
                    c.TokenCount, c.ExtractionStatus, c.CreatedAt));
                return Ok(dtos);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error getting chunks");
                return StatusCode(500, ex.Message);
            }
        }

        // ──────────────────────────────────────────────
        // Extraction
        // ──────────────────────────────────────────────

        [HttpPost("extract")]
        public async Task<ActionResult> ExtractAll(int scenarioId, CancellationToken ct)
        {
            try
            {
                var result = await extractionService.ExtractAllAsync(scenarioId, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"Error extracting scenario {scenarioId}");
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("extract/{chunkId}")]
        public async Task<ActionResult> ExtractChunk(int scenarioId, int chunkId, CancellationToken ct)
        {
            try
            {
                var result = await extractionService.ExtractChunkAsync(chunkId, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"Error extracting chunk {chunkId}");
                return StatusCode(500, ex.Message);
            }
        }

        // ──────────────────────────────────────────────
        // Graph - Entities
        // ──────────────────────────────────────────────

        [HttpGet("graph")]
        public async Task<ActionResult> GetGraph(int scenarioId, CancellationToken ct)
        {
            try
            {
                var graph = await graphService.GetGraphAsync(scenarioId, ct);
                return Ok(graph);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error getting graph");
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("graph/stats")]
        public async Task<ActionResult> GetGraphStats(int scenarioId, CancellationToken ct)
        {
            try
            {
                var stats = await graphService.GetGraphStatsAsync(scenarioId, ct);
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error getting graph stats");
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("entities")]
        public async Task<ActionResult> GetEntities(int scenarioId, [FromQuery] string type, CancellationToken ct)
        {
            try
            {
                var entities = await graphService.GetEntitiesAsync(scenarioId, type, ct);
                var dtos = entities.ConvertAll(e => new ScenarioEntityDto(
                    e.Id, e.Name, e.EntityType, e.Description, e.Properties,
                    e.Confidence, e.Origin, e.SourceId, e.NpcId, e.ExternalId,
                    e.IsReviewed, e.CreatedAt));
                return Ok(dtos);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error getting entities");
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("entities/{entityId}")]
        public async Task<ActionResult> GetEntity(int scenarioId, Guid entityId, CancellationToken ct)
        {
            try
            {
                var entity = await graphService.GetEntityAsync(entityId, ct);
                return Ok(new ScenarioEntityDto(
                    entity.Id, entity.Name, entity.EntityType, entity.Description,
                    entity.Properties, entity.Confidence, entity.Origin,
                    entity.SourceId, entity.NpcId, entity.ExternalId,
                    entity.IsReviewed, entity.CreatedAt));
            }
            catch (InvalidOperationException)
            {
                return NotFound();
            }
        }

        [HttpPost("entities")]
        public async Task<ActionResult> CreateEntity(int scenarioId, [FromBody] CreateScenarioEntityDto dto, CancellationToken ct)
        {
            try
            {
                var entity = await graphService.CreateEntityAsync(scenarioId, dto, ct);
                return CreatedAtAction(nameof(GetEntity), new { scenarioId, entityId = entity.Id },
                    new ScenarioEntityDto(entity.Id, entity.Name, entity.EntityType, entity.Description,
                        entity.Properties, entity.Confidence, entity.Origin,
                        entity.SourceId, entity.NpcId, entity.ExternalId,
                        entity.IsReviewed, entity.CreatedAt));
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error creating entity");
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPut("entities/{entityId}")]
        public async Task<ActionResult> UpdateEntity(int scenarioId, Guid entityId, [FromBody] UpdateScenarioEntityDto dto, CancellationToken ct)
        {
            try
            {
                var entity = await graphService.UpdateEntityAsync(entityId, dto, ct);
                return Ok(new ScenarioEntityDto(entity.Id, entity.Name, entity.EntityType, entity.Description,
                    entity.Properties, entity.Confidence, entity.Origin,
                    entity.SourceId, entity.NpcId, entity.ExternalId,
                    entity.IsReviewed, entity.CreatedAt));
            }
            catch (InvalidOperationException)
            {
                return NotFound();
            }
        }

        [HttpDelete("entities/{entityId}")]
        public async Task<ActionResult> DeleteEntity(int scenarioId, Guid entityId, CancellationToken ct)
        {
            try
            {
                await graphService.DeleteEntityAsync(entityId, ct);
                return NoContent();
            }
            catch (InvalidOperationException)
            {
                return NotFound();
            }
        }

        [HttpPost("entities/{entityId}/merge/{otherId}")]
        public async Task<ActionResult> MergeEntities(int scenarioId, Guid entityId, Guid otherId, CancellationToken ct)
        {
            try
            {
                var entity = await graphService.MergeEntitiesAsync(entityId, otherId, ct);
                return Ok(new ScenarioEntityDto(entity.Id, entity.Name, entity.EntityType, entity.Description,
                    entity.Properties, entity.Confidence, entity.Origin,
                    entity.SourceId, entity.NpcId, entity.ExternalId,
                    entity.IsReviewed, entity.CreatedAt));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // ──────────────────────────────────────────────
        // Graph - Edges
        // ──────────────────────────────────────────────

        [HttpGet("edges")]
        public async Task<ActionResult> GetEdges(int scenarioId, [FromQuery] string type, CancellationToken ct)
        {
            try
            {
                var edges = await graphService.GetEdgesAsync(scenarioId, type, ct);
                var dtos = edges.ConvertAll(e => new ScenarioEdgeDto(
                    e.Id, e.SourceEntityId, e.TargetEntityId,
                    e.EdgeType, e.Label, e.Weight,
                    e.Confidence, e.Origin, e.IsReviewed));
                return Ok(dtos);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error getting edges");
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("edges")]
        public async Task<ActionResult> CreateEdge(int scenarioId, [FromBody] CreateScenarioEdgeDto dto, CancellationToken ct)
        {
            try
            {
                var edge = await graphService.CreateEdgeAsync(scenarioId, dto, ct);
                return Ok(new ScenarioEdgeDto(edge.Id, edge.SourceEntityId, edge.TargetEntityId,
                    edge.EdgeType, edge.Label, edge.Weight,
                    edge.Confidence, edge.Origin, edge.IsReviewed));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("edges/{edgeId}")]
        public async Task<ActionResult> DeleteEdge(int scenarioId, Guid edgeId, CancellationToken ct)
        {
            try
            {
                await graphService.DeleteEdgeAsync(edgeId, ct);
                return NoContent();
            }
            catch (InvalidOperationException)
            {
                return NotFound();
            }
        }

        // ──────────────────────────────────────────────
        // Enrichments
        // ──────────────────────────────────────────────

        [HttpGet("enrichments")]
        public async Task<ActionResult> GetEnrichments(int scenarioId, CancellationToken ct)
        {
            try
            {
                var enrichments = await enrichmentService.GetEnrichmentsAsync(scenarioId, ct);
                var dtos = enrichments.ConvertAll(e => new ScenarioEnrichmentDto(
                    e.Id, e.EntityId, e.EnrichmentType, e.ExternalId,
                    e.Name, e.Description, e.Data, e.Source, e.CreatedAt));
                return Ok(dtos);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error getting enrichments");
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("enrichments/technique")]
        public async Task<ActionResult> ApplyTechnique(int scenarioId, [FromBody] ApplyAttackEnrichmentDto dto, CancellationToken ct)
        {
            try
            {
                var enrichment = await enrichmentService.ApplyTechniqueAsync(scenarioId, dto, ct);
                return Ok(new ScenarioEnrichmentDto(enrichment.Id, enrichment.EntityId,
                    enrichment.EnrichmentType, enrichment.ExternalId, enrichment.Name,
                    enrichment.Description, enrichment.Data, enrichment.Source, enrichment.CreatedAt));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("enrichments/group")]
        public async Task<ActionResult> ApplyGroup(int scenarioId, [FromBody] ApplyGroupEnrichmentDto dto, CancellationToken ct)
        {
            try
            {
                var enrichment = await enrichmentService.ApplyGroupAsync(scenarioId, dto, ct);
                return Ok(new ScenarioEnrichmentDto(enrichment.Id, enrichment.EntityId,
                    enrichment.EnrichmentType, enrichment.ExternalId, enrichment.Name,
                    enrichment.Description, enrichment.Data, enrichment.Source, enrichment.CreatedAt));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("enrichments/{enrichmentId}")]
        public async Task<ActionResult> DeleteEnrichment(int scenarioId, int enrichmentId, CancellationToken ct)
        {
            try
            {
                await enrichmentService.DeleteEnrichmentAsync(enrichmentId, ct);
                return NoContent();
            }
            catch (InvalidOperationException)
            {
                return NotFound();
            }
        }

        // ──────────────────────────────────────────────
        // Compilations
        // ──────────────────────────────────────────────

        [HttpPost("compile")]
        public async Task<ActionResult> Compile(int scenarioId, [FromBody] CompileScenarioDto dto, CancellationToken ct)
        {
            _log.Info($"=== COMPILE ENDPOINT HIT: ScenarioId={scenarioId}, Name={dto?.Name}, GenerateNpcs={dto?.GenerateNpcs} ===");

            try
            {
                if (dto == null)
                {
                    _log.Error("CompileScenarioDto is null");
                    return BadRequest("Request body is required");
                }

                _log.Info("Calling compilerService.CompileAsync");
                var compilation = await compilerService.CompileAsync(scenarioId, dto, ct);
                _log.Info($"Compilation completed successfully: ID={compilation.Id}, Status={compilation.Status}");

                return Ok(new ScenarioCompilationDto(compilation.Id, compilation.Name, compilation.Status,
                    compilation.NpcCount, compilation.TimelineEventCount, compilation.InjectCount,
                    compilation.CreatedAt, compilation.CompletedAt, compilation.ErrorMessage));
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"Error compiling scenario {scenarioId}: {ex.Message}");
                _log.Error($"Stack trace: {ex.StackTrace}");
                return StatusCode(500, new { message = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        [HttpGet("compilations")]
        public async Task<ActionResult> GetCompilations(int scenarioId, CancellationToken ct)
        {
            try
            {
                var compilations = await compilerService.GetCompilationsAsync(scenarioId, ct);
                var dtos = compilations.ConvertAll(c => new ScenarioCompilationDto(
                    c.Id, c.Name, c.Status, c.NpcCount, c.TimelineEventCount,
                    c.InjectCount, c.CreatedAt, c.CompletedAt, c.ErrorMessage));
                return Ok(dtos);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error getting compilations");
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("compilations/{compilationId}")]
        public async Task<ActionResult> GetCompilation(int scenarioId, int compilationId, CancellationToken ct)
        {
            try
            {
                var compilation = await compilerService.GetCompilationAsync(compilationId, ct);
                return Ok(new ScenarioCompilationDto(compilation.Id, compilation.Name, compilation.Status,
                    compilation.NpcCount, compilation.TimelineEventCount, compilation.InjectCount,
                    compilation.CreatedAt, compilation.CompletedAt, compilation.ErrorMessage));
            }
            catch (InvalidOperationException)
            {
                return NotFound();
            }
        }

        [HttpGet("compilations/{compilationId}/package")]
        public async Task<ActionResult> GetCompilationPackage(int scenarioId, int compilationId, CancellationToken ct)
        {
            try
            {
                var compilation = await compilerService.GetCompilationAsync(compilationId, ct);
                return Content(compilation.PackageData, "application/json");
            }
            catch (InvalidOperationException)
            {
                return NotFound();
            }
        }

        [HttpDelete("compilations/{compilationId}")]
        public async Task<ActionResult> DeleteCompilation(int scenarioId, int compilationId, CancellationToken ct)
        {
            try
            {
                await compilerService.DeleteCompilationAsync(compilationId, ct);
                return NoContent();
            }
            catch (InvalidOperationException)
            {
                return NotFound();
            }
        }

        // ──────────────────────────────────────────────
        // NPC-to-Machine Assignments
        // ──────────────────────────────────────────────

        /// <summary>
        /// List NPCs from a compilation alongside their current machine assignments.
        /// </summary>
        [HttpGet("compilations/{compilationId}/npcs")]
        public async Task<ActionResult> GetNpcsForAssignment(int scenarioId, int compilationId, CancellationToken ct)
        {
            try
            {
                // Verify compilation belongs to this scenario
                var compilation = await dbContext.ScenarioCompilations
                    .FirstOrDefaultAsync(c => c.Id == compilationId && c.ScenarioId == scenarioId, ct);
                if (compilation == null) return NotFound("Compilation not found");

                // Load compiled NPCs (Person entities that have an NpcId)
                var entities = await dbContext.ScenarioEntities
                    .Where(e => e.ScenarioId == scenarioId && e.EntityType == "Person" && e.NpcId != null)
                    .ToListAsync(ct);

                // Load existing assignments for this compilation
                var assignments = await dbContext.ScenarioNpcAssignments
                    .Where(a => a.CompilationId == compilationId)
                    .ToListAsync(ct);

                // Load machine names for assigned machines
                var machineIds = assignments.Select(a => a.MachineId).Distinct().ToList();
                var machines = await dbContext.Machines
                    .Where(m => machineIds.Contains(m.Id))
                    .Select(m => new { m.Id, m.Name, m.FQDN })
                    .ToListAsync(ct);

                var result = entities.Select(e =>
                {
                    var assignment = assignments.FirstOrDefault(a => a.NpcId == e.NpcId!.Value);
                    string machineName = null;
                    if (assignment != null)
                    {
                        var machine = machines.FirstOrDefault(m => m.Id == assignment.MachineId);
                        machineName = machine?.Name ?? machine?.FQDN ?? assignment.MachineId.ToString();
                    }

                    return new NpcForAssignmentDto(
                        e.NpcId!.Value,
                        e.Name,
                        e.Name,
                        assignment?.MachineId,
                        machineName,
                        assignment?.Id);
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"Error getting NPCs for compilation {compilationId}");
                return StatusCode(500, ex.Message);
            }
        }

        /// <summary>
        /// Assign a compiled NPC to a machine. Replaces any existing assignment for this NPC in this compilation.
        /// </summary>
        [HttpPost("compilations/{compilationId}/assignments")]
        public async Task<ActionResult> CreateAssignment(
            int scenarioId, int compilationId,
            [FromBody] CreateNpcAssignmentDto dto, CancellationToken ct)
        {
            try
            {
                var compilation = await dbContext.ScenarioCompilations
                    .FirstOrDefaultAsync(c => c.Id == compilationId && c.ScenarioId == scenarioId, ct);
                if (compilation == null) return NotFound("Compilation not found");

                // Verify the NPC exists and belongs to this scenario
                var entity = await dbContext.ScenarioEntities
                    .FirstOrDefaultAsync(e => e.ScenarioId == scenarioId && e.NpcId == dto.NpcId, ct);
                if (entity == null)
                    return BadRequest($"NPC {dto.NpcId} not found in scenario {scenarioId}");

                // Verify the machine exists
                var machine = await dbContext.Machines.FindAsync(dto.MachineId);
                if (machine == null)
                    return BadRequest($"Machine {dto.MachineId} not found");

                // Upsert: remove existing assignment for this NPC in this compilation
                var existing = await dbContext.ScenarioNpcAssignments
                    .FirstOrDefaultAsync(a => a.CompilationId == compilationId && a.NpcId == dto.NpcId, ct);
                if (existing != null)
                    dbContext.ScenarioNpcAssignments.Remove(existing);

                var assignment = new ScenarioNpcAssignment
                {
                    ScenarioId = scenarioId,
                    CompilationId = compilationId,
                    NpcId = dto.NpcId,
                    MachineId = dto.MachineId,
                    CreatedAt = DateTime.UtcNow
                };
                dbContext.ScenarioNpcAssignments.Add(assignment);
                await dbContext.SaveChangesAsync(ct);

                var machineName = machine.Name ?? machine.FQDN ?? dto.MachineId.ToString();
                return Ok(new NpcAssignmentDto(
                    assignment.Id, compilationId,
                    dto.NpcId, entity.Name,
                    dto.MachineId, machineName,
                    assignment.CreatedAt));
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"Error creating assignment for compilation {compilationId}");
                return StatusCode(500, ex.Message);
            }
        }

        /// <summary>
        /// Remove an NPC-to-machine assignment.
        /// </summary>
        [HttpDelete("compilations/{compilationId}/assignments/{assignmentId}")]
        public async Task<ActionResult> DeleteAssignment(
            int scenarioId, int compilationId, int assignmentId, CancellationToken ct)
        {
            try
            {
                var assignment = await dbContext.ScenarioNpcAssignments
                    .FirstOrDefaultAsync(a => a.Id == assignmentId && a.CompilationId == compilationId, ct);
                if (assignment == null) return NotFound();

                dbContext.ScenarioNpcAssignments.Remove(assignment);
                await dbContext.SaveChangesAsync(ct);
                return NoContent();
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"Error deleting assignment {assignmentId}");
                return StatusCode(500, ex.Message);
            }
        }

        /// <summary>
        /// Returns deployment readiness: how many compiled NPCs have machines assigned.
        /// </summary>
        [HttpGet("compilations/{compilationId}/readiness")]
        public async Task<ActionResult> GetDeploymentReadiness(int scenarioId, int compilationId, CancellationToken ct)
        {
            try
            {
                var compilation = await dbContext.ScenarioCompilations
                    .FirstOrDefaultAsync(c => c.Id == compilationId && c.ScenarioId == scenarioId, ct);
                if (compilation == null) return NotFound("Compilation not found");

                var totalNpcs = await dbContext.ScenarioEntities
                    .CountAsync(e => e.ScenarioId == scenarioId && e.EntityType == "Person" && e.NpcId != null, ct);

                var assignedNpcs = await dbContext.ScenarioNpcAssignments
                    .CountAsync(a => a.CompilationId == compilationId, ct);

                var issues = new System.Collections.Generic.List<string>();

                if (compilation.Status != "Completed")
                    issues.Add($"Compilation status is '{compilation.Status}' (must be 'Completed')");

                if (totalNpcs == 0)
                    issues.Add("No NPCs were generated during compilation");

                if (assignedNpcs < totalNpcs)
                    issues.Add($"{totalNpcs - assignedNpcs} NPC(s) have no machine assigned");

                return Ok(new DeploymentReadinessDto(
                    IsReady: issues.Count == 0,
                    TotalNpcs: totalNpcs,
                    AssignedNpcs: assignedNpcs,
                    UnassignedNpcs: totalNpcs - assignedNpcs,
                    Issues: issues));
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"Error getting readiness for compilation {compilationId}");
                return StatusCode(500, ex.Message);
            }
        }
    }
}
