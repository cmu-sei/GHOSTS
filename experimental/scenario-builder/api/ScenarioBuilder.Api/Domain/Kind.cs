// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

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

/// <summary>A stage of the interview. <see cref="Purpose"/> says what it settles; the model drafts its questions from it.</summary>
public record KindStage(int Number, string Title, List<KindQuestion> Questions, KindGate Gate, string? Purpose = null);

/// <summary>Text the agent drafts once every question of <see cref="DueAfterStage"/> is answered.</summary>
public record KindDraft(string Field, int DueAfterStage, string Path, string? Visibility = null);

/// <summary>
/// A type of scenario: its stages, questions, gates and drafts, as a <c>*.kind.json</c> file holds it.
/// <see cref="Topology"/> gives its scenarios a Topology page, where the range is built and deployed.
/// <see cref="Description"/> says what kind of exercise it is.
/// </summary>
public partial record Kind(string Id, string Version, string Name, List<KindStage> Stages, List<KindDraft> Drafts, bool Topology = false, string? Description = null)
{
    /// <summary>Who may see a piece of a scenario: everyone, exercise control and designers, or designers only.</summary>
    public static readonly string[] Visibilities = ["participant", "excon", "designer"];

    /// <summary>The one default a question can have: a new scenario answers it with the scenario's own name.</summary>
    public const string ScenarioNameDefault = "scenarioName";

    [JsonIgnore]
    public IEnumerable<KindQuestion> AllQuestions => Stages.SelectMany(s => s.Questions);

    [JsonIgnore]
    public int LastStage => Stages[^1].Number;

    public (KindQuestion Question, int Stage)? Find(string questionId)
    {
        foreach (var stage in Stages)
        foreach (var question in stage.Questions)
            if (question.Id == questionId)
                return (question, stage.Number);
        return null;
    }

    /// <summary>
    /// The kind as <paramref name="id"/> at <paramref name="version"/>, with what a person or the model may leave out
    /// filled in: stages numbered in order, an empty gate, a question's id from its wording, an empty why, a label
    /// from the id, and a place in the document under <c>/scenario</c>.
    /// </summary>
    public Kind Filled(string id, string version)
    {
        var stages = (Stages ?? []).Where(s => s is not null).ToList();
        var taken = stages.SelectMany(s => s.Questions ?? []).Where(q => !string.IsNullOrWhiteSpace(q?.Id)).Select(q => q.Id).ToHashSet();
        string IdFor(KindQuestion q)
        {
            if (!string.IsNullOrWhiteSpace(q.Id))
                return q.Id;
            var stem = IdOf(q.Text);
            var made = stem;
            for (var n = 2; !taken.Add(made); n++)
                made = $"{stem}{n}";
            return made;
        }

        return this with
        {
            Id = id,
            Version = version,
            Name = Name?.Trim() ?? "",
            Stages = stages.Select((s, i) => s with
            {
                Number = i,
                Gate = new KindGate(string.IsNullOrWhiteSpace(s.Gate?.Approval) ? null : s.Gate.Approval.Trim()),
                Questions = (s.Questions ?? []).Where(q => q is not null).Select(q =>
                {
                    var questionId = IdFor(q);
                    return q with
                    {
                        Id = questionId,
                        Why = q.Why ?? "",
                        Label = string.IsNullOrWhiteSpace(q.Label) ? LabelOf(questionId) : q.Label,
                        Path = string.IsNullOrWhiteSpace(q.Path) ? $"/scenario/{questionId}" : q.Path
                    };
                }).ToList()
            }).ToList(),
            Drafts = Drafts ?? []
        };
    }

