// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Api.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using NLog;

namespace Ghosts.Api.Infrastructure.Services
{
    public interface IScenarioSourceService
    {
        Task<List<ScenarioSource>> GetByScenarioAsync(int scenarioId, CancellationToken ct);
        Task<ScenarioSource> GetByIdAsync(int id, CancellationToken ct);
        Task<ScenarioSource> AddTextAsync(int scenarioId, CreateScenarioSourceTextDto dto, CancellationToken ct);
        Task<ScenarioSource> AddUrlAsync(int scenarioId, CreateScenarioSourceUrlDto dto, CancellationToken ct);
        Task<ScenarioSource> UploadFileAsync(int scenarioId, string fileName, string mimeType, byte[] fileData, string textContent, CancellationToken ct);
        Task DeleteAsync(int id, CancellationToken ct);
        Task<ScenarioSource> ChunkSourceAsync(int sourceId, CancellationToken ct);
        Task<List<ScenarioSourceChunk>> GetChunksAsync(int sourceId, CancellationToken ct);
    }

    public class ScenarioSourceService(ApplicationDbContext context) : IScenarioSourceService
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private readonly ApplicationDbContext _context = context;

        public async Task<List<ScenarioSource>> GetByScenarioAsync(int scenarioId, CancellationToken ct)
        {
            return await _context.ScenarioSources
                .Where(s => s.ScenarioId == scenarioId)
                .Include(s => s.Chunks)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync(ct);
        }

        public async Task<ScenarioSource> GetByIdAsync(int id, CancellationToken ct)
        {
            var source = await _context.ScenarioSources
                .Include(s => s.Chunks)
                .FirstOrDefaultAsync(s => s.Id == id, ct);

            if (source == null)
            {
                _log.Error($"ScenarioSource not found: {id}");
                throw new InvalidOperationException("ScenarioSource not found");
            }

            return source;
        }

        public async Task<ScenarioSource> AddTextAsync(int scenarioId, CreateScenarioSourceTextDto dto, CancellationToken ct)
        {
            var source = new ScenarioSource
            {
                ScenarioId = scenarioId,
                Name = dto.Name,
                SourceType = "Text",
                Content = dto.Content,
                Status = "Uploaded",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.ScenarioSources.Add(source);

            var operation = await _context.SaveChangesAsync(ct);
            if (operation < 1)
            {
                _log.Error($"Could not create text source: {operation}");
                throw new InvalidOperationException("Could not create text source");
            }

            _log.Info($"Created text source: {source.Id} - {source.Name}");

            // Auto-chunk the source
            await ChunkSourceAsync(source.Id, ct);

            // Reload with chunks
            return await GetByIdAsync(source.Id, ct);
        }

        public async Task<ScenarioSource> AddUrlAsync(int scenarioId, CreateScenarioSourceUrlDto dto, CancellationToken ct)
        {
            // Fetch the page content from the URL
            var fetchedContent = string.Empty;
            try
            {
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30);
                httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
                    "Mozilla/5.0 (compatible; GHOSTS/ScenarioBuilder)");
                var html = await httpClient.GetStringAsync(dto.Url, ct);
                fetchedContent = StripHtml(html);
                _log.Info($"Fetched {fetchedContent.Length} characters from URL: {dto.Url}");
            }
            catch (Exception ex)
            {
                _log.Warn(ex, $"Failed to fetch URL content from {dto.Url}");
                throw new InvalidOperationException($"Could not fetch URL content: {ex.Message}", ex);
            }

            var source = new ScenarioSource
            {
                ScenarioId = scenarioId,
                Name = dto.Name,
                SourceType = "Url",
                Content = fetchedContent,
                Status = "Uploaded",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.ScenarioSources.Add(source);

            var operation = await _context.SaveChangesAsync(ct);
            if (operation < 1)
            {
                _log.Error($"Could not create URL source: {operation}");
                throw new InvalidOperationException("Could not create URL source");
            }

            _log.Info($"Created URL source: {source.Id} - {source.Name}");

            // Auto-chunk the fetched content
            await ChunkSourceAsync(source.Id, ct);

            // Reload with chunks
            return await GetByIdAsync(source.Id, ct);
        }

        private static string StripHtml(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return string.Empty;

            // Remove script and style blocks entirely
            var text = Regex.Replace(html,
                @"<(script|style)[^>]*>[\s\S]*?</(script|style)>", " ",
                RegexOptions.IgnoreCase);

            // Replace block-level tags with newlines
            text = Regex.Replace(text,
                @"</(p|div|li|tr|h[1-6]|br|blockquote)[^>]*>", "\n",
                RegexOptions.IgnoreCase);

            // Strip remaining tags
            text = Regex.Replace(text, @"<[^>]+>", " ");

            // Decode HTML entities
            text = WebUtility.HtmlDecode(text);

            // Collapse whitespace
            text = Regex.Replace(text, @"[ \t]+", " ");
            text = Regex.Replace(text, @"\n{3,}", "\n\n");

            return text.Trim();
        }

