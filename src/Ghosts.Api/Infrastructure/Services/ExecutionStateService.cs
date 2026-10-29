// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Hubs;
using Ghosts.Api.Infrastructure.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NLog;

namespace Ghosts.Api.Infrastructure.Services;

/// <summary>White Cell's change to a run's flags. Set and Clear are flag names; By is who made the change.</summary>
public record SetFlagsDto(List<string> Set, List<string> Clear, string By);

/// <summary>White Cell's record of which answer a decision point got.</summary>
public record RecordDecisionDto(string ChoiceId, string By);

/// <summary>White Cell's judgment of an item's expected response: met or missed, with notes.</summary>
public record MarkResponseDto(bool Met, string Notes, string By);

public record ObjectiveStatusDto(int Id, string Name, string MetWhen, bool Met, DateTime? MetAt);

public record ExecutionStateDto(int ExecutionId, double ClockMinutes, List<string> Flags, Dictionary<string, string> Facts,
    List<ObjectiveStatusDto> Objectives, List<RecordedDecision> Decisions, List<ResponseMark> Responses);

public interface IExecutionStateService
{
    Task<ExecutionStateDto> GetAsync(int executionId, CancellationToken ct);
    Task<ExecutionStateDto> SetFlagsAsync(int executionId, SetFlagsDto dto, CancellationToken ct);
    Task<ExecutionStateDto> RecordDecisionAsync(int executionId, int itemId, RecordDecisionDto dto, CancellationToken ct);
    Task<ExecutionStateDto> MarkResponseAsync(int executionId, int itemId, MarkResponseDto dto, CancellationToken ct);
}