    /// <summary>A question id from its wording: "What does the team attack first?" is teamAttackFirst.</summary>
    public static string IdOf(string? text)
    {
        var words = Words().Matches(text ?? "").Select(m => m.Value.ToLowerInvariant()).Where(w => !QuestionWords.Contains(w)).Take(3).ToList();
        return words.Count == 0 ? "question" : words[0] + string.Concat(words.Skip(1).Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
    }

    private static readonly string[] QuestionWords =
    [
        "what", "which", "who", "whom", "whose", "how", "when", "where", "why", "does", "do", "did", "is", "are", "was", "were", "will",
        "would", "should", "can", "could", "the", "a", "an", "of", "to", "in", "on", "for", "and", "or", "with", "this", "that", "it",
        "its", "be", "has", "have", "there", "their", "they", "you", "your", "we", "our"
    ];

    /// <summary>A question id as a Title Case label: rulesOfEngagement is "Rules of Engagement".</summary>
    public static string LabelOf(string? id)
    {
        var words = Words().Matches(id ?? "").Select(m => m.Value.ToLowerInvariant()).ToList();
        return string.Join(' ', words.Select((w, i) => i > 0 && SmallWords.Contains(w) ? w : char.ToUpperInvariant(w[0]) + w[1..]));
    }

    private static readonly string[] SmallWords = ["a", "an", "and", "as", "at", "by", "for", "in", "of", "on", "or", "the", "to"];

    [GeneratedRegex("[A-Z]?[a-z]+|[A-Z]+(?![a-z])|[0-9]+")]
    private static partial Regex Words();
}

/// <summary>
/// The published versions of every kind, which scenarios run on. At startup it's filled from the database, and
/// publishing adds to it. It can also be read straight from a directory of kind files, each its kind's one version.
/// </summary>
public sealed partial class KindCatalog
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly Lock _gate = new();
    private readonly List<string> _order = [];
    private readonly Dictionary<string, List<Kind>> _versions = [];
    private readonly HashSet<string> _retired = [];

    public KindCatalog()
    {
    }

    /// <summary>Every kind file in <paramref name="directory"/>. A broken file throws, naming it and what's wrong.</summary>
    public KindCatalog(string directory)
    {
        foreach (var kind in ReadDirectory(directory))
            Add(kind);
    }

    public static List<Kind> ReadDirectory(string directory) =>
        Directory.GetFiles(directory, "*.kind.json").Order().Select(file => Read(File.ReadAllText(file), Path.GetFileName(file))).ToList();

    /// <summary>A kind file's text, which must keep every rule; otherwise it throws, naming <paramref name="source"/>.</summary>
    public static Kind Read(string json, string source)
    {
        var kind = JsonSerializer.Deserialize<Kind>(json, Json) ?? throw new InvalidOperationException($"{source}: empty kind file.");
        if (Problem(kind) is { } problem)
            throw new InvalidOperationException($"{source}: {problem}");
        return kind;
    }

    /// <summary>The latest version of every kind, retired ones too, in the order they were added.</summary>
    public IReadOnlyList<Kind> All
    {
        get { lock (_gate) return _order.Select(id => _versions[id][^1]).ToList(); }
    }

    /// <summary>What a new scenario can be made from: the latest version of every kind that isn't retired.</summary>
    public IReadOnlyList<Kind> Offered
    {
        get { lock (_gate) return _order.Where(id => !_retired.Contains(id)).Select(id => _versions[id][^1]).ToList(); }
    }

    public bool IsRetired(string id)
    {
        lock (_gate) return _retired.Contains(id);
    }

    /// <summary>A kind's latest version.</summary>
    public Kind? Find(string id)
    {
        lock (_gate) return _versions.TryGetValue(id, out var versions) ? versions[^1] : null;
    }

    /// <summary>One version of a kind: the one a scenario was created on.</summary>
    public Kind? Find(string id, string version)
    {
        lock (_gate) return _versions.TryGetValue(id, out var versions) ? versions.FirstOrDefault(k => k.Version == version) : null;
    }

    /// <summary>A newly published version, which becomes its kind's latest.</summary>
    public void Add(Kind kind)
    {
        lock (_gate)
        {
            if (!_versions.TryGetValue(kind.Id, out var versions))
            {
                _versions[kind.Id] = versions = [];
                _order.Add(kind.Id);
            }
            versions.Add(kind);
        }
    }

    public void SetRetired(string id, bool retired)
    {
        lock (_gate)
        {
            if (retired)
                _retired.Add(id);
            else
                _retired.Remove(id);
        }
    }

