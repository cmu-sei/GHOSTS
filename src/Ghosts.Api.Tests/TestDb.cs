// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json;
using Ghosts.Animator.Models;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Api.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Ghosts.Api.Tests;

/// <summary>An in-memory ApplicationDbContext for service tests, one database per call.</summary>
internal static class TestDb
{
    public static ApplicationDbContext New(string name = "test") => Named($"{name}-{Guid.NewGuid()}");

    /// <summary>A context on a named database, so two contexts share one store, as two requests do.</summary>
    public static ApplicationDbContext Named(string databaseName) => new Context(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(databaseName)
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
        .Options);

    /// <summary>NpcProfile is jsonb, which only Npgsql maps; the in-memory provider needs a conversion.</summary>
    private class Context(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
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
