// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Hubs;
using Ghosts.Api.Infrastructure.ContentServices;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Api.Infrastructure.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NLog;

namespace Ghosts.Api.Infrastructure.Services
{
    public interface IScenarioExtractionService
    {
        Task<ExtractionResultDto> ExtractAllAsync(int scenarioId, CancellationToken ct);
        Task<ExtractionResultDto> ExtractChunkAsync(int chunkId, CancellationToken ct);
    }

    public class ScenarioExtractionService(
        ApplicationDbContext context,
        IConfiguration configuration,
        IHubContext<ScenarioBuilderHub> hubContext) : IScenarioExtractionService
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private readonly ApplicationDbContext _context = context;
        private readonly IConfiguration _configuration = configuration;
        private readonly IHubContext<ScenarioBuilderHub> _hubContext = hubContext;

        // ── Public API ────────────────────────────────────────────────────────

        public async Task<ExtractionResultDto> ExtractAllAsync(int scenarioId, CancellationToken ct)
        {
            _log.Info($"Starting extraction for all chunks in scenario {scenarioId}");

            var scenario = await _context.Scenarios
                .Include(s => s.Sources)
                    .ThenInclude(source => source.Chunks)
                .FirstOrDefaultAsync(s => s.Id == scenarioId, ct);

            if (scenario == null)
                throw new InvalidOperationException("Scenario not found");

            var pendingChunks = scenario.Sources
                .SelectMany(s => s.Chunks)
                .Where(c => c.ExtractionStatus == "Pending")
                .OrderBy(c => c.SourceId)
                .ThenBy(c => c.ChunkIndex)
                .ToList();

            _log.Info($"Found {pendingChunks.Count} pending chunks to extract");

            if (pendingChunks.Count == 0)
                return new ExtractionResultDto(0, 0, 0, new List<string>());

            await SendProgressNotification(scenarioId, "started", 0, pendingChunks.Count, 0, 0);

            // ── Phase 1: parallel LLM calls ───────────────────────────────────
            // Concurrency default = 2 (safe for local Ollama; raise for cloud APIs)
            var concurrency = int.TryParse(_configuration["ScenarioBuilder:ExtractionConcurrency"], out var c) && c > 0 ? c : 2;
            var semaphore = new SemaphoreSlim(concurrency);

            var llmTasks = pendingChunks.Select(chunk => Task.Run(async () =>
            {
                await semaphore.WaitAsync(ct);
                try
                {
                    _log.Debug($"LLM call starting for chunk {chunk.Id}");
                    var prompt = await BuildExtractionPromptAsync(chunk, ct);
                    var llmResponse = await CallLlmAsync(prompt, ct);
                    var result = ParseExtractionResponse(llmResponse, out var parseErrors);
                    _log.Debug($"LLM call completed for chunk {chunk.Id}: {result.Entities?.Count ?? 0} entities, {result.Edges?.Count ?? 0} edges");
                    return (chunk, result, parseErrors, failed: false);
                }
                catch (Exception ex)
                {
                    _log.Error(ex, $"LLM call failed for chunk {chunk.Id}");
                    return (chunk, new ExtractionResponse(), new List<string> { ex.Message }, failed: true);
                }
                finally
                {
                    semaphore.Release();
                }
            }, ct)).ToList();

            var llmResults = await Task.WhenAll(llmTasks);

            // ── Phase 2: sequential batch writes ─────────────────────────────
            // Load all existing entities once (not per-chunk)
            var existingEntities = await _context.ScenarioEntities
                .Where(e => e.ScenarioId == scenarioId)
                .ToDictionaryAsync(e => e.Name.ToLower(), e => e, ct);

            // entityIdMap: entity name (lower) → Guid — used for edge resolution
            var entityIdMap = existingEntities.ToDictionary(
                kvp => kvp.Key, kvp => kvp.Value.Id, StringComparer.OrdinalIgnoreCase);

            // Load existing edges into a hash set for fast dedup
            var existingEdgeKeys = await _context.ScenarioEdges
                .Where(e => e.ScenarioId == scenarioId)
                .Select(e => new { e.SourceEntityId, e.TargetEntityId, e.EdgeType })
                .ToListAsync(ct);
            var existingEdgeSet = new HashSet<(Guid, Guid, string)>(
                existingEdgeKeys.Select(e => (e.SourceEntityId, e.TargetEntityId, e.EdgeType)));

            var allErrors = new List<string>();
            var totalEntitiesCreated = 0;
            var chunkEdgeWork = new List<(ScenarioSourceChunk chunk, List<ExtractedEdge> edges)>();

            foreach (var (chunk, result, errors, failed) in llmResults)
            {
                allErrors.AddRange(errors);

                if (failed)
                {
                    chunk.ExtractionStatus = "Failed";
                    continue;
                }

                // Upsert entities into the in-memory dict + EF tracking
                foreach (var entity in result.Entities ?? new List<ExtractedEntity>())
                {
                    if (string.IsNullOrWhiteSpace(entity.Name)) continue;
                    var nameLower = entity.Name.ToLower().Trim();

                    if (existingEntities.TryGetValue(nameLower, out var existing))
                    {
                        // Merge type if new
                        if (!string.IsNullOrEmpty(entity.Type) &&
                            !existing.EntityType.Contains(entity.Type, StringComparison.OrdinalIgnoreCase))
                        {
                            var merged = existing.EntityType
                                .Split('|', StringSplitOptions.RemoveEmptyEntries)
                                .Concat(entity.Type.Split('|', StringSplitOptions.RemoveEmptyEntries))
                                .Distinct(StringComparer.OrdinalIgnoreCase);
                            existing.EntityType = string.Join("|", merged);
                            existing.UpdatedAt = DateTime.UtcNow;
                        }
                        // Raise confidence if better
                        if (entity.Confidence > existing.Confidence)
                        {
                            existing.Confidence = entity.Confidence;
                            existing.Description = entity.Description ?? existing.Description;
                            existing.UpdatedAt = DateTime.UtcNow;
                        }
                    }
                    else
                    {
                        var newEntity = new ScenarioEntity
                        {
                            ScenarioId = scenarioId,
                            Name = entity.Name.Trim(),
                            EntityType = entity.Type,
                            Description = entity.Description ?? string.Empty,
                            Confidence = entity.Confidence,
                            Origin = "Extracted",
                            SourceId = chunk.Source.Id,
                            SourceChunkId = chunk.Id,
                            IsReviewed = false,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        _context.ScenarioEntities.Add(newEntity);
                        // Register in dict so subsequent chunks see this entity
                        existingEntities[nameLower] = newEntity;
                        entityIdMap[nameLower] = newEntity.Id; // EF sets Id on Add
                        totalEntitiesCreated++;
                    }
                }

                chunk.ExtractionStatus = "Completed";
                chunkEdgeWork.Add((chunk, result.Edges ?? new List<ExtractedEdge>()));
            }

            // One save for all entity upserts and chunk status updates
            await _context.SaveChangesAsync(ct);

            // Rebuild map now that all Guids are stable
            entityIdMap = existingEntities.ToDictionary(
                kvp => kvp.Key, kvp => kvp.Value.Id, StringComparer.OrdinalIgnoreCase);

            // Process edges
            var totalEdgesCreated = 0;
            foreach (var (chunk, edges) in chunkEdgeWork)
            {
                foreach (var edge in edges)
                {
                    var sourceId = ResolveEntityId(edge.Source, entityIdMap);
                    var targetId = ResolveEntityId(edge.Target, entityIdMap);

                    if (sourceId == Guid.Empty || targetId == Guid.Empty) continue;

                    var key = (sourceId, targetId, edge.Type);
                    if (!existingEdgeSet.Add(key)) continue; // already exists or duplicate in batch

                    _context.ScenarioEdges.Add(new ScenarioEdge
                    {
                        ScenarioId = scenarioId,
                        SourceEntityId = sourceId,
                        TargetEntityId = targetId,
                        EdgeType = edge.Type,
                        Label = edge.Label ?? edge.Type,
                        Confidence = edge.Confidence,
                        Origin = "Extracted",
                        SourceId = chunk.Source.Id,
                        SourceChunkId = chunk.Id,
                        IsReviewed = false,
                        CreatedAt = DateTime.UtcNow
                    });
                    totalEdgesCreated++;
                }
            }

            // One save for all edges
            await _context.SaveChangesAsync(ct);

            // Update scenario builder status
            var allDone = await _context.ScenarioSourceChunks
                .Where(c => c.ScenarioId == scenarioId)
                .AllAsync(c => c.ExtractionStatus == "Completed" || c.ExtractionStatus == "Failed", ct);

            if (allDone)
            {
                scenario.BuilderStatus = "Extracted";
                await _context.SaveChangesAsync(ct);
            }

            _log.Info($"Extraction completed: {totalEntitiesCreated} entities, {totalEdgesCreated} edges, {pendingChunks.Count} chunks processed");

            await SendProgressNotification(scenarioId, "completed", pendingChunks.Count, pendingChunks.Count, totalEntitiesCreated, totalEdgesCreated);

            return new ExtractionResultDto(totalEntitiesCreated, totalEdgesCreated, pendingChunks.Count, allErrors);
        }

        public async Task<ExtractionResultDto> ExtractChunkAsync(int chunkId, CancellationToken ct)
        {
            _log.Info($"Starting extraction for chunk {chunkId}");

            var chunk = await _context.ScenarioSourceChunks
                .Include(c => c.Source)
                .FirstOrDefaultAsync(c => c.Id == chunkId, ct);

            if (chunk == null)
                throw new InvalidOperationException("Chunk not found");

            chunk.ExtractionStatus = "Processing";
            await _context.SaveChangesAsync(ct);

            var entitiesCreated = 0;
            var edgesCreated = 0;
            var errors = new List<string>();

            try
            {
                var prompt = await BuildExtractionPromptAsync(chunk, ct);
                var llmResponse = await CallLlmAsync(prompt, ct);

                if (string.IsNullOrWhiteSpace(llmResponse))
                    throw new InvalidOperationException("LLM returned empty response");

                var extractionResult = ParseExtractionResponse(llmResponse, out var parseErrors);
                errors.AddRange(parseErrors);

                // Load all existing entities once for this scenario
                var existingEntities = await _context.ScenarioEntities
                    .Where(e => e.ScenarioId == chunk.ScenarioId)
                    .ToDictionaryAsync(e => e.Name.ToLower(), e => e, ct);

                var entityIdMap = existingEntities.ToDictionary(
                    kvp => kvp.Key, kvp => kvp.Value.Id, StringComparer.OrdinalIgnoreCase);

                // Upsert all entities, batch into context
                foreach (var entity in extractionResult.Entities ?? new List<ExtractedEntity>())
                {
                    if (string.IsNullOrWhiteSpace(entity.Name)) continue;
                    var nameLower = entity.Name.ToLower().Trim();

                    if (existingEntities.TryGetValue(nameLower, out var existing))
                    {
                        if (!string.IsNullOrEmpty(entity.Type) &&
                            !existing.EntityType.Contains(entity.Type, StringComparison.OrdinalIgnoreCase))
                        {
                            var merged = existing.EntityType
                                .Split('|', StringSplitOptions.RemoveEmptyEntries)
                                .Concat(entity.Type.Split('|', StringSplitOptions.RemoveEmptyEntries))
                                .Distinct(StringComparer.OrdinalIgnoreCase);
                            existing.EntityType = string.Join("|", merged);
                            existing.UpdatedAt = DateTime.UtcNow;
                        }
                        if (entity.Confidence > existing.Confidence)
                        {
                            existing.Confidence = entity.Confidence;
                            existing.Description = entity.Description ?? existing.Description;
                            existing.UpdatedAt = DateTime.UtcNow;
                        }
                    }
                    else
                    {
                        var newEntity = new ScenarioEntity
                        {
                            ScenarioId = chunk.ScenarioId,
                            Name = entity.Name.Trim(),
                            EntityType = entity.Type,
                            Description = entity.Description ?? string.Empty,
                            Confidence = entity.Confidence,
                            Origin = "Extracted",
                            SourceId = chunk.Source.Id,
                            SourceChunkId = chunkId,
                            IsReviewed = false,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        _context.ScenarioEntities.Add(newEntity);
                        existingEntities[nameLower] = newEntity;
                        entitiesCreated++;
                    }
                }

                // One save for all entities
                await _context.SaveChangesAsync(ct);

                // Rebuild map with stable IDs
                entityIdMap = existingEntities.ToDictionary(
                    kvp => kvp.Key, kvp => kvp.Value.Id, StringComparer.OrdinalIgnoreCase);

                // Load existing edges for dedup
                var existingEdgeKeys = await _context.ScenarioEdges
                    .Where(e => e.ScenarioId == chunk.ScenarioId)
                    .Select(e => new { e.SourceEntityId, e.TargetEntityId, e.EdgeType })
                    .ToListAsync(ct);
                var existingEdgeSet = new HashSet<(Guid, Guid, string)>(
                    existingEdgeKeys.Select(e => (e.SourceEntityId, e.TargetEntityId, e.EdgeType)));

                foreach (var edge in extractionResult.Edges ?? new List<ExtractedEdge>())
                {
                    var sourceId = ResolveEntityId(edge.Source, entityIdMap);
                    var targetId = ResolveEntityId(edge.Target, entityIdMap);

                    if (sourceId == Guid.Empty || targetId == Guid.Empty) continue;

                    var key = (sourceId, targetId, edge.Type);
                    if (!existingEdgeSet.Add(key)) continue;

                    _context.ScenarioEdges.Add(new ScenarioEdge
                    {
                        ScenarioId = chunk.ScenarioId,
                        SourceEntityId = sourceId,
                        TargetEntityId = targetId,
                        EdgeType = edge.Type,
                        Label = edge.Label ?? edge.Type,
                        Confidence = edge.Confidence,
                        Origin = "Extracted",
                        SourceId = chunk.Source.Id,
                        SourceChunkId = chunkId,
                        IsReviewed = false,
                        CreatedAt = DateTime.UtcNow
                    });
                    edgesCreated++;
                }

                chunk.ExtractionStatus = "Completed";

                // One save for all edges + status
                await _context.SaveChangesAsync(ct);

                _log.Info($"Chunk {chunkId} extraction completed: {entitiesCreated} entities, {edgesCreated} edges");
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"Failed to extract chunk {chunkId}");
                chunk.ExtractionStatus = "Failed";
                await _context.SaveChangesAsync(ct);
                errors.Add(ex.Message);
            }

            return new ExtractionResultDto(entitiesCreated, edgesCreated, 1, errors);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private Guid ResolveEntityId(string name, Dictionary<string, Guid> entityIdMap)
        {
            if (string.IsNullOrWhiteSpace(name)) return Guid.Empty;

            if (entityIdMap.TryGetValue(name, out var id)) return id;
            if (entityIdMap.TryGetValue(name.ToLower(), out id)) return id;

            var best = FindBestEntityNameMatch(name, entityIdMap.Keys);
            if (!string.IsNullOrEmpty(best))
            {
                entityIdMap.TryGetValue(best, out id);
                _log.Debug($"Fuzzy matched '{name}' to '{best}'");
                return id;
            }

            return Guid.Empty;
        }

        private async Task<string> BuildExtractionPromptAsync(ScenarioSourceChunk chunk, CancellationToken ct)
        {
            var templatePath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "config", "ContentServices", "ScenarioBuilder", "ExtractEntities.txt");

            if (!File.Exists(templatePath))
            {
                _log.Warn($"Extraction template not found at {templatePath}, using default prompt");
                return BuildDefaultPrompt(chunk.Content);
            }

            var template = await File.ReadAllTextAsync(templatePath, ct);
            return template.Replace("[[chunk_content]]", chunk.Content);
        }

        private static string BuildDefaultPrompt(string content)
        {
            return $@"Extract entities and relationships from the following text. Return a JSON object with ""entities"" and ""edges"" arrays.

Entity format:
{{
  ""name"": ""Entity Name"",
  ""type"": ""Person|Organization|System|Network|Location|Software|ThreatActor|Campaign|Vulnerability|DataAsset|Service|Custom"",
  ""description"": ""Brief description"",
  ""confidence"": 0.0-1.0
}}

Edge format:
{{
  ""source"": ""Source Entity Name"",
  ""target"": ""Target Entity Name"",
  ""type"": ""MemberOf|Targets|Exploits|Uses|LocatedAt|CommunicatesWith|DependsOn|Accesses|Owns|ReportsTo|AffiliatedWith|DefendedBy|CommandsAndControls|Custom"",
  ""label"": ""Relationship description"",
  ""confidence"": 0.0-1.0
}}

Text to analyze:
{content}

Return only valid JSON.";
        }

        private async Task<string> CallLlmAsync(string prompt, CancellationToken ct)
        {
            _log.Debug("Calling LLM for extraction");

            try
            {
                var contentEngineSettings = new ApplicationSettings.AnimatorSettingsDetail.ContentEngineSettings
                {
                    Source = _configuration["ScenarioBuilder:ContentEngine:Source"] ?? "ollama",
                    Model = _configuration["ScenarioBuilder:ContentEngine:Model"] ?? "llama3.1",
                    Host = _configuration["ScenarioBuilder:ContentEngine:Host"] ?? "http://localhost:11434"
                };

                var contentService = new ContentCreationService(contentEngineSettings);

                if (contentService.FormatterService == null)
                    throw new InvalidOperationException("Content service formatter is not available");

                var result = await contentService.FormatterService.ExecuteQuery(prompt);
                _log.Debug($"LLM returned {result?.Length ?? 0} characters");
                return result;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Failed to call LLM");
                throw new InvalidOperationException("Failed to call LLM for extraction", ex);
            }
        }

        private ExtractionResponse ParseExtractionResponse(string llmResponse, out List<string> errors)
        {
            errors = new List<string>();

            try
            {
                var jsonStart = llmResponse.IndexOf('{');
                var jsonEnd = llmResponse.LastIndexOf('}');

                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    var jsonContent = llmResponse.Substring(jsonStart, jsonEnd - jsonStart + 1);

                    if (jsonContent.Length < 5000)
                        _log.Debug($"Parsing JSON response: {jsonContent.Substring(0, Math.Min(500, jsonContent.Length))}...");

                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        AllowTrailingCommas = true,
                        ReadCommentHandling = JsonCommentHandling.Skip,
                        Converters = { new LenientExtractionResponseConverter() }
                    };

                    var result = JsonSerializer.Deserialize<ExtractionResponse>(jsonContent, options);

                    if (result == null)
                    {
                        errors.Add("Failed to deserialize JSON to ExtractionResponse");
                        return new ExtractionResponse();
                    }

                    if (result.Edges != null)
                    {
                        result.Edges = result.Edges
                            .Where(e => !string.IsNullOrWhiteSpace(e.Source) && !string.IsNullOrWhiteSpace(e.Target))
                            .ToList();
                    }

                    _log.Info($"Parsed {result.Entities?.Count ?? 0} entities and {result.Edges?.Count ?? 0} edges from LLM response");
                    return result;
                }

                _log.Warn("No JSON object found in LLM response");
                errors.Add("No JSON object found in LLM response");
                return new ExtractionResponse();
            }
            catch (JsonException ex)
            {
                _log.Warn(ex, "Failed to parse LLM response as JSON");
                errors.Add($"JSON parsing error: {ex.Message}");
                if (llmResponse.Length < 1000)
                    _log.Debug($"Failed JSON content: {llmResponse}");
                return new ExtractionResponse();
            }
        }

        private async Task SendProgressNotification(
            int scenarioId, string status,
            int chunksProcessed, int totalChunks,
            int entitiesCreated, int edgesCreated)
        {
            try
            {
                var connections = ScenarioBuilderHub.GetConnections();
                var progressData = new
                {
                    scenarioId, status, chunksProcessed, totalChunks,
                    entitiesCreated, edgesCreated, timestamp = DateTime.UtcNow
                };

                var targets = connections.GetConnections(scenarioId.ToString())
                    .Concat(connections.GetConnections("all"));

                foreach (var connectionId in targets)
                    await _hubContext.Clients.Client(connectionId).SendAsync("extractionProgress", progressData);
            }
            catch (Exception ex)
            {
                _log.Warn(ex, "Failed to send progress notification");
            }
        }

        private static string FindBestEntityNameMatch(string searchName, IEnumerable<string> entityNames)
        {
            if (string.IsNullOrWhiteSpace(searchName)) return string.Empty;

            var searchLower = searchName.ToLower().Trim();
            var entityList = entityNames.ToList();

            var exact = entityList.FirstOrDefault(e => e.Equals(searchName, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;

            var contains = entityList.FirstOrDefault(e =>
                e.Contains(searchName, StringComparison.OrdinalIgnoreCase) ||
                searchName.Contains(e, StringComparison.OrdinalIgnoreCase));
            if (contains != null) return contains;

            var searchWords = searchLower
                .Split(new[] { ' ', '-', '_', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2).ToList();

            if (searchWords.Any())
            {
                var bestMatch = entityList
                    .Select(e => new
                    {
                        Name = e,
                        Words = e.ToLower().Split(new[] { ' ', '-', '_', ',' }, StringSplitOptions.RemoveEmptyEntries)
                                 .Where(w => w.Length > 2).ToList()
                    })
                    .Select(e => new
                    {
                        e.Name,
                        Score = searchWords.Count(sw => e.Words.Contains(sw)) +
                                e.Words.Count(ew => searchWords.Contains(ew))
                    })
                    .Where(x => x.Score > 0)
                    .OrderByDescending(x => x.Score)
                    .FirstOrDefault();

                if (bestMatch != null && bestMatch.Score >= searchWords.Count / 2)
                    return bestMatch.Name;
            }

            return string.Empty;
        }

        // ── Internal DTOs ────────────────────────────────────────────────────

        private class ExtractionResponse
        {
            public List<ExtractedEntity> Entities { get; set; } = new List<ExtractedEntity>();
            public List<ExtractedEdge> Edges { get; set; } = new List<ExtractedEdge>();
        }

        private class ExtractedEntity
        {
            public string Name { get; set; } = string.Empty;
            public string Type { get; set; } = "Custom";
            public string Description { get; set; }
            public decimal Confidence { get; set; } = 0.8m;
        }

        private class ExtractedEdge
        {
            private string _source = string.Empty;
            private string _target = string.Empty;

            public string Source
            {
                get => _source;
                set => _source = value?.ToString() ?? string.Empty;
            }

            public string Target
            {
                get => _target;
                set => _target = value?.ToString() ?? string.Empty;
            }

            public string Type { get; set; } = "Custom";
            public string Label { get; set; }
            public decimal Confidence { get; set; } = 0.8m;
        }

        private class LenientExtractionResponseConverter : JsonConverter<ExtractionResponse>
        {
            public override ExtractionResponse Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                var result = new ExtractionResponse
                {
                    Entities = new List<ExtractedEntity>(),
                    Edges = new List<ExtractedEdge>()
                };

                if (reader.TokenType != JsonTokenType.StartObject)
                    return result;

                using var doc = JsonDocument.ParseValue(ref reader);
                var root = doc.RootElement;

                if (root.TryGetProperty("entities", out var entitiesElement) && entitiesElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in entitiesElement.EnumerateArray())
                    {
                        try
                        {
                            var entity = new ExtractedEntity();
                            if (el.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String)
                                entity.Name = nameEl.GetString() ?? string.Empty;
                            if (el.TryGetProperty("type", out var typeEl) && typeEl.ValueKind == JsonValueKind.String)
                                entity.Type = typeEl.GetString() ?? "Custom";
                            if (el.TryGetProperty("description", out var descEl) && descEl.ValueKind == JsonValueKind.String)
                                entity.Description = descEl.GetString();
                            if (el.TryGetProperty("confidence", out var confEl) && confEl.ValueKind == JsonValueKind.Number)
                                entity.Confidence = confEl.GetDecimal();

                            if (!string.IsNullOrWhiteSpace(entity.Name))
                                result.Entities.Add(entity);
                        }
                        catch { /* skip malformed */ }
                    }
                }

                if (root.TryGetProperty("edges", out var edgesElement) && edgesElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in edgesElement.EnumerateArray())
                    {
                        try
                        {
                            var edge = new ExtractedEdge();
                            if (el.TryGetProperty("source", out var sourceEl)) edge.Source = ExtractStringValue(sourceEl);
                            if (el.TryGetProperty("target", out var targetEl)) edge.Target = ExtractStringValue(targetEl);
                            if (el.TryGetProperty("type", out var typeEl) && typeEl.ValueKind == JsonValueKind.String)
                                edge.Type = typeEl.GetString() ?? "Custom";
                            if (el.TryGetProperty("label", out var labelEl) && labelEl.ValueKind == JsonValueKind.String)
                                edge.Label = labelEl.GetString();
                            if (el.TryGetProperty("confidence", out var confEl) && confEl.ValueKind == JsonValueKind.Number)
                                edge.Confidence = confEl.GetDecimal();

                            if (!string.IsNullOrWhiteSpace(edge.Source) && !string.IsNullOrWhiteSpace(edge.Target))
                                result.Edges.Add(edge);
                        }
                        catch { /* skip malformed */ }
                    }
                }

                return result;
            }

            private static string ExtractStringValue(JsonElement element) => element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? string.Empty,
                JsonValueKind.Object =>
                    element.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() ?? string.Empty :
                    element.TryGetProperty("id", out var i) && i.ValueKind == JsonValueKind.String ? i.GetString() ?? string.Empty :
                    element.GetRawText(),
                JsonValueKind.Array =>
                    element.EnumerateArray().FirstOrDefault() is { ValueKind: JsonValueKind.String } first
                        ? first.GetString() ?? string.Empty : string.Empty,
                JsonValueKind.Number => element.ToString(),
                _ => string.Empty
            };

            public override void Write(Utf8JsonWriter writer, ExtractionResponse value, JsonSerializerOptions options)
                => JsonSerializer.Serialize(writer, value, options);
        }
    }
}