        public async Task<ScenarioSource> UploadFileAsync(int scenarioId, string fileName, string mimeType, byte[] fileData, string textContent, CancellationToken ct)
        {
            var source = new ScenarioSource
            {
                ScenarioId = scenarioId,
                Name = fileName,
                SourceType = "Document",
                FileData = fileData,
                Content = textContent,
                OriginalFileName = fileName,
                MimeType = mimeType,
                FileSizeBytes = fileData.Length,
                Status = "Uploaded",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.ScenarioSources.Add(source);

            var operation = await _context.SaveChangesAsync(ct);
            if (operation < 1)
            {
                _log.Error($"Could not upload file source: {operation}");
                throw new InvalidOperationException("Could not upload file source");
            }

            _log.Info($"Uploaded file source: {source.Id} - {source.Name} ({source.FileSizeBytes} bytes)");

            // Auto-chunk the source
            await ChunkSourceAsync(source.Id, ct);

            // Reload with chunks
            return await GetByIdAsync(source.Id, ct);
        }

        public async Task DeleteAsync(int id, CancellationToken ct)
        {
            var source = await _context.ScenarioSources.FindAsync(id);
            if (source == null)
            {
                _log.Error($"ScenarioSource not found: {id}");
                throw new InvalidOperationException("ScenarioSource not found");
            }

            _context.ScenarioSources.Remove(source);

            var operation = await _context.SaveChangesAsync(ct);
            if (operation < 1)
            {
                _log.Error($"Could not delete source: {operation}");
                throw new InvalidOperationException("Could not delete ScenarioSource");
            }

            _log.Info($"Deleted source: {id}");
        }

        public async Task<ScenarioSource> ChunkSourceAsync(int sourceId, CancellationToken ct)
        {
            var source = await _context.ScenarioSources
                .Include(s => s.Chunks)
                .FirstOrDefaultAsync(s => s.Id == sourceId, ct);

            if (source == null)
            {
                _log.Error($"ScenarioSource not found: {sourceId}");
                throw new InvalidOperationException("ScenarioSource not found");
            }

            if (string.IsNullOrWhiteSpace(source.Content))
            {
                _log.Warn($"Source {sourceId} has no content to chunk");
                return source;
            }

            source.Status = "Chunking";
            source.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);

            // Clear existing chunks
            _context.ScenarioSourceChunks.RemoveRange(source.Chunks);

            // Simple paragraph-based chunking with overlap
            const int chunkSize = 4000;
            const int overlapSize = 500;

            var content = source.Content;
            var chunks = new List<ScenarioSourceChunk>();
            var chunkIndex = 0;
            var position = 0;

            while (position < content.Length)
            {
                var length = Math.Min(chunkSize, content.Length - position);
                var chunkContent = content.Substring(position, length);

                // Try to break at paragraph or newline for cleaner chunks
                if (position + length < content.Length)
                {
                    var lastNewline = chunkContent.LastIndexOf("\n\n");
                    if (lastNewline > chunkSize / 2) // Only break if we're past halfway
                    {
                        length = lastNewline + 2;
                        chunkContent = content.Substring(position, length);
                    }
                    else
                    {
                        lastNewline = chunkContent.LastIndexOf("\n");
                        if (lastNewline > chunkSize / 2)
                        {
                            length = lastNewline + 1;
                            chunkContent = content.Substring(position, length);
                        }
                    }
                }

                var chunk = new ScenarioSourceChunk
                {
                    SourceId = sourceId,
                    ScenarioId = source.ScenarioId,
                    ChunkIndex = chunkIndex,
                    Content = chunkContent.Trim(),
                    TokenCount = EstimateTokenCount(chunkContent),
                    ExtractionStatus = "Pending",
                    CreatedAt = DateTime.UtcNow
                };

                chunks.Add(chunk);
                chunkIndex++;

                // Move position forward with overlap
                position += length;
                if (position < content.Length)
                {
                    position -= overlapSize;
                }
            }

            _context.ScenarioSourceChunks.AddRange(chunks);
            source.Status = "Chunked";
            source.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);

            _log.Info($"Chunked source {sourceId} into {chunks.Count} chunks");

            // Update Scenario.BuilderStatus if it's "None"
            var scenario = await _context.Scenarios.FindAsync(source.ScenarioId);
            if (scenario != null && scenario.BuilderStatus == "None")
            {
                scenario.BuilderStatus = "Sources";
                scenario.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(ct);
            }

            return source;
        }

        public async Task<List<ScenarioSourceChunk>> GetChunksAsync(int sourceId, CancellationToken ct)
        {
            return await _context.ScenarioSourceChunks
                .Where(c => c.SourceId == sourceId)
                .OrderBy(c => c.ChunkIndex)
                .ToListAsync(ct);
        }

        private static int EstimateTokenCount(string text)
        {
            // Simple estimation: ~4 characters per token
            return text.Length / 4;
        }
    }
}
