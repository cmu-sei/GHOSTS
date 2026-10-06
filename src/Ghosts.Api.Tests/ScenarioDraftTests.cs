// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json;
using System.Text.Json.Nodes;
using Ghosts.Animator.Models;
using Ghosts.Api.Controllers.Api;
using Ghosts.Api.Infrastructure;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ghosts.Api.Tests;

/// <summary>
/// Drafts and publishing (I2), and the approved version of an imported document (A5). The author comes
/// from the auth proxy's header; a draft is shown only to its author, published only by its author when its
/// document validates, and never run.
/// </summary>
public class ScenarioDraftTests
{
    [Fact]
    public void The_user_is_the_proxys_header_or_anonymous()
    {
        Assert.Equal("alice", User("alice").Name);
        Assert.Equal(CurrentUser.Anonymous, User(null).Name);

        var custom = new CurrentUser(Accessor("X-Remote-User", "bob"),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Identity:UserHeader"] = "X-Remote-User" }).Build());
        Assert.Equal("bob", custom.Name);
    }

    [Fact]
    public async Task A_new_scenario_is_its_authors_draft()
    {
        await using var context = NewContext();

        var scenario = await new ScenarioService(context, User("alice")).CreateAsync(new CreateScenarioDto("Draft", "d", null, null, null, null), default);

        Assert.Equal("alice", scenario.Author);
        Assert.Null(scenario.PublishedAt);
        Assert.True(scenario.IsVisibleTo("alice"));
        Assert.False(scenario.IsVisibleTo("bob"));
    }

    [Fact]
    public async Task The_list_shows_published_scenarios_and_the_callers_own_drafts()
    {
        await using var context = NewContext();
        context.Scenarios.AddRange(
            new Scenario { Id = 1, Name = "Published", Author = "bob", PublishedAt = DateTime.UtcNow },
            new Scenario { Id = 2, Name = "Alice's draft", Author = "alice" },
            new Scenario { Id = 3, Name = "Bob's draft", Author = "bob" });
        await context.SaveChangesAsync();

        var result = await Controller(context, "alice").GetScenarios(default);

        var list = Assert.IsAssignableFrom<IEnumerable<ScenarioDto>>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(["Alice's draft", "Published"], list.Select(s => s.Name).Order());
    }

    [Fact]
    public async Task Only_the_author_publishes_a_draft_and_only_once()
    {
        await using var context = NewContext();
        var draft = await new ScenarioService(context, User("alice")).ImportDocumentAsync(Document(), [], default);

        var byBob = await Controller(context, "bob").PublishScenario(draft.Id, default);
        Assert.Equal(403, Assert.IsType<ObjectResult>(byBob).StatusCode);

        var byAlice = await Controller(context, "alice").PublishScenario(draft.Id, default);
        var published = Assert.IsType<ScenarioDto>(Assert.IsType<OkObjectResult>(byAlice).Value);
        Assert.NotNull(published.PublishedAt);

        Assert.IsType<ConflictObjectResult>(await Controller(context, "alice").PublishScenario(draft.Id, default));
    }

    [Fact]
    public async Task A_draft_cannot_be_run()
    {
        await using var context = NewContext();
        context.Scenarios.Add(new Scenario { Id = 1, Name = "Unreviewed", Author = "alice" });
        await context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ExecutionService(context, null!, null!).CreateAsync(new CreateExecutionDto(1, null, null, null, null), default));

        Assert.Contains("is a draft", ex.Message);
    }

    [Fact]
    public async Task The_approved_document_is_kept_and_an_edit_since_is_listed()
    {
        await using var context = NewContext();
        var service = new ScenarioService(context);
        var scenario = await service.ImportDocumentAsync(Document(), [], default);

        var approved = await service.ApprovedDocumentAsync(scenario.Id, default);
        Assert.False(approved.Edited);
        Assert.Empty(approved.Changes);
        Assert.Equal("Phishing Drill: First Contact", JsonNode.Parse(approved.Document)!["name"]!.GetValue<string>());

        var row = await context.Scenarios.SingleAsync();
        row.Name = "Renamed in the planner";
        await context.SaveChangesAsync();

        approved = await service.ApprovedDocumentAsync(scenario.Id, default);
        Assert.True(approved.Edited);
        Assert.Contains("/name: Phishing Drill: First Contact → Renamed in the planner", approved.Changes);
        Assert.Equal("Phishing Drill: First Contact", JsonNode.Parse(approved.Document)!["name"]!.GetValue<string>());
    }

    private static ScenariosController Controller(ApplicationDbContext context, string user) =>
        new(new ScenarioService(context, User(user)), null!, null!, null!, User(user), NullLogger<ScenariosController>.Instance);

    private static CurrentUser User(string? name) =>
        new(Accessor("X-Forwarded-User", name), new ConfigurationBuilder().Build());

    private static IHttpContextAccessor Accessor(string header, string? value)
    {
        var http = new DefaultHttpContext();
        if (value != null) http.Request.Headers[header] = value;
        return new HttpContextAccessor { HttpContext = http };
    }

    /// <summary>The schema's own example that validates clean.</summary>
    private static JsonObject Document()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "schemas", "scenario-document", "examples", "phishing-drill.scenario.json");
            if (File.Exists(path)) return JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        }
        throw new FileNotFoundException("schemas/scenario-document/examples/phishing-drill.scenario.json");
    }

    private static ApplicationDbContext NewContext() => new TestDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase($"drafts-{Guid.NewGuid()}")
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
        .Options);

    /// <summary>As in ScenarioDocumentStorageTests: NpcProfile is jsonb, which only Npgsql maps.</summary>
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
