// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ScenarioBuilder.Api.Domain;

namespace ScenarioBuilder.Api.Infrastructure;

/// <summary>One SQLite file, created at startup. Deleting it resets everything; there are no migrations yet.</summary>
public class BuilderDb(DbContextOptions<BuilderDb> options) : DbContext(options)
{
    public DbSet<Scenario> Scenarios => Set<Scenario>();
    public DbSet<Decision> Decisions => Set<Decision>();
    public DbSet<Proposal> Proposals => Set<Proposal>();
    public DbSet<Edit> Edits => Set<Edit>();
    public DbSet<Approval> Approvals => Set<Approval>();
    public DbSet<TemplateLibrary> Libraries => Set<TemplateLibrary>();
    public DbSet<LibraryVersion> LibraryVersions => Set<LibraryVersion>();
    public DbSet<Source> Sources => Set<Source>();
    public DbSet<PluginConnection> PluginConnections => Set<PluginConnection>();

    /// <summary>SQLite keeps no time zone; every time written is UTC, so read it back as UTC.</summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcConverter>();
    }

    private sealed class UtcConverter() : ValueConverter<DateTime, DateTime>(
        v => v.ToUniversalTime(), v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    private sealed class NullableUtcConverter() : ValueConverter<DateTime?, DateTime?>(
        v => v.HasValue ? v.Value.ToUniversalTime() : v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Scenario>().HasIndex(s => s.Slug).IsUnique();
        modelBuilder.Entity<Decision>().HasIndex(d => new { d.ScenarioId, d.QuestionId }).IsUnique();
        modelBuilder.Entity<Proposal>().HasIndex(p => p.ScenarioId);
        modelBuilder.Entity<Edit>().HasIndex(e => e.ScenarioId);
        modelBuilder.Entity<Approval>().HasIndex(a => new { a.ScenarioId, a.Stage });
        modelBuilder.Entity<TemplateLibrary>().HasIndex(l => l.Slug).IsUnique();
        modelBuilder.Entity<LibraryVersion>().HasIndex(v => new { v.LibraryId, v.Number }).IsUnique();
        modelBuilder.Entity<Source>().HasIndex(s => s.ScenarioId);
        modelBuilder.Entity<PluginConnection>().HasIndex(c => c.Plugin).IsUnique();
    }
}
