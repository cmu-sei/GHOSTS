// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json;

namespace Ghosts.Api.Tests;

/// <summary>
/// The workflows provisioned into every n8n are configuration that ships, so they are checked like code: no
/// browser session pasted into a request, no NPC list that spans every run, no trigger that runs on its own
/// outside an execution, and Pandora reached through one setting.
/// </summary>
public class N8nWorkflowTests
{
    public static TheoryData<string> Workflows()
    {
        var data = new TheoryData<string>();
        foreach (var f in Directory.GetFiles(WorkflowDir(), "*.json").Order()) data.Add(Path.GetFileName(f));
        return data;
    }

    [Theory]
    [MemberData(nameof(Workflows))]
    public void No_request_carries_a_cookie_or_pasted_browser_headers(string file)
    {
        foreach (var node in Nodes(file).Where(IsHttpRequest))
        {
            var names = HeaderNames(node).Select(n => n.ToLowerInvariant()).ToList();
            Assert.DoesNotContain("cookie", names);
            Assert.DoesNotContain("user-agent", names);
            Assert.DoesNotContain(names, n => n.StartsWith("sec-"));
        }
    }

    [Theory]
    [MemberData(nameof(Workflows))]
    public void Every_npc_list_call_is_scoped_to_the_execution_that_triggered_it(string file)
    {
        foreach (var node in Nodes(file).Where(IsHttpRequest))
        {
            var url = Url(node);
            if (!url.Contains("/api/npcs/list")) continue;
            Assert.Contains("executionId=", url);
            Assert.Contains("query.executionId", url);
        }
    }

    [Theory]
    [MemberData(nameof(Workflows))]
    public void No_workflow_runs_on_its_own_schedule(string file)
    {
        Assert.DoesNotContain(Nodes(file), n => Type(n) == "n8n-nodes-base.scheduleTrigger");
    }

    [Theory]
    [MemberData(nameof(Workflows))]
    public void Pandora_is_reached_through_the_pandora_url_setting(string file)
    {
        foreach (var node in Nodes(file).Where(IsHttpRequest))
        {
            var url = Url(node);
            if (!(url.Contains("pandora", StringComparison.OrdinalIgnoreCase) || url.Contains(":8800") || url.Contains("/messages/send"))) continue;
            Assert.Contains("$env.PANDORA_URL", url);
        }
    }

    private static IEnumerable<JsonElement> Nodes(string file)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(WorkflowDir(), file)));
        return doc.RootElement.GetProperty("nodes").EnumerateArray().Select(n => n.Clone()).ToList();
    }

    private static string Type(JsonElement node) => node.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";

    private static bool IsHttpRequest(JsonElement node) => Type(node) == "n8n-nodes-base.httpRequest";

    private static string Url(JsonElement node) =>
        node.TryGetProperty("parameters", out var p) && p.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";

    private static IEnumerable<string> HeaderNames(JsonElement node)
    {
        if (!node.TryGetProperty("parameters", out var p) || !p.TryGetProperty("headerParameters", out var hp)
            || !hp.TryGetProperty("parameters", out var list)) return [];
        return list.EnumerateArray().Select(h => h.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "").ToList();
    }

    private static string WorkflowDir()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "configuration", "n8n-workflows");
            if (Directory.Exists(candidate)) return candidate;
        }
        throw new DirectoryNotFoundException("configuration/n8n-workflows not found above the test binaries");
    }
}
