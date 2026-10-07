// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Ghosts.Animator.Models;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Ghosts.Api.Tests;

/// <summary>
/// Extraction calls the scenario's Builder model, the one its conversation uses, and files what it returns as
/// entities that remember the chunk they came from.
/// </summary>
public class ScenarioExtractionServiceTests
{
    [Fact]
    public async Task Extraction_calls_the_scenarios_model_and_files_its_entities_against_the_chunk()
    {
        await using var context = NewContext();
        var source = new ScenarioSource { ScenarioId = 1, Name = "Network notes", Chunks = [new ScenarioSourceChunk { ScenarioId = 1, Content = "MER-FS01 is the file server on the corp segment." }] };
        context.Scenarios.Add(new Scenario { Id = 1, Name = "Mine", BuilderModel = "picked-model", Sources = [source] });
        await context.SaveChangesAsync();

        string? called = null;
        var model = new ScriptedModel(request =>
        {
            called = request.ModelId;
            Assert.Contains("MER-FS01", request.Messages.Single().Content.Single().Text);
            return """{"entities":[{"name":"MER-FS01","type":"System","description":"The file server","confidence":0.9},{"name":"corp","type":"Network","description":"Office segment","confidence":0.8}],"edges":[{"source":"MER-FS01","target":"corp","type":"LocatedAt","label":"on","confidence":0.8}]}""";
        });
        var service = new ScenarioExtractionService(context, new ConfigurationBuilder().Build(), null!, model,
            Options.Create(new ScenarioAuthoringOptions { Model = "default-model" }));

        var result = await service.ExtractAllAsync(1, default);

        Assert.Equal("picked-model", called);
        Assert.Equal(2, result.EntitiesCreated);
        Assert.Equal(1, result.EdgesCreated);
        var chunk = await context.ScenarioSourceChunks.SingleAsync();
        Assert.Equal("Completed", chunk.ExtractionStatus);
        var host = await context.ScenarioEntities.SingleAsync(e => e.Name == "MER-FS01");
        Assert.Equal("Extracted", host.Origin);
        Assert.Equal(chunk.Id, host.SourceChunkId);
        Assert.False(host.IsReviewed);
    }

    [Fact]
    public async Task A_scenario_with_no_model_picked_extracts_on_the_default()
    {
        await using var context = NewContext();
        context.Scenarios.Add(new Scenario { Id = 1, Name = "Mine", Sources = [new ScenarioSource { ScenarioId = 1, Name = "Notes", Chunks = [new ScenarioSourceChunk { ScenarioId = 1, Content = "Nothing much." }] }] });
        await context.SaveChangesAsync();
        string? called = null;
        var model = new ScriptedModel(request => { called = request.ModelId; return """{"entities":[],"edges":[]}"""; });
        var service = new ScenarioExtractionService(context, new ConfigurationBuilder().Build(), null!, model,
            Options.Create(new ScenarioAuthoringOptions { Model = "default-model" }));

        await service.ExtractAllAsync(1, default);

        Assert.Equal("default-model", called);
    }

    private sealed class ScriptedModel(Func<ConverseRequest, string> reply) : IAuthoringModel
    {
        public Task<ConverseResponse> ConverseAsync(ConverseRequest request, CancellationToken ct) => Task.FromResult(new ConverseResponse
        {
            Output = new ConverseOutput { Message = new Message { Role = ConversationRole.Assistant, Content = [new ContentBlock { Text = reply(request) }] } },
            StopReason = StopReason.End_turn
        });
    }

    private static ApplicationDbContext NewContext() => new TestDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase($"extraction-{Guid.NewGuid()}")
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