/// <summary>
/// White Cell's view of and hand on a run's ground truth: read it, set or clear flags, and record the answer a
/// decision point got (its effects apply and the item completes). Objectives are re-evaluated after every change.
/// </summary>
public class ExecutionStateService(DbContext context, IHubContext<ExecutionHub> executionHub) : IExecutionStateService
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private static readonly string[] Terminal = ["Completed", "Failed", "Skipped"];

    public async Task<ExecutionStateDto> GetAsync(int executionId, CancellationToken ct)
    {
        var execution = await ExecutionAsync(executionId, ct);
        var clock = ExecutionClock.ElapsedMinutes(execution, DateTime.UtcNow);
        var state = RunState.Read(execution);
        var objectives = await ObjectivesAsync(context, execution, state, clock, ct);
        return Dto(execution, state, objectives, clock);
    }

    public async Task<ExecutionStateDto> SetFlagsAsync(int executionId, SetFlagsDto dto, CancellationToken ct)
    {
        var execution = await ExecutionAsync(executionId, ct);
        var state = RunState.Read(execution);
        var set = (dto.Set ?? []).Where(f => !string.IsNullOrWhiteSpace(f)).Select(f => f.Trim()).ToList();
        var clear = (dto.Clear ?? []).Where(f => !string.IsNullOrWhiteSpace(f)).Select(f => f.Trim()).ToHashSet(StringComparer.Ordinal);
        state = state with { Flags = state.Flags.Concat(set).Where(f => !clear.Contains(f)).Distinct(StringComparer.Ordinal).ToList() };

        AddEvent(executionId, "FlagsChanged", $"{dto.By ?? "white-cell"} set [{string.Join(", ", set)}] cleared [{string.Join(", ", clear)}]",
            new { set, clear, by = dto.By });
        return await SaveAsync(execution, state, ct);
    }

    public async Task<ExecutionStateDto> RecordDecisionAsync(int executionId, int itemId, RecordDecisionDto dto, CancellationToken ct)
    {
        var execution = await ExecutionAsync(executionId, ct);
        var item = await context.Set<ExecutionTimelineItem>().FirstOrDefaultAsync(i => i.Id == itemId && i.ExecutionId == executionId, ct)
                   ?? throw new InvalidOperationException("Timeline item not found");
        var choices = ScenarioExtras.Read<EventExtrasDto>(item.Extras)?.Choices ?? [];
        if (choices.Count == 0) throw new InvalidOperationException($"Timeline item #{item.Number} is not a decision point.");
        var choice = choices.FirstOrDefault(c => c.Id == dto.ChoiceId)
                     ?? throw new InvalidOperationException($"\"{dto.ChoiceId}\" is not one of item #{item.Number}'s choices ({string.Join(", ", choices.Select(c => c.Id))}).");
        if (Terminal.Contains(item.Status)) throw new InvalidOperationException($"Timeline item #{item.Number} is already {item.Status}.");

        var now = DateTime.UtcNow;
        var state = RunState.Apply(RunState.Read(execution), choice.Effects);
        state = state with { Decisions = [.. state.Decisions, new RecordedDecision(item.Id, item.Number, choice.Id, choice.Label, dto.By ?? "white-cell", now)] };

        item.Status = "Completed";
        item.CompletedAt = now;
        item.CompletedBy = dto.By ?? "white-cell";
        item.Notes = $"Decision: {choice.Label}";
        AddEvent(executionId, "DecisionRecorded", $"Item #{item.Number}: {choice.Label} (by {item.CompletedBy})",
            new { itemId, choiceId = choice.Id, by = item.CompletedBy });
        return await SaveAsync(execution, state, ct);
    }

    /// <summary>Marks whether an item's expected response was met. Marking an item again replaces its earlier mark.</summary>
    public async Task<ExecutionStateDto> MarkResponseAsync(int executionId, int itemId, MarkResponseDto dto, CancellationToken ct)
    {
        var execution = await ExecutionAsync(executionId, ct);
        var item = await context.Set<ExecutionTimelineItem>().FirstOrDefaultAsync(i => i.Id == itemId && i.ExecutionId == executionId, ct)
                   ?? throw new InvalidOperationException("Timeline item not found");
        var expected = ScenarioExtras.Read<EventExtrasDto>(item.Extras)?.ExpectedResponse;
        if (string.IsNullOrWhiteSpace(expected)) throw new InvalidOperationException($"Timeline item #{item.Number} has no expected response.");

        var by = dto.By ?? "white-cell";
        var state = RunState.Read(execution);
        state = state with { Responses = [.. state.Responses.Where(r => r.ItemId != item.Id), new ResponseMark(item.Id, item.Number, expected, dto.Met, dto.Notes, by, DateTime.UtcNow)] };
        AddEvent(executionId, "ResponseMarked", $"Item #{item.Number}: expected response {(dto.Met ? "met" : "missed")} (by {by})",
            new { itemId, met = dto.Met, notes = dto.Notes, by });
        return await SaveAsync(execution, state, ct);
    }

    /// <summary>
    /// Each objective's status, marking newly met ones in the state. Definitions come from the scenario's latest
    /// stored document, whose ids conditions refer to; failing that, the objective rows in order, numbered from 1.
    /// </summary>
    public static async Task<List<ObjectiveStatusDto>> ObjectivesAsync(DbContext context, Execution execution, ExecutionRunState state, double clock, CancellationToken ct)
    {
        var definitions = await DefinitionsAsync(context, execution.ScenarioId, ct);
        var now = DateTime.UtcNow;
        foreach (var (id, _, metWhen) in definitions)
        {
            if (state.ObjectivesMet.ContainsKey(id) || string.IsNullOrWhiteSpace(metWhen)) continue;
            if (ConditionEvaluator.Holds(metWhen, state, clock)) state.ObjectivesMet[id] = now;
        }
        return definitions.Select(d => new ObjectiveStatusDto(d.Id, d.Name, d.MetWhen, state.ObjectivesMet.ContainsKey(d.Id),
            state.ObjectivesMet.TryGetValue(d.Id, out var at) ? at : null)).ToList();
    }

    private static async Task<List<(int Id, string Name, string MetWhen)>> DefinitionsAsync(DbContext context, int scenarioId, CancellationToken ct)
    {
        var document = await context.Set<ScenarioDocument>()
            .Where(d => d.ScenarioId == scenarioId)
            .OrderByDescending(d => d.CreatedAt).ThenByDescending(d => d.Id)
            .Select(d => d.Document)
            .FirstOrDefaultAsync(ct);
        if (!string.IsNullOrEmpty(document) && JsonNode.Parse(document)?["assessment"]?["objectives"] is JsonArray fromDocument)
        {
            return fromDocument.OfType<JsonObject>()
                .Where(o => o["id"] is JsonValue v && v.TryGetValue<int>(out _))
                .Select(o => (o["id"]!.GetValue<int>(), o["name"]?.GetValue<string>() ?? string.Empty, o["metWhen"]?.GetValue<string>()))
                .ToList();
        }

        var rows = await context.Set<Objective>().Where(o => o.ScenarioId == scenarioId)
            .OrderBy(o => o.SortOrder).ThenBy(o => o.Id).ToListAsync(ct);
        return rows.Select((o, i) => (i + 1, o.Name, ScenarioExtras.Read<ObjectiveExtrasDto>(o.Extras)?.MetWhen)).ToList();
    }

    private async Task<ExecutionStateDto> SaveAsync(Execution execution, ExecutionRunState state, CancellationToken ct)
    {
        var clock = ExecutionClock.ElapsedMinutes(execution, DateTime.UtcNow);
        var objectives = await ObjectivesAsync(context, execution, state, clock, ct);
        RunState.Write(execution, state);
        await context.SaveChangesAsync(ct);
        var dto = Dto(execution, state, objectives, clock);
        await BroadcastAsync(execution.Id, dto);
        return dto;
    }

    private static ExecutionStateDto Dto(Execution execution, ExecutionRunState state, List<ObjectiveStatusDto> objectives, double clock) =>
        new(execution.Id, Math.Round(clock, 2), state.Flags, state.Facts, objectives, state.Decisions, state.Responses);

    private async Task<Execution> ExecutionAsync(int executionId, CancellationToken ct) =>
        await context.Set<Execution>().FirstOrDefaultAsync(e => e.Id == executionId, ct)
        ?? throw new InvalidOperationException("Execution not found");

    private void AddEvent(int executionId, string type, string description, object data) =>
        context.Set<ExecutionEvent>().Add(new ExecutionEvent
        {
            ExecutionId = executionId,
            Timestamp = DateTime.UtcNow,
            EventType = type,
            Description = description,
            Data = JsonSerializer.Serialize(data),
            Severity = "Info"
        });

    private async Task BroadcastAsync(int executionId, ExecutionStateDto state)
    {
        try
        {
            var connections = ExecutionHub.GetConnections();
            var message = new { executionId, updateType = "RunStateUpdate", data = state, timestamp = DateTime.UtcNow };
            foreach (var id in connections.GetConnections(executionId.ToString()).Concat(connections.GetConnections("all")))
                await executionHub.Clients.Client(id).SendAsync("ExecutionUpdate", message);
        }
        catch (Exception ex)
        {
            _log.Warn($"Failed to broadcast run state: {ex.Message}");
        }
    }
}
