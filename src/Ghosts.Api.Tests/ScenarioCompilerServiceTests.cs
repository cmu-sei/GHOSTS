// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json;
using Ghosts.Animator.Models;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Ghosts.Api.Tests;

/// <summary>
/// Compile is held to the import's standard: what it writes derives to a document that validated with 0
/// errors and is kept beside the rows, and a second compile adds nothing the first did.
/// </summary>
public class ScenarioCompilerServiceTests
{
    [Fact]
    public async Task Compiling_twice_adds_nothing_the_first_compile_added_and_keeps_a_validated_document()
    {
        await using var context = NewContext();
        var scenario = new Scenario { Name = "Payroll Week", Description = "A phishing drill against the payroll team." };
        context.Scenarios.Add(scenario);
        await context.SaveChangesAsync();
        context.ScenarioEntities.Add(new ScenarioEntity { ScenarioId = scenario.Id, Name = "Sandworm Team (emulated)", EntityType = "Organization" });
        context.ScenarioEnrichments.AddRange(
            new ScenarioEnrichment { ScenarioId = scenario.Id, EnrichmentType = "AttackTechnique", ExternalId = "T1566.002", Name = "Spearphishing Link", Description = "A link in mail." },
            new ScenarioEnrichment { ScenarioId = scenario.Id, EnrichmentType = "AttackTechnique", ExternalId = "T1059.001", Name = "PowerShell", Description = "Scripts on the host." });
        await context.SaveChangesAsync();
        var service = new ScenarioCompilerService(context, new ScenarioService(context), null!);
        var request = new CompileScenarioDto("Build 1", GenerateNpcs: false);

        var first = await service.CompileAsync(scenario.Id, request, default);
        var second = await service.CompileAsync(scenario.Id, request, default);

        Assert.Equal("Completed", first.Status);
        Assert.Equal(2, first.TimelineEventCount);
        Assert.Equal(2, first.InjectCount);
        Assert.Equal("Completed", second.Status);
        Assert.Equal(0, second.TimelineEventCount);
        Assert.Equal(0, second.InjectCount);

        var timeline = await context.ScenarioTimelines.SingleAsync(t => t.ScenarioId == scenario.Id);
        Assert.Equal(2, await context.ScenarioTimelineEvents.CountAsync(e => e.ScenarioTimelineId == timeline.Id));
        var parameters = await context.ScenarioParameters.SingleAsync(p => p.ScenarioId == scenario.Id);
        Assert.Equal(2, await context.Injects.CountAsync(i => i.ScenarioParametersId == parameters.Id));
        Assert.Equal(1, await context.Nations.CountAsync(n => n.ScenarioParametersId == parameters.Id));

        // Each compile kept the document its rows derive to, so GET document returns it, and it is not an import.
        var documents = await context.ScenarioDocuments.Where(d => d.ScenarioId == scenario.Id).ToListAsync();
        Assert.Equal(2, documents.Count);
        Assert.All(documents, d => Assert.Equal(ScenarioDocument.Compiled, d.Origin));
        Assert.All(documents, d => Assert.Equal(d.RowsHash, d.ContentHash));
        Assert.Equal("Compiled", (await context.Scenarios.SingleAsync(s => s.Id == scenario.Id)).BuilderStatus);
    }

    private static ApplicationDbContext NewContext() => new TestDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase($"compiler-{Guid.NewGuid()}")
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
