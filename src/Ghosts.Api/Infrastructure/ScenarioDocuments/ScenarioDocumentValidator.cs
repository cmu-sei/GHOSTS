// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ghosts.Api.Infrastructure.ScenarioDocuments;

/// <summary>
/// Validates a scenario document against schema v1 by running the reference tool,
/// schemas/scenario-document/tools/scenario-doc.mjs. There is no JSON Schema library in the API's
/// dependency tree, and the tool is already the schema's reference implementation (ajv, draft
/// 2020-12), so a document cannot pass one validator and fail the other. Set
/// GHOSTS_SCENARIO_DOC_TOOL when the tool is not below the working directory.
/// </summary>
public static class ScenarioDocumentValidator
{
    private const string ToolPathVariable = "GHOSTS_SCENARIO_DOC_TOOL";
    private static readonly string[] ToolRelativePath = ["schemas", "scenario-document", "tools", "scenario-doc.mjs"];

    /// <summary>The validator's findings; empty when the document is valid.</summary>
    /// <exception cref="InvalidOperationException">The validator could not be run.</exception>
    public static async Task<IReadOnlyList<string>> ValidateAsync(string documentText, CancellationToken ct)
    {
        var tool = ResolveTool();
        var file = Path.Combine(Path.GetTempPath(), $"scenario-document-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(file, documentText, ct);

        try
        {
            var info = new ProcessStartInfo("node")
            {
                WorkingDirectory = Path.GetDirectoryName(tool),
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            info.ArgumentList.Add(tool);
            info.ArgumentList.Add("validate");
            info.ArgumentList.Add(file);

            using var process = Process.Start(info)
                ?? throw new InvalidOperationException("Scenario document validator could not be started");

            var stdout = await process.StandardOutput.ReadToEndAsync(ct);
            var stderr = await process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            if (process.ExitCode == 0) return [];

            // "FAIL  <file>" then one indented line per finding. The file line is dropped: it names
            // the temporary file, which is no use to the caller.
            var findings = stdout.Split('\n')
                .Where(l => l.StartsWith("      ", StringComparison.Ordinal))
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToList();

            return findings.Count > 0
                ? findings
                : throw new InvalidOperationException(
                    $"Scenario document validator failed (exit {process.ExitCode}): {stderr.Trim()}");
        }
        finally
        {
            File.Delete(file);
        }
    }

    private static string ResolveTool()
    {
        var configured = Environment.GetEnvironmentVariable(ToolPathVariable);
        if (!string.IsNullOrEmpty(configured))
        {
            return File.Exists(configured)
                ? configured
                : throw new InvalidOperationException($"{ToolPathVariable} does not exist: {configured}");
        }

        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
            {
                var candidate = Path.Combine([dir.FullName, .. ToolRelativePath]);
                if (File.Exists(candidate)) return candidate;
            }
        }

        throw new InvalidOperationException(
            $"Scenario document validator not found. Set {ToolPathVariable} to {string.Join("/", ToolRelativePath)}.");
    }
}
