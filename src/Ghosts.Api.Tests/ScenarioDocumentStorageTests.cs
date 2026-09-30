// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json;
using System.Text.Json.Nodes;
using Ghosts.Animator.Models;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.ScenarioDocuments;
using Ghosts.Api.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Ghosts.Api.Tests;

/// <summary>
/// The document kept beside the rows. One row per import, so a scenario keeps its history and the
/// newest row is the document the export returns. In-memory rather than Postgres: nothing here is
/// Postgres-specific, and a test that needs a database is a test that does not run.
/// </summary>
public class ScenarioDocumentStorageTests
{
    [Fact]
    public async Task A_second_import_adds_a_row_and_the_export_returns_the_newer_one()
    {
        await using var context = NewContext();
        context.Scenarios.Add(new Scenario { Id = 1, Name = "Held Document", Description = "d" });
        await context.SaveChangesAsync();

        var service = new ScenarioService(context);
        var first = Document("first");
        var second = Document("second");

        await service.StoreDocumentAsync(1, first, [], CancellationToken.None);
        await service.StoreDocumentAsync(1, second,
            [ScenarioFinding.Warn(4, "STORAGE_LOSSY", "/catalog", "No column holds this.")], CancellationToken.None);

        var rows = context.ScenarioDocuments.Where(d => d.ScenarioId == 1).OrderBy(d => d.Id).ToList();
        Assert.Equal(2, rows.Count);
        Assert.NotEqual(rows[0].ContentHash, rows[1].ContentHash);

        // The newest row is the current document, and the older one is still there.
        var exported = await service.ExportDocumentAsync(1, false, CancellationToken.None);
        Assert.Equal(ScenarioDocumentMapper.Serialize(second), exported);
        Assert.Contains("\"second\"", exported);
        Assert.Contains("STORAGE_LOSSY", rows[1].Validation);
    }

    [Fact]
    public async Task A_scenario_with_no_document_row_exports_the_derived_form()
    {
        await using var context = NewContext();
        context.Scenarios.Add(new Scenario { Id = 1, Name = "Rows Only", Description = "d" });
        await context.SaveChangesAsync();

        var exported = await new ScenarioService(context).ExportDocumentAsync(1, false, CancellationToken.None);

        Assert.Contains("\"slug\": \"rows-only\"", exported);
    }

    [Fact]
    public async Task An_edit_after_the_document_was_stored_makes_the_export_return_the_rows()
    {
        await using var context = NewContext();
        context.Scenarios.Add(new Scenario { Id = 1, Name = "Held Document", Description = "d" });
        await context.SaveChangesAsync();

        var service = new ScenarioService(context);
        await service.StoreDocumentAsync(1, Document("stored"), [], CancellationToken.None);
        Assert.Contains("\"stored\"", await service.ExportDocumentAsync(1, false, CancellationToken.None));

        // An edit through any other path than import leaves the stored document behind.
        var scenario = await context.Scenarios.FindAsync(1);
        scenario!.Name = "Edited In The Builder";
        await context.SaveChangesAsync();

        var exported = await service.ExportDocumentAsync(1, false, CancellationToken.None);
        Assert.Equal(await service.ExportDocumentAsync(1, true, CancellationToken.None), exported);
        Assert.Contains("Edited In The Builder", exported);
    }

    private static JsonObject Document(string name) => new()
    {
        ["schemaVersion"] = ScenarioDocumentMapper.SchemaVersion,
        ["slug"] = "held-document",
        ["name"] = name,
        ["description"] = "A document the columns are not asked to rebuild."
    };

    private static ApplicationDbContext NewContext() => new TestDbContext(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"scenario-documents-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    /// <summary>
    /// The application's context with one thing changed: NpcRecord.NpcProfile is an object mapped
    /// straight to jsonb, which only the Npgsql provider can do, and it is the one property that stops
    /// the model from loading anywhere else. Stored as text here so the rest of the model — including
    /// scenarios and their documents — can be exercised without a database.
    /// </summary>
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
