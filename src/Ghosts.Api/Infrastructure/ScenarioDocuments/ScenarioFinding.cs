// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;
using System.Linq;

namespace Ghosts.Api.Infrastructure.ScenarioDocuments;

/// <summary>
/// One thing the validator found in a scenario document. The validator writes nothing and decides
/// nothing: a caller reads the findings and an import refuses on any of severity "error".
/// </summary>
/// <param name="Tier">
/// Which tier produced it: 1 schema, 2 referential, 3 doctrinal/semantic (model-assisted, advisory,
/// not built), 4 dry run, 5 deployed verification (not built).
/// </param>
/// <param name="Severity">"error" blocks an import; "warning" and "info" do not.</param>
/// <param name="Code">Stable identifier for the check, e.g. REF_UNKNOWN_ENTITY. Callers match on this.</param>
/// <param name="Path">JSON pointer to the offending value, e.g. /edges/3/from. "" is the document.</param>
/// <param name="Message">What is wrong, in the exercise developer's words.</param>
/// <param name="Hint">What to do about it, where there is one useful thing to say.</param>
public record ScenarioFinding(
    int Tier,
    string Severity,
    string Code,
    string Path,
    string Message,
    string Hint = null)
{
    public const string Error = "error";
    public const string Warning = "warning";
    public const string Info = "info";

    public static ScenarioFinding Err(int tier, string code, string path, string message, string hint = null) =>
        new(tier, Error, code, path, message, hint);

    public static ScenarioFinding Warn(int tier, string code, string path, string message, string hint = null) =>
        new(tier, Warning, code, path, message, hint);

    public static ScenarioFinding Note(int tier, string code, string path, string message, string hint = null) =>
        new(tier, Info, code, path, message, hint);
}

/// <summary>What one run of the validator found. Ordered by tier, then by path.</summary>
public record ScenarioValidationResult(IReadOnlyList<ScenarioFinding> Findings)
{
    /// <summary>True when nothing of severity "error" was found — the only question an import asks.</summary>
    public bool IsValid => !Findings.Any(f => f.Severity == ScenarioFinding.Error);

    public int Errors => Findings.Count(f => f.Severity == ScenarioFinding.Error);
    public int Warnings => Findings.Count(f => f.Severity == ScenarioFinding.Warning);
}
