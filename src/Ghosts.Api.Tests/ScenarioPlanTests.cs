// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json.Nodes;
using Ghosts.Api.Infrastructure.ScenarioDocuments;

namespace Ghosts.Api.Tests;

/// <summary>
/// The exercise plan the API renders from a document (B4) is the one the schema tool renders:
/// <c>examples/*.plan.md</c> are <c>scenario-doc.mjs render</c>'s output for each example, and the port must
/// match them byte for byte. Regenerate them with the tool when the renderer changes.
/// </summary>
public class ScenarioPlanTests
{
    [Theory]
    [InlineData("meridian-hybrid")]
    [InlineData("operation-overlord")]
    [InlineData("phishing-drill")]
    [InlineData("soc-morning")]
    public void The_plan_matches_what_the_schema_tool_renders(string example)
    {
        var document = JsonNode.Parse(File.ReadAllText(Path.Combine(Examples, $"{example}.scenario.json")))!.AsObject();
        var expected = File.ReadAllText(Path.Combine(Examples, $"{example}.plan.md"));

        var plan = ScenarioPlan.Render(document, $"`{example}.scenario.json`");

        Assert.Equal(expected, plan);
    }

    private static readonly string Examples = Find();

    private static string Find()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "schemas", "scenario-document", "examples");
            if (Directory.Exists(candidate)) return candidate;
        }
        throw new DirectoryNotFoundException("schemas/scenario-document/examples is not above the test binary");
    }
}
