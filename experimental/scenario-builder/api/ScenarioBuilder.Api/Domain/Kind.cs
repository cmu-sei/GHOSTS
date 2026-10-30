// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScenarioBuilder.Api.Domain;

public enum AnswerType { Text, Choice, List }

/// <summary>One design question. <see cref="Path"/> is where its answer lands in the scenario document; <see cref="Label"/> names it in a list.</summary>
public record KindQuestion(
    string Id,
    string Text,
    string Why,
    string? Recommended,
    AnswerType Answer,
    List<string>? Options,
    string Path,
    string? IdPrefix = null,
    string? Visibility = null,
    string? Label = null,
    string? Default = null);

/// <summary>What closes a stage besides its answers: an approval the owner gives, when named.</summary>
public record KindGate(string? Approval);

public record KindStage(int Number, string Title, List<KindQuestion> Questions, KindGate Gate);

/// <summary>Text the agent drafts once every question of <see cref="DueAfterStage"/> is answered.</summary>
public record KindDraft(string Field, int DueAfterStage, string Path, string? Visibility = null);

/// <summary>A type of scenario: its stages, questions, gates and drafts, read from a <c>*.kind.json</c> file.</summary>
public record Kind(string Id, string Version, string Name, List<KindStage> Stages, List<KindDraft> Drafts)
{
    /// <summary>Who may see a piece of a scenario: everyone, exercise control and designers, or designers only.</summary>
    public static readonly string[] Visibilities = ["participant", "excon", "designer"];

    /// <summary>The one default a question can have: a new scenario answers it with the scenario's own name.</summary>
    public const string ScenarioNameDefault = "scenarioName";

    public IEnumerable<KindQuestion> AllQuestions => Stages.SelectMany(s => s.Questions);

    public int LastStage => Stages[^1].Number;

    public (KindQuestion Question, int Stage)? Find(string questionId)
    {
        foreach (var stage in Stages)
        foreach (var question in stage.Questions)
            if (question.Id == questionId)
                return (question, stage.Number);
        return null;
    }
}

/// <summary>Every kind in a directory, checked when loaded: a broken kind file stops the API from starting.</summary>
public sealed class KindCatalog
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly List<Kind> _kinds = [];

    public KindCatalog(string directory)
    {
        foreach (var file in Directory.GetFiles(directory, "*.kind.json").Order())
        {
            var kind = JsonSerializer.Deserialize<Kind>(File.ReadAllText(file), Json)
                       ?? throw new InvalidOperationException($"{file}: empty kind file.");
            Validate(kind, Path.GetFileName(file));
            _kinds.Add(kind);
        }
    }

    public IReadOnlyList<Kind> All => _kinds;

    public Kind? Find(string id) => _kinds.FirstOrDefault(k => k.Id == id);

    private static void Validate(Kind kind, string file)
    {
        void Fail(string rule) => throw new InvalidOperationException($"{file}: {rule}");

        if (kind.Stages is not { Count: > 0 })
            Fail("a kind needs at least one stage.");
        var ids = new HashSet<string>();
        for (var i = 0; i < kind.Stages.Count; i++)
        {
            var stage = kind.Stages[i];
            if (stage.Number != i)
                Fail($"stages must be numbered 0, 1, 2… in order; found {stage.Number} at position {i}.");
            if (stage.Gate is null)
                Fail($"stage {i} has no gate; use {{}} for none.");
            foreach (var q in stage.Questions)
            {
                if (!ids.Add(q.Id))
                    Fail($"question id '{q.Id}' is used twice.");
                if (!q.Path.StartsWith('/'))
                    Fail($"question '{q.Id}' has path '{q.Path}', which must start with '/'.");
                if (q.Answer == AnswerType.Choice && q.Options is not { Count: > 0 })
                    Fail($"choice question '{q.Id}' has no options.");
                if (q.Answer == AnswerType.Choice && q.Recommended is not null && !q.Options!.Contains(q.Recommended))
                    Fail($"question '{q.Id}' is recommended '{q.Recommended}', which isn't one of its options.");
                if (q.Default is not null && (q.Default != Kind.ScenarioNameDefault || q.Answer != AnswerType.Text))
                    Fail($"question '{q.Id}' defaults to '{q.Default}'; the only default is {Kind.ScenarioNameDefault}, on a text question.");
                if (q.Visibility is not null && !Kind.Visibilities.Contains(q.Visibility))
                    Fail($"question '{q.Id}' has visibility '{q.Visibility}'; use one of {string.Join(", ", Kind.Visibilities)}.");
            }
        }
        foreach (var draft in kind.Drafts ?? [])
        {
            if (draft.DueAfterStage < 0 || draft.DueAfterStage > kind.LastStage)
                Fail($"draft '{draft.Field}' is due after stage {draft.DueAfterStage}, which doesn't exist.");
            if (draft.Visibility is not null && !Kind.Visibilities.Contains(draft.Visibility))
                Fail($"draft '{draft.Field}' has visibility '{draft.Visibility}'; use one of {string.Join(", ", Kind.Visibilities)}.");
        }
    }
}
