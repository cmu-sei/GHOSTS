// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.ScenarioDocuments;
using Ghosts.Domain.Code;
using Microsoft.EntityFrameworkCore;
using NLog;

namespace Ghosts.Api.Infrastructure.Services
{
    public interface IScenarioService
    {
        Task<List<Scenario>> GetAllAsync(CancellationToken ct);
        Task<Scenario> GetByIdAsync(int id, CancellationToken ct);
        Task<bool> IsVisibleAsync(int id, string user, CancellationToken ct);
        Task<Scenario> CreateAsync(CreateScenarioDto dto, CancellationToken ct);
        Task<Scenario> ImportDocumentAsync(JsonObject document, IReadOnlyList<ScenarioFinding> findings, CancellationToken ct);
        Task<Scenario> ReplaceFromDocumentAsync(int id, JsonObject document, IReadOnlyList<ScenarioFinding> findings, CancellationToken ct);
        Task<bool> HasContentAsync(int id, CancellationToken ct);
        Task StoreDocumentAsync(int scenarioId, JsonObject document, IReadOnlyList<ScenarioFinding> findings, CancellationToken ct,
            string origin = ScenarioDocument.Imported);
        Task<bool> HasDocumentAsync(int scenarioId, CancellationToken ct);
        Task<string> ExportDocumentAsync(int id, bool derived, CancellationToken ct);
        Task<ApprovedScenarioDocument> ApprovedDocumentAsync(int id, CancellationToken ct);
        Task<Scenario> PublishAsync(int id, CancellationToken ct);
        Task<Scenario> UpdateAsync(int id, UpdateScenarioDto dto, CancellationToken ct);
        Task DeleteAsync(int id, CancellationToken ct);
    }

    /// <summary>
    /// The document a scenario was last imported from, and what an edit since has changed in its rows (A5).
    /// Changes is empty when nothing was edited.
    /// </summary>
    public record ApprovedScenarioDocument(int ScenarioId, DateTime ApprovedAt, string ContentHash, string Document,
        bool Edited, IReadOnlyList<string> Changes);

    public class ScenarioService(ApplicationDbContext context, CurrentUser user = null) : IScenarioService
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private readonly ApplicationDbContext _context = context;

        public async Task<List<Scenario>> GetAllAsync(CancellationToken ct)
        {
            return await _context.Scenarios
                .Include(s => s.ScenarioParameters)
                    .ThenInclude(sp => sp.Nations)
                .Include(s => s.ScenarioParameters)
                    .ThenInclude(sp => sp.ThreatActors)
                .Include(s => s.ScenarioParameters)
                    .ThenInclude(sp => sp.Injects)
                .Include(s => s.ScenarioParameters)
                    .ThenInclude(sp => sp.UserPools)
                .Include(s => s.ScenarioParameters)
                    .ThenInclude(sp => sp.WorkflowBindings)
                .Include(s => s.TechnicalEnvironment)
                    .ThenInclude(te => te.Vulnerabilities)
                .Include(s => s.GameMechanics)
                .Include(s => s.ScenarioTimeline)
                    .ThenInclude(t => t.ScenarioTimelineEvents)
                .OrderByDescending(s => s.UpdatedAt)
                .ToListAsync(ct);
        }

        public async Task<Scenario> GetByIdAsync(int id, CancellationToken ct)
        {
            var scenario = await _context.Scenarios
                .Include(s => s.ScenarioParameters)
                    .ThenInclude(sp => sp.Nations)
                .Include(s => s.ScenarioParameters)
                    .ThenInclude(sp => sp.ThreatActors)
                .Include(s => s.ScenarioParameters)
                    .ThenInclude(sp => sp.Injects)
                .Include(s => s.ScenarioParameters)
                    .ThenInclude(sp => sp.UserPools)
                .Include(s => s.ScenarioParameters)
                    .ThenInclude(sp => sp.WorkflowBindings)
                .Include(s => s.TechnicalEnvironment)
                    .ThenInclude(te => te.Vulnerabilities)
                .Include(s => s.GameMechanics)
                .Include(s => s.ScenarioTimeline)
                    .ThenInclude(t => t.ScenarioTimelineEvents)
                .FirstOrDefaultAsync(s => s.Id == id, ct);

            if (scenario == null)
            {
                _log.Error($"Scenario not found: {id}");
                throw new InvalidOperationException("Scenario not found");
            }

            return scenario;
        }

        /// <summary>Whether the scenario exists and is shown to this user: published, or their own draft (I2).</summary>
        public async Task<bool> IsVisibleAsync(int id, string user, CancellationToken ct) =>
            await _context.Scenarios.AnyAsync(s => s.Id == id && (s.PublishedAt != null || s.Author == user), ct);

