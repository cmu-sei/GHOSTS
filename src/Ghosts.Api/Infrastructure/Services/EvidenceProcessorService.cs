// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Api.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using NLog;

namespace Ghosts.Api.Infrastructure.Services;

public interface IEvidenceProcessor
{
    Task ProcessSocialEvidenceAsync(Guid authorNpcId, string content,
        NpcActivity.ActivityTypes activityType, CancellationToken ct);

    // Future: host/cyber evidence seam
    // Task ProcessHostEvidenceAsync(Guid npcId, string handler, string result, CancellationToken ct);
}

public class EvidenceProcessorService(ApplicationDbContext context) : IEvidenceProcessor
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private readonly ApplicationDbContext _context = context;

    public async Task ProcessSocialEvidenceAsync(Guid authorNpcId, string content,
        NpcActivity.ActivityTypes activityType, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(content)) return;

        try
        {
            // 1. Load author NPC to get ScenarioId
            var author = await _context.Npcs.AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == authorNpcId, ct);
            if (author?.ScenarioId == null) return;

            // 2. Find running execution for this scenario
            var execution = await _context.Executions
                .FirstOrDefaultAsync(e => e.ScenarioId == author.ScenarioId
                    && e.Status == ExecutionStatus.Running, ct);
            if (execution == null) return;

            // 3. Load active hypotheses (execution-scoped or global)
            var hypotheses = await GetActiveHypothesesAsync(execution, ct);
            if (hypotheses.Count == 0) return;

            // 4. Match content against hypothesis keywords
            var matched = MatchHypotheses(hypotheses, content);
            if (matched.Count == 0) return;

            // 5. Get observer NPCs: author + their social connections
            var observerIds = await GetObserverNpcIdsAsync(authorNpcId, ct);

            // 6. Update beliefs for each observer × matched hypothesis
            var newBeliefs = new List<NpcBelief>();
            foreach (var hypothesis in matched)
            {
                // Batch-load latest beliefs for all observers for this hypothesis
                var latestBeliefs = await _context.NpcBeliefs
                    .Where(b => observerIds.Contains(b.NpcId) && b.Name == hypothesis.Name)
                    .GroupBy(b => b.NpcId)
                    .Select(g => g.OrderByDescending(b => b.Step).First())
                    .ToListAsync(ct);

                var beliefLookup = latestBeliefs.ToDictionary(b => b.NpcId);

                foreach (var observerId in observerIds)
                {
                    beliefLookup.TryGetValue(observerId, out var prior);

                    var priorPosterior = prior?.Posterior ?? 0.5m;
                    var step = (prior?.Step ?? 0) + 1;
                    var likelihoodH1 = hypothesis.DefaultLikelihood;
                    var likelihoodH2 = 1m - likelihoodH1;

                    var bayes = new Bayes(step, likelihoodH1, priorPosterior,
                        likelihoodH2, 1m - priorPosterior);

                    newBeliefs.Add(new NpcBelief
                    {
                        NpcId = observerId,
                        ToNpcId = observerId,
                        FromNpcId = authorNpcId,
                        Name = hypothesis.Name,
                        Step = step,
                        Likelihood = likelihoodH1,
                        Posterior = bayes.PosteriorH1,
                        ExecutionId = execution.Id,
                        CreatedUtc = DateTime.UtcNow
                    });
                }
            }

            if (newBeliefs.Count > 0)
            {
                await _context.NpcBeliefs.AddRangeAsync(newBeliefs, ct);
                await _context.SaveChangesAsync(ct);

                _log.Trace($"Evidence processed: {newBeliefs.Count} belief updates " +
                    $"for execution {execution.Id}, author {authorNpcId}, " +
                    $"{matched.Count} hypotheses matched");
            }
        }
        catch (Exception e)
        {
            _log.Error(e, $"Error processing social evidence for NPC {authorNpcId}");
        }
    }

    private async Task<List<Hypothesis>> GetActiveHypothesesAsync(Execution execution, CancellationToken ct)
    {
        // Check if execution specifies active hypothesis IDs
        int[] hypothesisIds = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(execution.Configuration) && execution.Configuration != "{}")
            {
                using var doc = JsonDocument.Parse(execution.Configuration);
                if (doc.RootElement.TryGetProperty("activeHypothesisIds", out var idsElement)
                    && idsElement.ValueKind == JsonValueKind.Array)
                {
                    hypothesisIds = idsElement.EnumerateArray()
                        .Where(e => e.ValueKind == JsonValueKind.Number)
                        .Select(e => e.GetInt32())
                        .ToArray();
                }
            }
        }
        catch (JsonException)
        {
            // Malformed config — fall through to loading all active
        }

        if (hypothesisIds != null && hypothesisIds.Length > 0)
        {
            return await _context.Hypotheses
                .Where(h => hypothesisIds.Contains(h.Id))
                .ToListAsync(ct);
        }

        return await _context.Hypotheses
            .Where(h => h.IsActive)
            .ToListAsync(ct);
    }

    private static List<Hypothesis> MatchHypotheses(List<Hypothesis> hypotheses, string content)
    {
        var contentLower = content.ToLowerInvariant();
        var matched = new List<Hypothesis>();

        foreach (var h in hypotheses)
        {
            if (string.IsNullOrWhiteSpace(h.Keywords)) continue;

            var keywords = h.Keywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (keywords.Any(kw => contentLower.Contains(kw.ToLowerInvariant())))
            {
                matched.Add(h);
            }
        }

        return matched;
    }

    private async Task<List<Guid>> GetObserverNpcIdsAsync(Guid authorNpcId, CancellationToken ct)
    {
        var connectedIds = await _context.NpcSocialConnections
            .Where(c => c.NpcId == authorNpcId)
            .Select(c => c.ConnectedNpcId)
            .ToListAsync(ct);

        // Author + their connections
        var observers = new List<Guid> { authorNpcId };
        observers.AddRange(connectedIds);
        return observers.Distinct().ToList();
    }
}
