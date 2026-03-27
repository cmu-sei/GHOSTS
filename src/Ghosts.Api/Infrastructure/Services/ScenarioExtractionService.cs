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

        public async Task<ExtractionResultDto> ExtractAllAsync(int scenarioId, CancellationToken ct)
        {
            _log.Info($"Starting extraction for all chunks in scenario {scenarioId}");

            var scenario = await _context.Scenarios
                .Include(s => s.Sources)
                    .ThenInclude(source => source.Chunks)
                .FirstOrDefaultAsync(s => s.Id == scenarioId, ct);

            if (scenario == null)
            {
                _log.Error($"Scenario not found: {scenarioId}");
                throw new InvalidOperationException("Scenario not found");
            }

            var pendingChunks = scenario.Sources
                .SelectMany(s => s.Chunks)
                .Where(c => c.ExtractionStatus == "Pending")
                .OrderBy(c => c.SourceId)
                .ThenBy(c => c.ChunkIndex)
                .ToList();

            _log.Info($"Found {pendingChunks.Count} pending chunks to extract");

            // Send initial progress notification
            await SendProgressNotification(scenarioId, "started", 0, pendingChunks.Count, 0, 0);

            var totalEntitiesCreated = 0;
            var totalEdgesCreated = 0;
            var totalChunksProcessed = 0;
            var allErrors = new List<string>();

            foreach (var chunk in pendingChunks)
            {
                try
                {
                    var result = await ExtractChunkAsync(chunk.Id, ct);
                    totalEntitiesCreated += result.EntitiesCreated;
                    totalEdgesCreated += result.EdgesCreated;
                    totalChunksProcessed += result.ChunksProcessed;
                    allErrors.AddRange(result.Errors);

                    // Send progress update after each chunk
                    await SendProgressNotification(
                        scenarioId,
                        "processing",
                        totalChunksProcessed,
                        pendingChunks.Count,
                        totalEntitiesCreated,
                        totalEdgesCreated);
                }
                catch (Exception ex)
                {
                    _log.Error(ex, $"Failed to extract chunk {chunk.Id}");
                    allErrors.Add($"Chunk {chunk.Id}: {ex.Message}");
                }
            }

            // Check if all chunks are completed
            var allChunksCompleted = await _context.ScenarioSourceChunks
                .Where(c => c.ScenarioId == scenarioId)
                .AllAsync(c => c.ExtractionStatus == "Completed" || c.ExtractionStatus == "Failed", ct);

            if (allChunksCompleted)
            {
                scenario.BuilderStatus = "Extracted";
                await _context.SaveChangesAsync(ct);
                _log.Info($"All chunks extracted for scenario {scenarioId}, updated status to Extracted");
            }

            _log.Info($"Extraction completed: {totalEntitiesCreated} entities, {totalEdgesCreated} edges, {totalChunksProcessed} chunks processed");

            // Send completion notification
            await SendProgressNotification(
                scenarioId,
                "completed",
                totalChunksProcessed,
                pendingChunks.Count,
                totalEntitiesCreated,
                totalEdgesCreated);

            return new ExtractionResultDto(
                totalEntitiesCreated,
                totalEdgesCreated,
                totalChunksProcessed,
                allErrors);
        }

        public async Task<ExtractionResultDto> ExtractChunkAsync(int chunkId, CancellationToken ct)
        {
            _log.Info($"Starting extraction for chunk {chunkId}");

            var chunk = await _context.ScenarioSourceChunks
                .Include(c => c.Source)
                .FirstOrDefaultAsync(c => c.Id == chunkId, ct);

            if (chunk == null)
            {
                _log.Error($"Chunk not found: {chunkId}");
                throw new InvalidOperationException("Chunk not found");
            }

            chunk.ExtractionStatus = "Processing";
            await _context.SaveChangesAsync(ct);

            var entitiesCreated = 0;
            var edgesCreated = 0;
            var errors = new List<string>();

            try
            {
                // Build extraction prompt
                var prompt = await BuildExtractionPromptAsync(chunk, ct);

                // Call LLM
                var llmResponse = await CallLlmAsync(prompt, ct);

                if (string.IsNullOrWhiteSpace(llmResponse))
                {
                    throw new InvalidOperationException("LLM returned empty response");
                }

                // Parse JSON response
                var extractionResult = ParseExtractionResponse(llmResponse, out var parseErrors);
                errors.AddRange(parseErrors);

                // Store entities - build map from ALL existing entities first to prevent duplicates
                var existingEntities = await _context.ScenarioEntities
                    .Where(e => e.ScenarioId == chunk.ScenarioId)
                    .ToDictionaryAsync(e => e.Name.ToLower(), e => e.Id, ct);

                var entityIdMap = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in existingEntities)
                {
                    entityIdMap[kvp.Key] = kvp.Value;
                }

                foreach (var entity in extractionResult.Entities ?? new List<ExtractedEntity>())
                {
                    try
                    {
                        var storedEntity = await StoreEntityAsync(chunk.ScenarioId, chunk.Source.Id, chunkId, entity, ct);
                        entityIdMap[entity.Name] = storedEntity.Id;
                        if (storedEntity.CreatedAt >= DateTime.UtcNow.AddSeconds(-5))
                        {
                            // Only count as "created" if it's newly created (not updated)
                            entitiesCreated++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.Warn(ex, $"Failed to store entity: {entity.Name}");
                        errors.Add($"Entity {entity.Name}: {ex.Message}");
                    }
                }

                // Store edges with fuzzy matching
                foreach (var edge in extractionResult.Edges ?? new List<ExtractedEdge>())
                {
                    try
                    {
                        Guid sourceId = Guid.Empty;
                        Guid targetId = Guid.Empty;

                        // Try to find source entity
                        if (!entityIdMap.TryGetValue(edge.Source, out sourceId))
                        {
                            // Try fuzzy match
                            var matchedSourceName = FindBestEntityNameMatch(edge.Source, entityIdMap.Keys);
                            if (!string.IsNullOrEmpty(matchedSourceName))
                            {
                                entityIdMap.TryGetValue(matchedSourceName, out sourceId);
                                _log.Debug($"Fuzzy matched source '{edge.Source}' to '{matchedSourceName}'");
                            }
                        }

                        // Try to find target entity
                        if (!entityIdMap.TryGetValue(edge.Target, out targetId))
                        {
                            // Try fuzzy match
                            var matchedTargetName = FindBestEntityNameMatch(edge.Target, entityIdMap.Keys);
                            if (!string.IsNullOrEmpty(matchedTargetName))
                            {
                                entityIdMap.TryGetValue(matchedTargetName, out targetId);
                                _log.Debug($"Fuzzy matched target '{edge.Target}' to '{matchedTargetName}'");
                            }
                        }

                        // Only create edge if both entities were found
                        if (sourceId != Guid.Empty && targetId != Guid.Empty)
                        {
                            await StoreEdgeAsync(chunk.ScenarioId, chunk.Source.Id, chunkId, sourceId, targetId, edge, ct);
                            edgesCreated++;
                        }
                        else
                        {
                            var missing = sourceId == Guid.Empty && targetId == Guid.Empty ? "both" :
                                         sourceId == Guid.Empty ? "source" : "target";
                            _log.Debug($"Skipping edge {edge.Source} -> {edge.Target}: {missing} entity not found");
                            // Silently skip - these are normal when LLM references entities loosely
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.Warn(ex, $"Failed to store edge: {edge.Source} -> {edge.Target}");
                        errors.Add($"Edge {edge.Source} -> {edge.Target}: {ex.Message}");
                    }
                }

                chunk.ExtractionStatus = "Completed";
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

        // Helper methods

        private async Task<string> BuildExtractionPromptAsync(ScenarioSourceChunk chunk, CancellationToken ct)
        {
            var templatePath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "config",
                "ContentServices",
                "ScenarioBuilder",
                "ExtractEntities.txt");

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
  ""type"": ""MemberOf|Targets|Exploits|Uses|LocatedAt|CommunicatesWith|DependsOn|Accesses|Owns|ReportsTo|AffiliatedWith|DefendedBy|CommandsAndControl|Custom"",
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
                // Get content engine settings from ScenarioBuilder configuration
                var contentEngineSettings = new ApplicationSettings.AnimatorSettingsDetail.ContentEngineSettings
                {
                    Source = _configuration["ScenarioBuilder:ContentEngine:Source"] ?? "ollama",
                    Model = _configuration["ScenarioBuilder:ContentEngine:Model"] ?? "llama3.1",
                    Host = _configuration["ScenarioBuilder:ContentEngine:Host"] ?? "http://localhost:11434"
                };

                var contentService = new ContentCreationService(contentEngineSettings);

                if (contentService.FormatterService == null)
                {
                    throw new InvalidOperationException("Content service formatter is not available");
                }

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
                // Try to find JSON in the response (LLM might return markdown or extra text)
                var jsonStart = llmResponse.IndexOf('{');
                var jsonEnd = llmResponse.LastIndexOf('}');

                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    var jsonContent = llmResponse.Substring(jsonStart, jsonEnd - jsonStart + 1);

                    // Log the JSON for debugging
                    if (jsonContent.Length < 5000)
                    {
                        _log.Debug($"Parsing JSON response: {jsonContent.Substring(0, Math.Min(500, jsonContent.Length))}...");
                    }

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

                    // Clean up any malformed edges (where source/target might be null or non-string)
                    if (result.Edges != null)
                    {
                        var validEdges = new List<ExtractedEdge>();
                        foreach (var edge in result.Edges)
                        {
                            if (string.IsNullOrWhiteSpace(edge.Source) || string.IsNullOrWhiteSpace(edge.Target))
                            {
                                errors.Add($"Skipped edge with empty source or target");
                                continue;
                            }
                            validEdges.Add(edge);
                        }
                        result.Edges = validEdges;
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

                // Try to log a snippet of what failed
                if (llmResponse.Length < 1000)
                {
                    _log.Debug($"Failed JSON content: {llmResponse}");
                }

                return new ExtractionResponse();
            }
        }

        private async Task<ScenarioEntity> StoreEntityAsync(
            int scenarioId,
            int sourceId,
            int chunkId,
            ExtractedEntity entity,
            CancellationToken ct)
        {
            // Check if entity already exists (case-insensitive name match ONLY - ignore type)
            var existingEntity = await _context.ScenarioEntities
                .FirstOrDefaultAsync(e =>
                    e.ScenarioId == scenarioId &&
                    e.Name.ToLower() == entity.Name.ToLower(),
                    ct);

            if (existingEntity != null)
            {
                // Merge entity types if different
                if (!string.IsNullOrEmpty(entity.Type) &&
                    !existingEntity.EntityType.Contains(entity.Type, StringComparison.OrdinalIgnoreCase))
                {
                    // Add the new type to existing types (separated by |)
                    var existingTypes = existingEntity.EntityType.Split('|', StringSplitOptions.RemoveEmptyEntries);
                    var newTypes = entity.Type.Split('|', StringSplitOptions.RemoveEmptyEntries);
                    var allTypes = existingTypes.Concat(newTypes).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                    existingEntity.EntityType = string.Join("|", allTypes);
                    existingEntity.UpdatedAt = DateTime.UtcNow;
                    _log.Debug($"Merged entity type for {existingEntity.Name}: {existingEntity.EntityType}");
                }

                // Update if new confidence is higher
                if (entity.Confidence > existingEntity.Confidence)
                {
                    existingEntity.Confidence = entity.Confidence;
                    existingEntity.Description = entity.Description ?? existingEntity.Description;
                    existingEntity.UpdatedAt = DateTime.UtcNow;
                    _log.Debug($"Updated existing entity {existingEntity.Name} with higher confidence");
                }

                await _context.SaveChangesAsync(ct);
                return existingEntity;
            }

            // Create new entity
            var newEntity = new ScenarioEntity
            {
                ScenarioId = scenarioId,
                Name = entity.Name,
                EntityType = entity.Type,
                Description = entity.Description ?? string.Empty,
                Confidence = entity.Confidence,
                Origin = "Extracted",
                SourceId = sourceId,
                SourceChunkId = chunkId,
                IsReviewed = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.ScenarioEntities.Add(newEntity);
            await _context.SaveChangesAsync(ct);

            _log.Debug($"Created new entity {newEntity.Name} ({newEntity.EntityType})");

            return newEntity;
        }

        private async Task<ScenarioEdge> StoreEdgeAsync(
            int scenarioId,
            int sourceId,
            int chunkId,
            Guid sourceEntityId,
            Guid targetEntityId,
            ExtractedEdge edge,
            CancellationToken ct)
        {
            // Check if edge already exists
            var existingEdge = await _context.ScenarioEdges
                .FirstOrDefaultAsync(e =>
                    e.ScenarioId == scenarioId &&
                    e.SourceEntityId == sourceEntityId &&
                    e.TargetEntityId == targetEntityId &&
                    e.EdgeType == edge.Type,
                    ct);

            if (existingEdge != null)
            {
                _log.Debug($"Edge already exists: {edge.Source} -> {edge.Target}");
                return existingEdge;
            }

            var newEdge = new ScenarioEdge
            {
                ScenarioId = scenarioId,
                SourceEntityId = sourceEntityId,
                TargetEntityId = targetEntityId,
                EdgeType = edge.Type,
                Label = edge.Label ?? edge.Type,
                Confidence = edge.Confidence,
                Origin = "Extracted",
                SourceId = sourceId,
                SourceChunkId = chunkId,
                IsReviewed = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.ScenarioEdges.Add(newEdge);
            await _context.SaveChangesAsync(ct);

            _log.Debug($"Created new edge {edge.Source} -> {edge.Target} ({edge.Type})");

            return newEdge;
        }

        // Internal DTOs for parsing LLM response
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
                set => _source = value?.ToString() ?? string.Empty; // Handle if LLM sends object
            }

            public string Target
            {
                get => _target;
                set => _target = value?.ToString() ?? string.Empty; // Handle if LLM sends object
            }

            public string Type { get; set; } = "Custom";
            public string Label { get; set; }
            public decimal Confidence { get; set; } = 0.8m;
        }

        private async Task SendProgressNotification(
            int scenarioId,
            string status,
            int chunksProcessed,
            int totalChunks,
            int entitiesCreated,
            int edgesCreated)
        {
            try
            {
                var connections = ScenarioBuilderHub.GetConnections();
                var scenarioConnections = connections.GetConnections(scenarioId.ToString());
                var allConnections = connections.GetConnections("all");

                var progressData = new
                {
                    scenarioId,
                    status,
                    chunksProcessed,
                    totalChunks,
                    entitiesCreated,
                    edgesCreated,
                    timestamp = DateTime.UtcNow
                };

                foreach (var connectionId in scenarioConnections.Concat(allConnections))
                {
                    await _hubContext.Clients.Client(connectionId).SendAsync("extractionProgress", progressData);
                }

                _log.Debug($"Sent extraction progress for scenario {scenarioId}: {status} - {chunksProcessed}/{totalChunks} chunks");
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

            // Try exact match (case-insensitive)
            var exact = entityList.FirstOrDefault(e => e.Equals(searchName, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;

            // Try contains match (search is contained in entity name, or vice versa)
            var contains = entityList.FirstOrDefault(e =>
                e.Contains(searchName, StringComparison.OrdinalIgnoreCase) ||
                searchName.Contains(e, StringComparison.OrdinalIgnoreCase));
            if (contains != null) return contains;

            // Try word-based matching (for multi-word entities)
            var searchWords = searchLower.Split(new[] { ' ', '-', '_', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2) // Ignore short words like "of", "the"
                .ToList();

            if (searchWords.Any())
            {
                var bestMatch = entityList
                    .Select(e => new
                    {
                        Name = e,
                        Words = e.ToLower().Split(new[] { ' ', '-', '_', ',' }, StringSplitOptions.RemoveEmptyEntries)
                            .Where(w => w.Length > 2)
                            .ToList()
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
                {
                    return bestMatch.Name;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Lenient JSON converter that handles malformed LLM responses gracefully
        /// </summary>
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
                {
                    return result;
                }

                using var doc = JsonDocument.ParseValue(ref reader);
                var root = doc.RootElement;

                // Parse entities array
                if (root.TryGetProperty("entities", out var entitiesElement) && entitiesElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var entityElement in entitiesElement.EnumerateArray())
                    {
                        try
                        {
                            var entity = new ExtractedEntity();

                            if (entityElement.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String)
                                entity.Name = nameEl.GetString() ?? string.Empty;

                            if (entityElement.TryGetProperty("type", out var typeEl) && typeEl.ValueKind == JsonValueKind.String)
                                entity.Type = typeEl.GetString() ?? "Custom";

                            if (entityElement.TryGetProperty("description", out var descEl) && descEl.ValueKind == JsonValueKind.String)
                                entity.Description = descEl.GetString();

                            if (entityElement.TryGetProperty("confidence", out var confEl) && confEl.ValueKind == JsonValueKind.Number)
                                entity.Confidence = confEl.GetDecimal();

                            // Only add if entity has a name
                            if (!string.IsNullOrWhiteSpace(entity.Name))
                            {
                                result.Entities.Add(entity);
                            }
                        }
                        catch
                        {
                            // Skip malformed entity
                            continue;
                        }
                    }
                }

                // Parse edges array
                if (root.TryGetProperty("edges", out var edgesElement) && edgesElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var edgeElement in edgesElement.EnumerateArray())
                    {
                        try
                        {
                            var edge = new ExtractedEdge();

                            // Handle source - can be string, object, or array
                            if (edgeElement.TryGetProperty("source", out var sourceEl))
                            {
                                edge.Source = ExtractStringValue(sourceEl);
                            }

                            // Handle target - can be string, object, or array
                            if (edgeElement.TryGetProperty("target", out var targetEl))
                            {
                                edge.Target = ExtractStringValue(targetEl);
                            }

                            if (edgeElement.TryGetProperty("type", out var typeEl) && typeEl.ValueKind == JsonValueKind.String)
                                edge.Type = typeEl.GetString() ?? "Custom";

                            if (edgeElement.TryGetProperty("label", out var labelEl) && labelEl.ValueKind == JsonValueKind.String)
                                edge.Label = labelEl.GetString();

                            if (edgeElement.TryGetProperty("confidence", out var confEl) && confEl.ValueKind == JsonValueKind.Number)
                                edge.Confidence = confEl.GetDecimal();

                            // Only add if edge has both source and target
                            if (!string.IsNullOrWhiteSpace(edge.Source) && !string.IsNullOrWhiteSpace(edge.Target))
                            {
                                result.Edges.Add(edge);
                            }
                        }
                        catch
                        {
                            // Skip malformed edge
                            continue;
                        }
                    }
                }

                return result;
            }

            private static string ExtractStringValue(JsonElement element)
            {
                switch (element.ValueKind)
                {
                    case JsonValueKind.String:
                        return element.GetString() ?? string.Empty;

                    case JsonValueKind.Object:
                        // If it's an object, try to get a "name" or "id" property
                        if (element.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String)
                            return nameEl.GetString() ?? string.Empty;
                        if (element.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String)
                            return idEl.GetString() ?? string.Empty;
                        // Otherwise return the raw JSON
                        return element.GetRawText();

                    case JsonValueKind.Array:
                        // If it's an array, try to get the first string element
                        var arr = element.EnumerateArray().FirstOrDefault();
                        if (arr.ValueKind == JsonValueKind.String)
                            return arr.GetString() ?? string.Empty;
                        return string.Empty;

                    case JsonValueKind.Number:
                        return element.ToString();

                    default:
                        return string.Empty;
                }
            }

            public override void Write(Utf8JsonWriter writer, ExtractionResponse value, JsonSerializerOptions options)
            {
                JsonSerializer.Serialize(writer, value, options);
            }
        }
    }
}