        /// <summary>
        /// The scenario as canonical scenario-document text: the document it was imported from when
        /// there is one and the rows have not been edited since, and otherwise one derived from the
        /// rows. Pass derived to force the derived form even when a document is stored. What no column
        /// holds is in the rows' extras, so the two differ only by the schema README's known remainders.
        /// </summary>
        public async Task<string> ExportDocumentAsync(int id, bool derived, CancellationToken ct)
        {
            var rows = await DerivedDocumentAsync(id, ct);
            if (derived) return rows;

            // The stored document is current only while the rows still derive to what they did when
            // it was stored. Any edit since — PUT, the builder's graph, objectives — and the rows win.
            var stored = await CurrentDocumentAsync(id, ct);
            // Re-canonicalized rather than echoed: jsonb keeps the document but not its key order.
            return stored != null && stored.RowsHash == Hash(rows)
                ? ScenarioDocumentMapper.Serialize(JsonNode.Parse(stored.Document))
                : rows;
        }

        /// <summary>The document the rows alone derive to.</summary>
        private async Task<string> DerivedDocumentAsync(int id, CancellationToken ct)
        {
            // The graph and the objectives are read here because the scenario read path does not
            // include them. GetByIdAsync throws when the scenario does not exist, which is the 404.
            var scenario = await GetByIdAsync(id, ct);
            var entities = await _context.ScenarioEntities.Where(e => e.ScenarioId == id).ToListAsync(ct);
            var edges = await _context.ScenarioEdges.Where(e => e.ScenarioId == id).ToListAsync(ct);
            var objectives = await _context.Objectives.Where(o => o.ScenarioId == id).ToListAsync(ct);

            return ScenarioDocumentMapper.Serialize(
                ScenarioDocumentMapper.ToDocument(scenario, entities, edges, objectives));
        }

        /// <summary>
        /// Creates a scenario from a document and keeps the document beside the rows, both in one
        /// transaction: CreateAsync sees this transaction as ambient and leaves the commit here, so a
        /// scenario imported from a document never exists without it.
        /// </summary>
        public async Task<Scenario> ImportDocumentAsync(
            JsonObject document, IReadOnlyList<ScenarioFinding> findings, CancellationToken ct)
        {
            var ambient = _context.Database.CurrentTransaction;
            await using var transaction = ambient == null ? await _context.Database.BeginTransactionAsync(ct) : null;

            var scenario = await CreateAsync(ScenarioDocumentMapper.FromDocument(document), ct);
            // The rows hash is taken from what the database holds, not the entities just written:
            // decimal(5,4) rounds and jsonb reorders, and every later export reads the database.
            _context.ChangeTracker.Clear();
            await StoreDocumentAsync(scenario.Id, document, findings, ct);

            if (transaction != null) await transaction.CommitAsync(ct);

            return scenario;
        }

        /// <summary>
        /// Replaces what a scenario holds with a document, and keeps the scenario: its id, sources, document
        /// history, runs, NPC records and draft or published state. The Scenario Builder imports into the
        /// scenario it is open on this way. Old compilations and their NPC assignments go too, or a run would
        /// deploy them beside the new timeline. One transaction, as ImportDocumentAsync.
        /// </summary>
        public async Task<Scenario> ReplaceFromDocumentAsync(
            int id, JsonObject document, IReadOnlyList<ScenarioFinding> findings, CancellationToken ct)
        {
            var ambient = _context.Database.CurrentTransaction;
            await using var transaction = ambient == null ? await _context.Database.BeginTransactionAsync(ct) : null;

            var scenario = await GetByIdAsync(id, ct);

            // Edges first: an edge's target entity is a restricting key.
            _context.ScenarioEdges.RemoveRange(await _context.ScenarioEdges.Where(e => e.ScenarioId == id).ToListAsync(ct));
            await _context.SaveChangesAsync(ct);
            _context.ScenarioEntities.RemoveRange(await _context.ScenarioEntities.Where(e => e.ScenarioId == id).ToListAsync(ct));
            _context.ScenarioEnrichments.RemoveRange(await _context.ScenarioEnrichments.Where(e => e.ScenarioId == id).ToListAsync(ct));
            _context.Objectives.RemoveRange(await _context.Objectives.Where(o => o.ScenarioId == id).ToListAsync(ct));
            _context.ScenarioNpcAssignments.RemoveRange(await _context.ScenarioNpcAssignments.Where(a => a.ScenarioId == id).ToListAsync(ct));
            _context.ScenarioCompilations.RemoveRange(await _context.ScenarioCompilations.Where(c => c.ScenarioId == id).ToListAsync(ct));
            if (scenario.ScenarioParameters != null) _context.Remove(scenario.ScenarioParameters);
            if (scenario.TechnicalEnvironment != null) _context.Remove(scenario.TechnicalEnvironment);
            if (scenario.GameMechanics != null) _context.Remove(scenario.GameMechanics);
            if (scenario.ScenarioTimeline != null) _context.Remove(scenario.ScenarioTimeline);
            await _context.SaveChangesAsync(ct);

            scenario.ScenarioParameters = null;
            scenario.TechnicalEnvironment = null;
            scenario.GameMechanics = null;
            scenario.ScenarioTimeline = null;

            var dto = ScenarioDocumentMapper.FromDocument(document);
            scenario.Name = dto.Name;
            scenario.Description = dto.Description;
            scenario.Extras = ScenarioExtras.Write(dto.Extras);
            scenario.UpdatedAt = DateTime.UtcNow;
            MapChildren(dto, scenario);
            // Entities and edges get their keys when they are mapped. Found through the tracked scenario,
            // a keyed entity would be taken for an existing row, so they are added explicitly.
            _context.AddRange(scenario.Entities);
            _context.AddRange(scenario.Edges);
            await _context.SaveChangesAsync(ct);

            if (scenario.Objectives.Count > 0)
            {
                RemapObjectiveIds(dto.Objectives, scenario);
                await _context.SaveChangesAsync(ct);
            }

            _context.ChangeTracker.Clear();
            await StoreDocumentAsync(id, document, findings, ct);

            if (transaction != null) await transaction.CommitAsync(ct);

            _log.Info($"Replaced scenario {id} from a document: {scenario.Name}");
            return await GetByIdAsync(id, ct);
        }

