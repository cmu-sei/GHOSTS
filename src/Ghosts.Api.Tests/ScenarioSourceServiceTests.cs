// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json;
using Ghosts.Animator.Models;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Ghosts.Api.Tests;

/// <summary>A chunk records where it sits in its source, so a citation can name the page or the place, not only the chunk.</summary>
public class ScenarioSourceServiceTests
{
    [Fact]
    public async Task Chunks_record_their_offset_in_the_source_and_the_pdf_page_they_start_on()
    {
        await using var context = NewContext();
        // Three pages as PDF extraction writes them, a form feed between pages, each longer than a chunk so
        // chunks start on every page; a chunk's page is the one it starts on, and the overlap can put that on the page before.
        var content = string.Join('\f', Enumerable.Range(1, 3).Select(p => $"Page {p}. " + new string((char)('a' + p), 5000) + "\n\n"));
        var pdf = new ScenarioSource { ScenarioId = 1, Name = "plan.pdf", Content = content };
        var text = new ScenarioSource { ScenarioId = 1, Name = "notes", Content = "   The gateway is substation-gw-01." };
        context.ScenarioSources.AddRange(pdf, text);
        await context.SaveChangesAsync();
        var service = new ScenarioSourceService(context);

        await service.ChunkSourceAsync(pdf.Id, default);
        await service.ChunkSourceAsync(text.Id, default);

        var chunks = await service.GetChunksAsync(pdf.Id, default);
        Assert.True(chunks.Count >= 3);
        // The offset is where the stored text begins in the source, exactly.
        Assert.All(chunks, c => Assert.Equal(c.Content, content.Substring(c.StartOffset!.Value, c.Content.Length)));
        Assert.Equal(1, chunks.First().Page);
        Assert.Equal(3, chunks.Last().Page);
        Assert.Contains(chunks, c => c.Page == 2);
        Assert.Equal(chunks.Select(c => c.Page!.Value), chunks.Select(c => c.Page!.Value).OrderBy(p => p));

        // Plain text has no pages; its offset is past the leading whitespace the stored text drops.
        var note = Assert.Single(await service.GetChunksAsync(text.Id, default));
        Assert.Null(note.Page);
        Assert.Equal(3, note.StartOffset);
        Assert.Equal("The gateway is substation-gw-01.", note.Content);
    }

    private static ApplicationDbContext NewContext() => new TestDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase($"sources-{Guid.NewGuid()}")
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
