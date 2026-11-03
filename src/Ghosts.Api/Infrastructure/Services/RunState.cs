// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Ghosts.Api.Infrastructure.Services;

/// <summary>
/// A run's ground truth, kept on the execution: the flags and facts set so far, the objectives met (by the
/// document's own objective ids), the decisions White Cell recorded and its marks on expected responses. Seeded
/// from startingConditions at start.
/// </summary>
public record ExecutionRunState(List<string> Flags, Dictionary<string, string> Facts, Dictionary<int, DateTime> ObjectivesMet, List<RecordedDecision> Decisions,
    List<ResponseMark> Responses)
{
    public static ExecutionRunState Empty() => new([], [], [], [], []);
}

public record RecordedDecision(int ItemId, int ItemNumber, string ChoiceId, string Label, string By, DateTime At);

/// <summary>White Cell's judgment of whether the participants gave an item's expected response.</summary>
public record ResponseMark(int ItemId, int ItemNumber, string Expected, bool Met, string Notes, string By, DateTime At);

/// <summary>
/// The scenario document's condition grammar, as experimental/rpg's engine evaluates it: terms joined by &amp;&amp;,
/// each flag:x (x is set), !x (x is not set), objective:N (objective N is met) or clock&gt;=M (M minutes of exercise
/// time have passed). An empty condition holds; a term the grammar does not know does not.
/// </summary>
public static class ConditionEvaluator
{
    public static bool Holds(string condition, ExecutionRunState state, double clockMinutes)
    {
        if (string.IsNullOrWhiteSpace(condition)) return true;
        return condition.Split("&&").Select(t => t.Trim()).All(term => Term(term, state, clockMinutes));
    }

    private static bool Term(string term, ExecutionRunState state, double clockMinutes)
    {
        if (term.Length == 0) return true;
        if (term.StartsWith("flag:", StringComparison.Ordinal)) return state.Flags.Contains(term[5..].Trim());
        if (term.StartsWith('!')) return !state.Flags.Contains(term[1..].Trim());
        if (term.StartsWith("objective:", StringComparison.Ordinal))
            return int.TryParse(term[10..].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) && state.ObjectivesMet.ContainsKey(id);
        if (term.StartsWith("clock>=", StringComparison.Ordinal))
            return double.TryParse(term[7..].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var minutes) && clockMinutes >= minutes;
        return false;
    }
}

/// <summary>Reading, seeding and changing a run's state on its execution row.</summary>
public static class RunState
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static ExecutionRunState Read(Execution execution) =>
        string.IsNullOrWhiteSpace(execution.RunState)
            ? ExecutionRunState.Empty()
            : Normalize(JsonSerializer.Deserialize<ExecutionRunState>(execution.RunState, Json));

    public static void Write(Execution execution, ExecutionRunState state) =>
        execution.RunState = JsonSerializer.Serialize(state, Json);

    /// <summary>The state a run starts with: the document's startingConditions.</summary>
    public static ExecutionRunState Seed(StartingConditionsDto starting) => new(
        (starting?.Flags ?? []).Distinct(StringComparer.Ordinal).ToList(),
        new Dictionary<string, string>(starting?.Facts ?? new Dictionary<string, string>()),
        [],
        [],
        []);

    /// <summary>Sets an effect's flags and facts. Effects only set: nothing in the grammar clears a flag.</summary>
    public static ExecutionRunState Apply(ExecutionRunState state, EffectsDto effects)
    {
        if (effects == null) return state;
        var flags = state.Flags.Concat(effects.SetFlags ?? []).Distinct(StringComparer.Ordinal).ToList();
        var facts = new Dictionary<string, string>(state.Facts);
        foreach (var (k, v) in effects.SetFacts ?? new Dictionary<string, string>()) facts[k] = v;
        return state with { Flags = flags, Facts = facts };
    }

    /// <summary>
    /// The run state as the database holds it now, not as this context loaded it, so a change another request saved
    /// meanwhile (White Cell setting a flag, say) is kept when this one writes.
    /// </summary>
    public static async Task<ExecutionRunState> ReadFreshAsync(DbContext context, Execution execution, CancellationToken ct)
    {
        execution.RunState = await context.Set<Execution>().AsNoTracking()
            .Where(e => e.Id == execution.Id).Select(e => e.RunState).FirstOrDefaultAsync(ct);
        return Read(execution);
    }

    /// <summary>Applies the effects a completed item's scenario event declares, onto the run state as it is now.</summary>
    public static async Task ApplyItemEffectsAsync(DbContext context, Execution execution, ExecutionTimelineItem item, CancellationToken ct)
    {
        var effects = ScenarioExtras.Read<EventExtrasDto>(item.Extras)?.Effects;
        if (effects == null) return;
        Write(execution, Apply(await ReadFreshAsync(context, execution, ct), effects));
    }

    private static ExecutionRunState Normalize(ExecutionRunState s) => s == null
        ? ExecutionRunState.Empty()
        : new ExecutionRunState(s.Flags ?? [], s.Facts ?? [], s.ObjectivesMet ?? [], s.Decisions ?? [], s.Responses ?? []);
}