        /// <summary>
        /// Whether a scenario holds anything an import would replace: events, actors, injects, pools, objectives
        /// or a graph. Entities the Sources step extracted don't count on their own: extraction runs as soon as a
        /// source is added, before any draft exists, so a scenario that has never had a document would otherwise
        /// always look occupied. Once a document (import or compile) has been stored, every entity counts again.
        /// </summary>
        public async Task<bool> HasContentAsync(int id, CancellationToken ct)
        {
            var scenario = await GetByIdAsync(id, ct);
            var parameters = scenario.ScenarioParameters;
            if (scenario.ScenarioTimeline?.ScenarioTimelineEvents.Count > 0) return true;
            if (parameters != null && parameters.Nations.Count + parameters.ThreatActors.Count + parameters.Injects.Count + parameters.UserPools.Count > 0) return true;
            if (await _context.Objectives.AnyAsync(o => o.ScenarioId == id, ct)) return true;

            var everHadDocument = await HasDocumentAsync(id, ct);
            return await _context.ScenarioEntities.AnyAsync(
                e => e.ScenarioId == id && (everHadDocument || e.Origin != "Extracted"), ct);
        }

        /// <summary>
        /// The newest document someone imported, and the change list from what its rows derived to then to
        /// what they derive to now. Null when the scenario has never had a document imported.
        /// </summary>
        public async Task<ApprovedScenarioDocument> ApprovedDocumentAsync(int id, CancellationToken ct)
        {
            var rows = await DerivedDocumentAsync(id, ct);
            var approved = await _context.ScenarioDocuments.AsNoTracking()
                .Where(d => d.ScenarioId == id && d.Origin == ScenarioDocument.Imported)
                .OrderByDescending(d => d.Id)
                .FirstOrDefaultAsync(ct);
            if (approved == null) return null;

            var edited = approved.RowsHash != Hash(rows);
            var changes = edited && approved.RowsDocument != null
                ? ScenarioDocumentDiff.Changes(JsonNode.Parse(approved.RowsDocument), JsonNode.Parse(rows))
                : [];
            return new ApprovedScenarioDocument(id, approved.CreatedAt, approved.ContentHash,
                ScenarioDocumentMapper.Serialize(JsonNode.Parse(approved.Document)), edited, changes);
        }

        /// <summary>Makes a draft visible to everyone and deployable. The caller checks who may, and the document.</summary>
        public async Task<Scenario> PublishAsync(int id, CancellationToken ct)
        {
            var scenario = await _context.Scenarios.FirstOrDefaultAsync(s => s.Id == id, ct)
                           ?? throw new InvalidOperationException("Scenario not found");
            scenario.PublishedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
            _log.Info($"Published scenario {id}");
            return await GetByIdAsync(id, ct);
        }

