// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

namespace ScenarioBuilder.Api.Domain;

/// <summary>Stated: a person gave it, and only a proposal can change it. Proposed: the agent chose it.</summary>
public enum Who { Stated, Proposed }

public enum ProposalStatus { Pending, Applied, Discarded }

public enum SourceKind { Text, Url, File }

public class Scenario
{
    public int Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ScenarioTypeId { get; set; } = string.Empty;
    public string ScenarioTypeVersion { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;

    /// <summary>The open stage. Questions of later stages can't be answered yet.</summary>
    public int Stage { get; set; }

    /// <summary>Drafted fields, such as executiveSummary, as a JSON object of strings.</summary>
    public string Drafts { get; set; } = "{}";

    /// <summary>
    /// The owner handed the agent every remaining decision.
    /// Only the owner, as a person, turns it on or off.
    /// </summary>
    public bool Delegated { get; set; }

    /// <summary>Hidden from the scenario list. Nothing else changes; the pull API still serves it.</summary>
    public bool Archived { get; set; }

    public DateTime Created { get; set; }
    public DateTime Updated { get; set; }
}

/// <summary>One answered question. Open questions have no row.</summary>
public class Decision
{
    public int Id { get; set; }
    public int ScenarioId { get; set; }
    public string QuestionId { get; set; } = string.Empty;
    public int Stage { get; set; }

    /// <summary>The answer as JSON: a string, or an array of strings.</summary>
    public string Value { get; set; } = "null";

    public Who Who { get; set; }
    public string DecidedBy { get; set; } = string.Empty;
    public Via Via { get; set; }
    public string? Reason { get; set; }

    /// <summary>The person's own words, when the agent recorded a stated answer from them.</summary>
    public string? Quote { get; set; }

    public DateTime Time { get; set; }
}

/// <summary>A suggested change to a stated answer, waiting for the owner.</summary>
public class Proposal
{
    public int Id { get; set; }
    public int ScenarioId { get; set; }
    public string QuestionId { get; set; } = string.Empty;
    public string? Before { get; set; }
    public string After { get; set; } = "null";
    public string Reason { get; set; } = string.Empty;
    public string ProposedBy { get; set; } = string.Empty;
    public Via Via { get; set; }
    public ProposalStatus Status { get; set; }
    public DateTime Created { get; set; }
    public DateTime? Decided { get; set; }
}

/// <summary>Every change to an answer or a drafted field, so history survives an overwrite.</summary>
public class Edit
{
    public int Id { get; set; }
    public int ScenarioId { get; set; }

    /// <summary>A question id, or a drafted field's name.</summary>
    public string Target { get; set; } = string.Empty;

    public string? Before { get; set; }
    public string After { get; set; } = "null";
    public string By { get; set; } = string.Empty;
    public Via Via { get; set; }
    public DateTime Time { get; set; }
}

/// <summary>A named set of document templates for some scenario types. Edits go to the draft; publishing makes a version.</summary>
public class TemplateLibrary
{
    public int Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>The scenario type ids it serves, as a JSON array.</summary>
    public string ScenarioTypes { get; set; } = "[]";

    /// <summary>The draft's templates, as a JSON array of <see cref="TemplateSpec"/>.</summary>
    public string Draft { get; set; } = "[]";

    public DateTime Created { get; set; }
    public DateTime Updated { get; set; }
}

/// <summary>One published version of a library, kept whole as its <c>.library.json</c> file.</summary>
public class LibraryVersion
{
    public int Id { get; set; }
    public int LibraryId { get; set; }
    public int Number { get; set; }
    public string Content { get; set; } = "{}";
    public string PublishedBy { get; set; } = string.Empty;
    public DateTime Published { get; set; }
}

/// <summary>
/// A scenario type as admins manage it: the draft they edit, the scenario type it was copied from, and whether new scenarios may still
/// use it. Its published versions are <see cref="PublishedScenarioType"/> rows; scenarios only ever run on those.
/// </summary>
public class ScenarioTypeRecord
{
    public int Id { get; set; }

    /// <summary>The scenario type's id, such as crucible-tabletop. It never changes.</summary>
    public string ScenarioTypeId { get; set; } = string.Empty;

    /// <summary>The draft, as the scenario type's JSON. Its version is the latest published one, or 0.0.0 before the first publish.</summary>
    public string Draft { get; set; } = "{}";

    public string? BasedOn { get; set; }

    /// <summary>Hidden from New scenario. Scenarios already on it carry on.</summary>
    public bool Retired { get; set; }

    public DateTime Created { get; set; }

    public DateTime Updated { get; set; }
}

/// <summary>One published version of a scenario type, kept whole as its <c>.scenario-type.json</c> file.</summary>
public class PublishedScenarioType
{
    public int Id { get; set; }

    public string ScenarioTypeId { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string Content { get; set; } = "{}";

    public string PublishedBy { get; set; } = string.Empty;

    public DateTime Published { get; set; }
}

/// <summary>Context the person gave for a scenario: pasted text, a fetched page, or a file's text. The file itself isn't kept.</summary>
public class Source
{
    public int Id { get; set; }
    public int ScenarioId { get; set; }
    public SourceKind Kind { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>The URL or the file's name; none for pasted text.</summary>
    public string? Origin { get; set; }

    public string Text { get; set; } = string.Empty;
    public string AddedBy { get; set; } = string.Empty;
    public DateTime Added { get; set; }
}

/// <summary>Where a plugin's app is: its API's address, set by an admin. No row means it isn't connected.</summary>
public class PluginConnection
{
    public int Id { get; set; }
    public string Plugin { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string ConnectedBy { get; set; } = string.Empty;
    public DateTime Connected { get; set; }
}

/// <summary>The one GraySpace scenarios deploy to, set by an admin. No row means it isn't connected.</summary>
public class GraySpaceConnection
{
    public int Id { get; set; }

    /// <summary>GraySpace's address, without /api/v1.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>GraySpace's GRAYSPACE_API_TOKEN, sent as Authorization: Bearer. It never leaves the API.</summary>
    public string Token { get; set; } = string.Empty;

    public string ConnectedBy { get; set; } = string.Empty;
    public DateTime Connected { get; set; }
}

/// <summary>A scenario's enclave on GraySpace: the slice spec being built, and what the page last did with it there.</summary>
public class Enclave
{
    public int Id { get; set; }
    public int ScenarioId { get; set; }

    /// <summary>The slice spec (GraySpace's grayspace-slice-v1) as JSON, as last written.</summary>
    public string Spec { get; set; } = "{}";

    /// <summary>The version GraySpace stored on the last save or reuse; none before.</summary>
    public string? Version { get; set; }

    /// <summary>The last instance created from it.</summary>
    public string? InstanceId { get; set; }

    /// <summary>The last job started from the page: a plan, a create, a destroy or a retry.</summary>
    public string? JobId { get; set; }

    public string UpdatedBy { get; set; } = string.Empty;
    public DateTime Updated { get; set; }
}