    /// <summary>
    /// Null when the kind keeps every rule; otherwise the first rule it breaks. An unfinished draft may have no
    /// stages, or stages with no questions yet; a published kind may not.
    /// </summary>
    public static string? Problem(Kind kind, bool finished = true)
    {
        if (kind.Id is null || !KindId().IsMatch(kind.Id))
            return $"The scenario type id \"{kind.Id}\" must be lower-case words joined by hyphens.";
        if (string.IsNullOrWhiteSpace(kind.Name))
            return "Give the scenario type a name.";
        if (kind.Stages is null || (finished && kind.Stages.Count == 0))
            return "a scenario type needs at least one stage.";
        var ids = new HashSet<string>();
        var draftPaths = (kind.Drafts ?? []).Where(d => d is not null).Select(d => d.Path).ToHashSet();
        for (var i = 0; i < kind.Stages.Count; i++)
        {
            var stage = kind.Stages[i];
            if (stage is null)
                return $"stage {i} is empty.";
            if (stage.Number != i)
                return $"stages must be numbered 0, 1, 2… in order; found {stage.Number} at position {i}.";
            if (string.IsNullOrWhiteSpace(stage.Title))
                return $"stage {i} has no title.";
            if (stage.Gate is null)
                return $"stage {i} has no gate; use {{}} for none.";
            if (stage.Questions is null || (finished && stage.Questions.Count == 0))
                return $"stage {i} ({stage.Title}) has no questions.";
            foreach (var q in stage.Questions)
            {
                if (q?.Id is null || !QuestionId().IsMatch(q.Id))
                    return $"question id '{q?.Id}' in stage {i} must be letters and digits, starting with a lower-case letter.";
                if (!ids.Add(q.Id))
                    return $"question id '{q.Id}' is used twice.";
                if (string.IsNullOrWhiteSpace(q.Text))
                    return $"question '{q.Id}' has no text.";
                if (q.Path is null || !q.Path.StartsWith('/'))
                    return $"question '{q.Id}' has path '{q.Path}', which must start with '/'.";
                if (draftPaths.Contains(q.Path))
                    return $"question '{q.Id}' has path '{q.Path}', where a drafted field goes.";
                if (!Enum.IsDefined(q.Answer))
                    return $"question '{q.Id}' has the answer type {(int)q.Answer}; use text, choice or list.";
                if (q.Answer == AnswerType.Choice && q.Options is not { Count: > 0 })
                    return $"choice question '{q.Id}' has no options.";
                if (q.Answer == AnswerType.Choice && q.Recommended is not null && !q.Options!.Contains(q.Recommended))
                    return $"question '{q.Id}' is recommended '{q.Recommended}', which isn't one of its options.";
                if (q.Default is not null && (q.Default != Kind.ScenarioNameDefault || q.Answer != AnswerType.Text))
                    return $"question '{q.Id}' defaults to '{q.Default}'; the only default is {Kind.ScenarioNameDefault}, on a text question.";
                if (q.Visibility is not null && !Kind.Visibilities.Contains(q.Visibility))
                    return $"question '{q.Id}' has visibility '{q.Visibility}'; use one of {string.Join(", ", Kind.Visibilities)}.";
            }
        }
        foreach (var draft in kind.Drafts ?? [])
        {
            if (draft?.Field is null || draft.Path is null || !draft.Path.StartsWith('/'))
                return $"draft '{draft?.Field}' needs a field and a path starting with '/'.";
            if (draft.DueAfterStage < 0 || draft.DueAfterStage >= kind.Stages.Count)
                return $"draft '{draft.Field}' is due after stage {draft.DueAfterStage}, which doesn't exist.";
            if (draft.Visibility is not null && !Kind.Visibilities.Contains(draft.Visibility))
                return $"draft '{draft.Field}' has visibility '{draft.Visibility}'; use one of {string.Join(", ", Kind.Visibilities)}.";
        }
        return null;
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex KindId();

    [GeneratedRegex("^[a-z][A-Za-z0-9]*$")]
    private static partial Regex QuestionId();
}