        /// <summary>
        /// Adds a document row for a scenario. One row per import, never an update: the newest row is
        /// the current document and the older ones are the scenario's history.
        /// </summary>
        public async Task StoreDocumentAsync(
            int scenarioId, JsonObject document, IReadOnlyList<ScenarioFinding> findings, CancellationToken ct,
            string origin = ScenarioDocument.Imported)
        {
            var text = ScenarioDocumentMapper.Serialize(document);
            var rows = await DerivedDocumentAsync(scenarioId, ct);

            _context.ScenarioDocuments.Add(new ScenarioDocument
            {
                ScenarioId = scenarioId,
                SchemaVersion = (document["schemaVersion"] as JsonValue)?.GetValue<string>()
                                ?? ScenarioDocumentMapper.SchemaVersion,
                ContentHash = Hash(text),
                RowsHash = Hash(rows),
                Document = text,
                Validation = ValidationRecord(findings),
                Origin = origin,
                RowsDocument = rows,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync(ct);
            _log.Info($"Stored scenario document: scenario {scenarioId}, {findings.Count} finding(s)");
        }

        public async Task<bool> HasDocumentAsync(int scenarioId, CancellationToken ct) =>
            await _context.ScenarioDocuments.AnyAsync(d => d.ScenarioId == scenarioId, ct);

        /// <summary>The newest document row, or null when the scenario has none.</summary>
        private async Task<ScenarioDocument> CurrentDocumentAsync(int id, CancellationToken ct) =>
            await _context.ScenarioDocuments
                .AsNoTracking()
                .Where(d => d.ScenarioId == id)
                .OrderByDescending(d => d.Id)
                .FirstOrDefaultAsync(ct);

        /// <summary>SHA-256 of the text, lower-case hex.</summary>
        private static string Hash(string text) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

        /// <summary>
        /// What the validator said about this document, stored with it: a document is only as good as
        /// the run that accepted it, and a warning that was acceptable in one import is a fact about
        /// that import.
        /// </summary>
        private static string ValidationRecord(IReadOnlyList<ScenarioFinding> findings)
        {
            var list = new JsonArray();
            foreach (var f in findings)
            {
                list.Add(new JsonObject
                {
                    ["tier"] = f.Tier,
                    ["severity"] = f.Severity,
                    ["code"] = f.Code,
                    ["path"] = f.Path,
                    ["message"] = f.Message
                });
            }

            return new JsonObject
            {
                ["validator"] = ApplicationDetails.Version,
                ["validatedAt"] = DateTime.UtcNow.ToString("o"),
                ["errors"] = findings.Count(f => f.Severity == ScenarioFinding.Error),
                ["warnings"] = findings.Count(f => f.Severity == ScenarioFinding.Warning),
                ["findings"] = list
            }.ToJsonString();
        }

        public async Task<Scenario> CreateAsync(CreateScenarioDto dto, CancellationToken ct)
        {
            var scenario = new Scenario
            {
                Name = dto.Name,
                Description = dto.Description,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                // Every new scenario starts as its author's draft
                Author = user?.Name ?? CurrentUser.Anonymous,
                Extras = ScenarioExtras.Write(dto.Extras)
            };

            MapChildren(dto, scenario);

            _context.Scenarios.Add(scenario);

            // One transaction: an objective's database id is only known after the first save, and
            // the parent and event references that point at it are rewritten in the second. A caller
            // that already opened one owns it — that is how the validator's dry run creates a
            // scenario and then rolls the whole thing back.
            var ambient = _context.Database.CurrentTransaction;
            await using var transaction = ambient == null ? await _context.Database.BeginTransactionAsync(ct) : null;

            var operation = await _context.SaveChangesAsync(ct);
            if (operation < 1)
            {
                _log.Error($"Could not create scenario: {operation}");
                throw new InvalidOperationException("Could not create Scenario");
            }

            if (scenario.Objectives.Count > 0)
            {
                RemapObjectiveIds(dto.Objectives, scenario);
                await _context.SaveChangesAsync(ct);
            }

            if (transaction != null) await transaction.CommitAsync(ct);

            _log.Info($"Created scenario: {scenario.Id} - {scenario.Name}");

            // Reload with all relationships
            return await GetByIdAsync(scenario.Id, ct);
        }

        /// <summary>A scenario's parts from a create DTO: what CreateAsync makes, and what ReplaceFromDocumentAsync remakes.</summary>
        private static void MapChildren(CreateScenarioDto dto, Scenario scenario)
        {
            if (dto.ScenarioParameters != null)
            {
                scenario.ScenarioParameters = MapScenarioParameters(dto.ScenarioParameters);
            }

            if (dto.TechnicalEnvironment != null)
            {
                scenario.TechnicalEnvironment = MapTechnicalEnvironment(dto.TechnicalEnvironment);
            }

            if (dto.GameMechanics != null)
            {
                scenario.GameMechanics = MapGameMechanics(dto.GameMechanics);
            }

            if (dto.Timeline != null)
            {
                scenario.ScenarioTimeline = MapTimeline(dto.Timeline);
            }

            if (dto.Objectives != null)
            {
                scenario.Objectives = MapObjectives(dto.Objectives);
            }

            if (dto.Entities != null)
            {
                MapGraph(dto, scenario);
            }
        }

        public async Task<Scenario> UpdateAsync(int id, UpdateScenarioDto dto, CancellationToken ct)
        {
            var scenario = await _context.Scenarios
                .Include(s => s.ScenarioParameters)
                    .ThenInclude(sp => sp.Nations)
                .Include(s => s.ScenarioParameters)
                    .ThenInclude(sp => sp.ThreatActors)
                .Include(s => s.ScenarioParameters)
                    .ThenInclude(sp => sp.Injects)
                .Include(s => s.ScenarioParameters)
                    .ThenInclude(sp => sp.UserPools)
                .Include(s => s.ScenarioParameters)
                    .ThenInclude(sp => sp.WorkflowBindings)
                .Include(s => s.TechnicalEnvironment)
                    .ThenInclude(te => te.Vulnerabilities)
                .Include(s => s.GameMechanics)
                .Include(s => s.ScenarioTimeline)
                    .ThenInclude(t => t.ScenarioTimelineEvents)
                .FirstOrDefaultAsync(s => s.Id == id, ct);

            if (scenario == null)
            {
                _log.Error($"Scenario not found: {id}");
                throw new InvalidOperationException("Scenario not found");
            }

            scenario.Name = dto.Name;
            scenario.Description = dto.Description;
            scenario.UpdatedAt = DateTime.UtcNow;
            if (dto.BuilderStatus != null)
                scenario.BuilderStatus = dto.BuilderStatus;
            // Only when supplied, like workflow bindings, so a caller that predates extras does not wipe them.
            if (dto.Extras != null)
                scenario.Extras = ScenarioExtras.Write(dto.Extras);

            // Update ScenarioParameters
            if (dto.ScenarioParameters != null)
            {
                if (scenario.ScenarioParameters != null)
                {
                    var current = scenario.ScenarioParameters;
                    current.Objectives = dto.ScenarioParameters.Objectives;
                    current.PoliticalContext = dto.ScenarioParameters.PoliticalContext;
                    current.RulesOfEngagement = dto.ScenarioParameters.RulesOfEngagement;
                    current.VictoryConditions = dto.ScenarioParameters.VictoryConditions;

                    // Each list is replaced only when the caller sends it, as workflow bindings always were, and an
                    // item sent without extras keeps the extras of the row it replaces, matched by name. So a caller
                    // that does not know a list, or extras, cannot wipe what the document said (A5).
                    if (dto.ScenarioParameters.Nations != null)
                    {
                        _context.Nations.RemoveRange(current.Nations);
                        current.Nations = dto.ScenarioParameters.Nations.Select(n => new Nation
                        {
                            Name = n.Name,
                            Alignment = n.Alignment
                        }).ToList();
                    }

                    if (dto.ScenarioParameters.ThreatActors != null)
                    {
                        var before = current.ThreatActors;
                        _context.ThreatActors.RemoveRange(before);
                        current.ThreatActors = dto.ScenarioParameters.ThreatActors.Select(ta => new ThreatActor
                        {
                            Name = ta.Name,
                            Type = ta.Type,
                            Capability = ta.Capability,
                            Ttps = string.Join(",", ta.Ttps),
                            Extras = Keep(ta.Extras, before.FirstOrDefault(x => x.Name == ta.Name)?.Extras)
                        }).ToList();
                    }

                    if (dto.ScenarioParameters.Injects != null)
                    {
                        var before = current.Injects;
                        _context.Injects.RemoveRange(before);
                        current.Injects = dto.ScenarioParameters.Injects.Select(i => new Inject
                        {
                            Trigger = i.Trigger,
                            Title = i.Title,
                            Extras = Keep(i.Extras, before.FirstOrDefault(x => x.Title == i.Title)?.Extras)
                        }).ToList();
                    }

                    if (dto.ScenarioParameters.UserPools != null)
                    {
                        var before = current.UserPools;
                        _context.UserPools.RemoveRange(before);
                        current.UserPools = dto.ScenarioParameters.UserPools.Select(up => new UserPool
                        {
                            Role = up.Role,
                            Count = up.Count,
                            Extras = Keep(up.Extras, before.FirstOrDefault(x => x.Role == up.Role)?.Extras)
                        }).ToList();
                    }

                    if (dto.ScenarioParameters.WorkflowBindings != null)
                    {
                        _context.ScenarioWorkflowBindings.RemoveRange(current.WorkflowBindings);
                        current.WorkflowBindings = MapWorkflowBindings(dto.ScenarioParameters.WorkflowBindings);
                    }
                }
                else
                {
                    scenario.ScenarioParameters = MapScenarioParameters(dto.ScenarioParameters);
                }
            }

            // Update TechnicalEnvironment
            if (dto.TechnicalEnvironment != null)
            {
                if (scenario.TechnicalEnvironment != null)
                {
                    scenario.TechnicalEnvironment.NetworkTopology = dto.TechnicalEnvironment.NetworkTopology;
                    scenario.TechnicalEnvironment.Services = dto.TechnicalEnvironment.Services;
                    scenario.TechnicalEnvironment.Assets = dto.TechnicalEnvironment.Assets;
                    scenario.TechnicalEnvironment.Defenses = System.Text.Json.JsonSerializer.Serialize(dto.TechnicalEnvironment.Defenses);

                    if (dto.TechnicalEnvironment.Vulnerabilities != null)
                    {
                        var before = scenario.TechnicalEnvironment.Vulnerabilities;
                        _context.Vulnerabilities.RemoveRange(before);
                        scenario.TechnicalEnvironment.Vulnerabilities = dto.TechnicalEnvironment.Vulnerabilities.Select(v => new Vulnerability
                        {
                            Asset = v.Asset,
                            Cve = v.Cve,
                            Severity = v.Severity,
                            Extras = Keep(v.Extras, before.FirstOrDefault(x => x.Asset == v.Asset && x.Cve == v.Cve)?.Extras)
                        }).ToList();
                    }
                }
                else
                {
                    scenario.TechnicalEnvironment = MapTechnicalEnvironment(dto.TechnicalEnvironment);
                }
            }

            // Update GameMechanics
            if (dto.GameMechanics != null)
            {
                if (scenario.GameMechanics != null)
                {
                    scenario.GameMechanics.TimelineType = dto.GameMechanics.TimelineType;
                    scenario.GameMechanics.DurationHours = dto.GameMechanics.DurationHours;
                    scenario.GameMechanics.AdjudicationType = dto.GameMechanics.AdjudicationType;
                    scenario.GameMechanics.EscalationLadder = dto.GameMechanics.EscalationLadder;
                    scenario.GameMechanics.BranchingLogic = dto.GameMechanics.BranchingLogic;
                    scenario.GameMechanics.CollectLogs = dto.GameMechanics.Telemetry.CollectLogs;
                    scenario.GameMechanics.CollectNetwork = dto.GameMechanics.Telemetry.CollectNetwork;
                    scenario.GameMechanics.CollectEndpoint = dto.GameMechanics.Telemetry.CollectEndpoint;
                    scenario.GameMechanics.CollectChat = dto.GameMechanics.Telemetry.CollectChat;
                    scenario.GameMechanics.PerformanceMetrics = dto.GameMechanics.PerformanceMetrics;
                }
                else
                {
                    scenario.GameMechanics = MapGameMechanics(dto.GameMechanics);
                }
            }

            // Update Timeline
            if (dto.Timeline != null)
            {
                if (scenario.ScenarioTimeline != null && dto.Timeline.Events != null)
                {
                    var before = scenario.ScenarioTimeline.ScenarioTimelineEvents;
                    _context.ScenarioTimelineEvents.RemoveRange(before);

                    scenario.ScenarioTimeline.ExerciseDuration = dto.Timeline.ExerciseDuration;
                    scenario.ScenarioTimeline.ScenarioTimelineEvents = dto.Timeline.Events.Select(e => new ScenarioTimelineEvent
                    {
                        Time = e.Time,
                        Number = e.Number,
                        Assigned = e.Assigned,
                        Description = e.Description,
                        Status = e.Status,
                        ObjectiveIds = e.ObjectiveIds?.Count > 0 ? JsonSerializer.Serialize(e.ObjectiveIds) : null,
                        TriggerKind = Enum.TryParse<TriggerKind>(e.TriggerKind, true, out var tk) ? tk : TriggerKind.PointInTime,
                        Schedule = e.Schedule,
                        TriggerCondition = e.TriggerCondition,
                        ExecutionType = Enum.TryParse<ExecutionType>(e.ExecutionType, true, out var et) ? et : ExecutionType.Manual,
                        WorkflowId = e.WorkflowId,
                        Extras = Keep(e.Extras, before.FirstOrDefault(x => x.Number == e.Number)?.Extras)
                    }).ToList();
                }
                else if (scenario.ScenarioTimeline != null)
                {
                    scenario.ScenarioTimeline.ExerciseDuration = dto.Timeline.ExerciseDuration;
                }
                else
                {
                    scenario.ScenarioTimeline = MapTimeline(dto.Timeline);
                }
            }

            await _context.SaveChangesAsync(ct);

            _log.Info($"Updated scenario: {scenario.Id} - {scenario.Name}");

            return scenario;
        }

        /// <summary>The extras an updated row gets: what the caller sent, or what the row it replaces held.</summary>
        private static string Keep<T>(T sent, string replaced) where T : class =>
            sent != null ? ScenarioExtras.Write(sent) : replaced;

        public async Task DeleteAsync(int id, CancellationToken ct)
        {
            var scenario = await _context.Scenarios.FindAsync(id);
            if (scenario == null)
            {
                _log.Error($"Scenario not found: {id}");
                throw new InvalidOperationException("Scenario not found");
            }

            _context.Scenarios.Remove(scenario);

            var operation = await _context.SaveChangesAsync(ct);
            if (operation < 1)
            {
                _log.Error($"Could not delete scenario: {operation}");
                throw new InvalidOperationException("Could not delete Scenario");
            }

            _log.Info($"Deleted scenario: {id}");
        }

        // Helper mapping methods
        private static ScenarioParameters MapScenarioParameters(ScenarioParametersDto dto)
        {
            return new ScenarioParameters
            {
                Objectives = dto.Objectives,
                PoliticalContext = dto.PoliticalContext,
                RulesOfEngagement = dto.RulesOfEngagement,
                VictoryConditions = dto.VictoryConditions,
                Nations = dto.Nations.Select(n => new Nation { Name = n.Name, Alignment = n.Alignment }).ToList(),
                ThreatActors = dto.ThreatActors.Select(ta => new ThreatActor
                {
                    Name = ta.Name,
                    Type = ta.Type,
                    Capability = ta.Capability,
                    Ttps = string.Join(",", ta.Ttps),
                    Extras = ScenarioExtras.Write(ta.Extras)
                }).ToList(),
                Injects = dto.Injects.Select(i => new Inject { Trigger = i.Trigger, Title = i.Title, Extras = ScenarioExtras.Write(i.Extras) }).ToList(),
                UserPools = dto.UserPools.Select(up => new UserPool { Role = up.Role, Count = up.Count, Extras = ScenarioExtras.Write(up.Extras) }).ToList(),
                WorkflowBindings = MapWorkflowBindings(dto.WorkflowBindings)
            };
        }

        /// <summary>
        /// Maps binding DTOs to entities, or seeds the default animation workflow set when none
        /// are supplied — so a new scenario's runs come alive out of the box while staying overridable.
        /// </summary>
        private static List<ScenarioWorkflowBinding> MapWorkflowBindings(List<ScenarioWorkflowBindingDto> dtos)
        {
            if (dtos == null)
            {
                return ScenarioWorkflowBinding.Defaults()
                    .Select(d => new ScenarioWorkflowBinding
                    {
                        WorkflowRef = d.WorkflowRef,
                        DisplayName = d.DisplayName,
                        Cron = d.Cron,
                        Enabled = d.Enabled
                    }).ToList();
            }

            return dtos.Select(b => new ScenarioWorkflowBinding
            {
                WorkflowRef = b.WorkflowRef,
                DisplayName = b.DisplayName,
                Cron = b.Cron,
                Enabled = b.Enabled
            }).ToList();
        }

        private static TechnicalEnvironment MapTechnicalEnvironment(TechnicalEnvironmentDto dto)
        {
            return new TechnicalEnvironment
            {
                NetworkTopology = dto.NetworkTopology,
                Services = dto.Services,
                Assets = dto.Assets,
                Defenses = System.Text.Json.JsonSerializer.Serialize(dto.Defenses),
                Vulnerabilities = dto.Vulnerabilities.Select(v => new Vulnerability
                {
                    Asset = v.Asset,
                    Cve = v.Cve,
                    Severity = v.Severity,
                    Extras = ScenarioExtras.Write(v.Extras)
                }).ToList()
            };
        }

        private static GameMechanics MapGameMechanics(GameMechanicsDto dto)
        {
            return new GameMechanics
            {
                TimelineType = dto.TimelineType,
                DurationHours = dto.DurationHours,
                AdjudicationType = dto.AdjudicationType,
                EscalationLadder = dto.EscalationLadder,
                BranchingLogic = dto.BranchingLogic,
                CollectLogs = dto.Telemetry.CollectLogs,
                CollectNetwork = dto.Telemetry.CollectNetwork,
                CollectEndpoint = dto.Telemetry.CollectEndpoint,
                CollectChat = dto.Telemetry.CollectChat,
                PerformanceMetrics = dto.PerformanceMetrics
            };
        }

        private static List<Objective> MapObjectives(List<ScenarioObjectiveImportDto> dtos)
        {
            var now = DateTime.UtcNow;
            return dtos.Select(o => new Objective
            {
                Name = o.Name,
                Description = o.Description,
                Type = string.IsNullOrEmpty(o.Type) ? "MET" : o.Type,
                Priority = o.Priority > 0 ? o.Priority : 1,
                SuccessCriteria = o.SuccessCriteria,
                Assigned = o.Assigned,
                SortOrder = o.SortOrder,
                Extras = ScenarioExtras.Write(o.Extras),
                CreatedAt = now,
                UpdatedAt = now
            }).ToList();
        }

        /// <summary>
        /// Entities and edges created with the scenario. Edges name their endpoints by the caller's
        /// local entity id, resolved here to the GUIDs assigned to the new entities. CreatedAt is
        /// stepped so the tables keep the order they were given in, which is all they can express.
        /// </summary>
        private static void MapGraph(CreateScenarioDto dto, Scenario scenario)
        {
            var now = DateTime.UtcNow;
            var byLocalId = new Dictionary<string, ScenarioEntity>();

            scenario.Entities = dto.Entities.Select((e, i) =>
            {
                var entity = new ScenarioEntity
                {
                    Name = e.Name,
                    EntityType = string.IsNullOrEmpty(e.Type) ? "Custom" : e.Type,
                    Description = e.Description,
                    Properties = string.IsNullOrEmpty(e.Properties) ? "{}" : e.Properties,
                    Confidence = e.Confidence,
                    Origin = string.IsNullOrEmpty(e.Origin) ? "Operator" : e.Origin,
                    ExternalId = e.ExternalId,
                    IsReviewed = e.IsReviewed,
                    CreatedAt = now.AddMilliseconds(i),
                    UpdatedAt = now.AddMilliseconds(i)
                };
                if (!string.IsNullOrEmpty(e.Id)) byLocalId[e.Id] = entity;
                return entity;
            }).ToList();

            if (dto.Edges == null) return;

            var edges = new List<ScenarioEdge>();
            foreach (var (e, i) in dto.Edges.Select((e, i) => (e, i)))
            {
                if (!byLocalId.TryGetValue(e.From ?? string.Empty, out var source) ||
                    !byLocalId.TryGetValue(e.To ?? string.Empty, out var target))
                {
                    _log.Warn($"Edge {e.From} -> {e.To} skipped: unknown entity");
                    continue;
                }

                edges.Add(new ScenarioEdge
                {
                    SourceEntityId = source.Id,
                    TargetEntityId = target.Id,
                    EdgeType = string.IsNullOrEmpty(e.Type) ? "Custom" : e.Type,
                    Label = e.Label,
                    Weight = e.Weight,
                    Confidence = e.Confidence,
                    Origin = string.IsNullOrEmpty(e.Origin) ? "Operator" : e.Origin,
                    Properties = string.IsNullOrEmpty(e.Properties) ? "{}" : e.Properties,
                    IsReviewed = e.IsReviewed,
                    CreatedAt = now.AddMilliseconds(i)
                });
            }
            scenario.Edges = edges;
        }

        /// <summary>
        /// Rewrites the caller's local objective ids — in objective parents and in the timeline
        /// events that refer to them — to the database ids assigned by the first save.
        /// </summary>
        private static void RemapObjectiveIds(List<ScenarioObjectiveImportDto> dtos, Scenario scenario)
        {
            var objectives = scenario.Objectives.ToList();
            var ids = new Dictionary<int, int>();
            for (var i = 0; i < objectives.Count; i++) ids[dtos[i].Id] = objectives[i].Id;

            for (var i = 0; i < objectives.Count; i++)
            {
                if (dtos[i].ParentId is int parent && ids.TryGetValue(parent, out var parentId))
                {
                    objectives[i].ParentId = parentId;
                }
            }

            foreach (var e in scenario.ScenarioTimeline?.ScenarioTimelineEvents ?? [])
            {
                if (string.IsNullOrEmpty(e.ObjectiveIds)) continue;
                var mapped = JsonSerializer.Deserialize<List<int>>(e.ObjectiveIds)
                    .Where(ids.ContainsKey).Select(id => ids[id]).ToList();
                e.ObjectiveIds = mapped.Count > 0 ? JsonSerializer.Serialize(mapped) : null;
            }
        }

        private static ScenarioTimeline MapTimeline(TimelineDto dto)
        {
            return new ScenarioTimeline
            {
                ExerciseDuration = dto.ExerciseDuration,
                ScenarioTimelineEvents = dto.Events.Select(e => new ScenarioTimelineEvent
                {
                    Time = e.Time,
                    Number = e.Number,
                    Assigned = e.Assigned,
                    Description = e.Description,
                    Status = e.Status,
                    ObjectiveIds = e.ObjectiveIds?.Count > 0 ? JsonSerializer.Serialize(e.ObjectiveIds) : null,
                    TriggerKind = Enum.TryParse<TriggerKind>(e.TriggerKind, true, out var tk) ? tk : TriggerKind.PointInTime,
                    Schedule = e.Schedule,
                    TriggerCondition = e.TriggerCondition,
                    ExecutionType = Enum.TryParse<ExecutionType>(e.ExecutionType, true, out var et) ? et : ExecutionType.Manual,
                    WorkflowId = e.WorkflowId,
                    Extras = ScenarioExtras.Write(e.Extras)
                }).ToList()
            };
        }
    }
}
