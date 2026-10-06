// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json;
using System.Text.Json.Nodes;
using Ghosts.Animator.Models;
using Ghosts.Api.Controllers.Api;
using Ghosts.Api.Infrastructure;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.ScenarioDocuments;
using Ghosts.Api.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Serialization;

namespace Ghosts.Api.Tests;

/// <summary>
/// What a document says that no column holds is kept in the rows' extras, so the rows alone rebuild the
/// document, and an edit in the scenario screens — which saves every row again — keeps it.
/// </summary>
public class ScenarioExtrasTests
{
    [Theory]
    [InlineData("meridian-hybrid.scenario.json")]
    [InlineData("operation-overlord.scenario.json")]
    [InlineData("phishing-drill.scenario.json")]
    [InlineData("soc-morning.scenario.json")]
    public async Task The_rows_alone_rebuild_every_example(string file)
    {
        var document = Example(file);
        await using var context = NewContext();
        var service = new ScenarioService(context);

        var scenario = await service.ImportDocumentAsync(document, [], CancellationToken.None);

        Assert.Equal(ScenarioDocumentMapper.Serialize(document),
            await service.ExportDocumentAsync(scenario.Id, true, CancellationToken.None));
    }

    /// <summary>
    /// The planner reads the scenario through the API and saves it back through PUT (Newtonsoft) or the
    /// hub (System.Text.Json). Either way every row is written again, and the extras must come with them.
    /// </summary>
    [Theory]
    [InlineData("put")]
    [InlineData("hub")]
    public async Task A_save_from_the_scenario_screens_keeps_what_no_column_holds(string path)
    {
        var document = Example("meridian-hybrid.scenario.json");
        await using var context = NewContext();
        var service = new ScenarioService(context);
        var scenario = await service.ImportDocumentAsync(document, [], CancellationToken.None);

        var read = Assert.IsType<OkObjectResult>((await Controller(context).GetScenario(scenario.Id, default)).Result).Value;
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(read, AspNetNewtonsoft);
        var update = path == "put"
            ? Newtonsoft.Json.JsonConvert.DeserializeObject<UpdateScenarioDto>(json, AspNetNewtonsoft)!
            : JsonSerializer.Deserialize<UpdateScenarioDto>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        await service.UpdateAsync(scenario.Id, update with { Name = "Edited" }, CancellationToken.None);

        document["name"] = "Edited";
        Assert.Equal(ScenarioDocumentMapper.Serialize(document),
            await service.ExportDocumentAsync(scenario.Id, true, CancellationToken.None));
    }

    /// <summary>
    /// An example as the test imports it. Two examples state durations the hour columns cannot hold, which
    /// import refuses, so they are given whole hours. Every document-only path no example uses is added, so
    /// each one is exercised.
    /// </summary>
    private static JsonObject Example(string file)
    {
        var document = JsonNode.Parse(File.ReadAllText(Path.Combine(Schemas, "examples", file)))!.AsObject();
        if (!ScenarioDocumentMapper.TryDurationHours(document["rulesOfPlay"]?["duration"]?.GetValue<string>(), out _))
            document["rulesOfPlay"]!["duration"] = "2h";

        var terrain = document["terrain"] as JsonObject ?? new JsonObject();
        document["terrain"] = terrain;
        terrain["reference"] = new JsonObject { ["provider"] = "topomojo", ["slice"] = "corp-base" };
        terrain["segments"] = new JsonArray(new JsonObject { ["name"] = "corp", ["cidr"] = "10.0.0.0/24", ["description"] = "Office LAN" });
        terrain["services"] = new JsonArray(new JsonObject { ["name"] = "mail", ["description"] = "Exchange", ["hosts"] = new JsonArray("mx01") });
        terrain["defenses"] = new JsonArray(new JsonObject { ["name"] = "EDR", ["description"] = "On every workstation", ["covers"] = new JsonArray("corp") });
        terrain["vulnerabilities"] = new JsonArray(new JsonObject
            { ["asset"] = "mx01", ["cve"] = "CVE-2021-26855", ["description"] = "ProxyLogon, unpatched", ["severity"] = "critical" });

        document["population"] = new JsonObject
        {
            ["pools"] = new JsonArray(new JsonObject { ["role"] = "Finance", ["count"] = 4, ["description"] = "Accounts payable" })
        };

        var rop = document["rulesOfPlay"]!.AsObject();
        rop["escalationLadder"] = new JsonObject
        {
            ["summary"] = "Three rungs.",
            ["rungs"] = new JsonArray(new JsonObject { ["name"] = "Foothold", ["description"] = "One host", ["recoverable"] = true })
        };

        // Export lists injects (a title, no description) before the other events, so the new inject goes after the last one.
        var events = document["timeline"]!["events"]!.AsArray();
        events.Insert(events.Count(e => e!["title"] != null && e["description"] == null), new JsonObject
        {
            ["id"] = "board-call", ["at"] = "T+20m", ["owner"] = "red-team", ["title"] = "The board calls",
            ["expectedResponse"] = "Brief the board", ["indicators"] = new JsonArray("Phone rings"),
            ["effects"] = new JsonObject { ["setFlags"] = new JsonArray("board_briefed") }
        });
        events.Add(new JsonObject
        {
            ["id"] = "titled-event", ["at"] = "T+30m", ["owner"] = "white-cell", ["title"] = "A titled event",
            ["description"] = "An event with both a title and a description"
        });

        var objectives = document["assessment"]?["objectives"] as JsonArray;
        if (objectives is { Count: > 0 }) objectives[0]!["metWhen"] = "flag:board_briefed";

        document["sources"] = new JsonArray(new JsonObject { ["id"] = "brief", ["name"] = "Exercise brief", ["type"] = "document" });
        document["references"] = new JsonArray(new JsonObject { ["id"] = "nist", ["title"] = "NIST 800-61", ["locator"] = "§3.2" });
        return document;
    }

    private static readonly Newtonsoft.Json.JsonSerializerSettings AspNetNewtonsoft = new()
    {
        ContractResolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy() }
    };

    private static readonly string Schemas = Find();

    private static string Find()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "schemas", "scenario-document");
            if (Directory.Exists(candidate)) return candidate;
        }
        throw new DirectoryNotFoundException("schemas/scenario-document is not above the test binary");
    }

    private static ScenariosController Controller(ApplicationDbContext context)
    {
        var user = new CurrentUser(new HttpContextAccessor { HttpContext = new DefaultHttpContext() }, new ConfigurationBuilder().Build());
        return new(new ScenarioService(context, user), null!, null!, null!, user, NullLogger<ScenariosController>.Instance);
    }

    private static ApplicationDbContext NewContext() => new TestDbContext(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"scenario-extras-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    /// <summary>NpcRecord.NpcProfile maps to jsonb only under Npgsql; stored as text here (see ScenarioDocumentStorageTests).</summary>
    private class TestDbContext(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<NpcRecord>().Property(n => n.NpcProfile).HasConversion(
                profile => JsonSerializer.Serialize(profile, (JsonSerializerOptions?)null),
                text => JsonSerializer.Deserialize<NpcProfile>(text, (JsonSerializerOptions?)null)!);
        }
    }
}
