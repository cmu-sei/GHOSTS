// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.IO;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Extensions;
using Ghosts.Domain.Messages.MesssagesForServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using NLog;

namespace Ghosts.Api.Infrastructure.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
            Database.EnsureCreated();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
            // Suppress pending model changes warning - empty migrations indicate model is in sync
            optionsBuilder.ConfigureWarnings(warnings =>
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        }

        public DbSet<Machine> Machines { get; set; }
        public DbSet<Group> Groups { get; set; }
        public DbSet<GroupMachine> GroupMachines { get; set; }

        public DbSet<Machine.MachineHistoryItem> HistoryMachine { get; set; }
        public DbSet<HistoryHealth> HistoryHealth { get; set; }
        public DbSet<HistoryTimeline> HistoryTimeline { get; set; }
        public DbSet<HistoryTrackable> HistoryTrackables { get; set; }
        public DbSet<MachineTimeline> MachineTimelines { get; set; }
        public DbSet<MachineUpdate> MachineUpdates { get; set; }
        public DbSet<Trackable> Trackables { get; set; }
        public DbSet<Webhook> Webhooks { get; set; }

        public DbSet<Survey> Surveys { get; set; }
        public DbSet<Survey.DriveInfo> Drives { get; set; }
        public DbSet<Survey.Interface> Interfaces { get; set; }
        public DbSet<Survey.Interface.InterfaceBinding> InterfaceBindings { get; set; }
        public DbSet<Survey.EventLog> EventLogs { get; set; }
        public DbSet<Survey.EventLog.EventLogEntry> EventLogEntries { get; set; }
        public DbSet<Survey.LocalProcess> Processes { get; set; }
        public DbSet<Survey.LocalUser> LocalUsers { get; set; }
        public DbSet<Survey.Port> Ports { get; set; }

        public DbSet<NpcRecord> Npcs { get; set; }
        public DbSet<NpcIpAddress> NpcIps { get; set; }
        public DbSet<NpcActivity> NpcActivities { get; set; }

        // NPC Social Graph tables (now directly on NPC)
        public DbSet<NpcSocialConnection> NpcSocialConnections { get; set; }
        public DbSet<NpcLearning> NpcLearning { get; set; }
        public DbSet<NpcBelief> NpcBeliefs { get; set; }
        public DbSet<NpcPreference> NpcPreferences { get; set; }
        public DbSet<NpcInteraction> NpcInteractions { get; set; }

        public DbSet<Scenario> Scenarios { get; set; }
        public DbSet<ScenarioParameters> ScenarioParameters { get; set; }
        public DbSet<Nation> Nations { get; set; }
        public DbSet<ThreatActor> ThreatActors { get; set; }
        public DbSet<Inject> Injects { get; set; }
        public DbSet<UserPool> UserPools { get; set; }
        public DbSet<TechnicalEnvironment> TechnicalEnvironments { get; set; }
        public DbSet<Vulnerability> Vulnerabilities { get; set; }
        public DbSet<GameMechanics> GameMechanics { get; set; }
        public DbSet<ScenarioTimeline> ScenarioTimelines { get; set; }
        public DbSet<ScenarioTimelineEvent> ScenarioTimelineEvents { get; set; }

        public DbSet<Execution> Executions { get; set; }
        public DbSet<ExecutionEvent> ExecutionEvents { get; set; }
        public DbSet<ExecutionMetricSnapshot> ExecutionMetricSnapshots { get; set; }

        // Scenario Builder tables
        public DbSet<ScenarioSource> ScenarioSources { get; set; }
        public DbSet<ScenarioSourceChunk> ScenarioSourceChunks { get; set; }
        public DbSet<ScenarioEntity> ScenarioEntities { get; set; }
        public DbSet<ScenarioEdge> ScenarioEdges { get; set; }
        public DbSet<ScenarioEnrichment> ScenarioEnrichments { get; set; }
        public DbSet<ScenarioCompilation> ScenarioCompilations { get; set; }
        public DbSet<ScenarioNpcAssignment> ScenarioNpcAssignments { get; set; }

        public DbSet<Objective> Objectives { get; set; }

        public DbSet<Hypothesis> Hypotheses { get; set; }

        // Execution Map spatial metadata
        public DbSet<MapFeature> MapFeatures { get; set; }

        // MITRE ATT&CK reference data
        public DbSet<AttackTechnique> AttackTechniques { get; set; }
        public DbSet<AttackGroup> AttackGroups { get; set; }
        public DbSet<AttackGroupTechnique> AttackGroupTechniques { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // Add your customizations after calling base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfiguration(new MachineUpdateConfiguration());

            // NPC Profile remains JSONB for now
            modelBuilder.Entity<NpcRecord>().Property(o => o.NpcProfile).HasColumnType("jsonb");

            // NPC relationships - social graph properties now directly on NPC
            modelBuilder.Entity<NpcRecord>()
                .HasMany(n => n.Connections)
                .WithOne(c => c.Npc)
                .HasForeignKey(c => c.NpcId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<NpcRecord>()
                .HasMany(n => n.Knowledge)
                .WithOne(k => k.Npc)
                .HasForeignKey(k => k.NpcId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<NpcRecord>()
                .HasMany(n => n.Beliefs)
                .WithOne(b => b.Npc)
                .HasForeignKey(b => b.NpcId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<NpcRecord>()
                .HasMany(n => n.Preferences)
                .WithOne(p => p.Npc)
                .HasForeignKey(p => p.NpcId)
                .OnDelete(DeleteBehavior.Cascade);

            // NpcSocialConnection relationships
            modelBuilder.Entity<NpcSocialConnection>()
                .HasMany(c => c.Interactions)
                .WithOne(i => i.SocialConnection)
                .HasForeignKey(i => i.SocialConnectionId)
                .OnDelete(DeleteBehavior.Cascade);

            // NPC-Scenario relationship (optional, nullable foreign key)
            modelBuilder.Entity<NpcRecord>()
                .HasOne(n => n.Scenario)
                .WithMany(s => s.Npcs)
                .HasForeignKey(n => n.ScenarioId)
                .OnDelete(DeleteBehavior.SetNull);

            // Indexes for NPC and Social Graph tables
            modelBuilder.Entity<NpcRecord>().HasIndex(n => n.CurrentStep);
            modelBuilder.Entity<NpcRecord>().HasIndex(n => n.ScenarioId);
            modelBuilder.Entity<NpcSocialConnection>().HasIndex(c => new { c.NpcId, c.ConnectedNpcId });
            modelBuilder.Entity<NpcLearning>().HasIndex(l => new { l.NpcId, l.Topic, l.Step });
            modelBuilder.Entity<NpcBelief>().HasIndex(b => new { b.NpcId, b.Name, b.Step });
            modelBuilder.Entity<NpcBelief>().HasIndex(b => b.ExecutionId);
            modelBuilder.Entity<NpcBelief>()
                .HasOne(b => b.Execution)
                .WithMany()
                .HasForeignKey(b => b.ExecutionId)
                .OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<NpcPreference>().HasIndex(p => new { p.NpcId, p.Name, p.Step });

            modelBuilder.Entity<Hypothesis>().HasIndex(h => h.IsActive);

            modelBuilder.Entity<Machine>().HasIndex(o => new { o.CreatedUtc });
            modelBuilder.Entity<Machine>().HasIndex(o => new { o.Status });
            modelBuilder.Entity<Machine>().HasIndex(o => new { o.LastReportedUtc });

            modelBuilder.Entity<Machine.MachineHistoryItem>().HasIndex(o => new { o.CreatedUtc });

            modelBuilder.Entity<HistoryHealth>().HasIndex(o => new { o.CreatedUtc });

            modelBuilder.Entity<HistoryTimeline>().HasIndex(o => new { o.CreatedUtc });
            modelBuilder.Entity<HistoryTimeline>().HasIndex(o => new { o.Tags });

            modelBuilder.Entity<MachineUpdate>().HasIndex(o => new { o.CreatedUtc });
            modelBuilder.Entity<MachineUpdate>().HasIndex(o => new { o.ActiveUtc });
            modelBuilder.Entity<MachineUpdate>().HasIndex(o => new { o.Status });

            modelBuilder.Entity<GroupMachine>().HasIndex(o => new { o.MachineId });

            modelBuilder.Entity<Group>().HasIndex(o => new { o.Name });

            modelBuilder.Entity<Webhook>().HasIndex(o => new { o.Status });
            modelBuilder.Entity<Webhook>().HasIndex(o => new { o.CreatedUtc });

            modelBuilder.Entity<Survey>().HasIndex(o => new { o.MachineId });
            modelBuilder.Entity<Survey.DriveInfo>().HasIndex(o => new { o.SurveyId });
            modelBuilder.Entity<Survey.EventLog>().HasIndex(o => new { o.SurveyId });
            modelBuilder.Entity<Survey.Interface>().HasIndex(o => new { o.SurveyId });
            modelBuilder.Entity<Survey.LocalProcess>().HasIndex(o => new { o.SurveyId });
            modelBuilder.Entity<Survey.LocalUser>().HasIndex(o => new { o.SurveyId });
            modelBuilder.Entity<Survey.Port>().HasIndex(o => new { o.SurveyId });
            modelBuilder.Entity<Survey.EventLog.EventLogEntry>().HasIndex(o => new { o.EventLogId });
            modelBuilder.Entity<Survey.Interface.InterfaceBinding>().HasIndex(o => new { o.InterfaceId });

            // Scenario relationships
            modelBuilder.Entity<Scenario>()
                .HasOne(s => s.ScenarioParameters)
                .WithOne(sp => sp.Scenario)
                .HasForeignKey<ScenarioParameters>(sp => sp.ScenarioId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Scenario>()
                .HasOne(s => s.TechnicalEnvironment)
                .WithOne(te => te.Scenario)
                .HasForeignKey<TechnicalEnvironment>(te => te.ScenarioId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Scenario>()
                .HasOne(s => s.GameMechanics)
                .WithOne(gm => gm.Scenario)
                .HasForeignKey<GameMechanics>(gm => gm.ScenarioId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Scenario>()
                .HasOne(s => s.ScenarioTimeline)
                .WithOne(t => t.Scenario)
                .HasForeignKey<ScenarioTimeline>(t => t.ScenarioId)
                .OnDelete(DeleteBehavior.Cascade);

            // ScenarioParameters relationships
            modelBuilder.Entity<ScenarioParameters>()
                .HasMany(sp => sp.Nations)
                .WithOne(n => n.ScenarioParameters)
                .HasForeignKey(n => n.ScenarioParametersId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ScenarioParameters>()
                .HasMany(sp => sp.ThreatActors)
                .WithOne(ta => ta.ScenarioParameters)
                .HasForeignKey(ta => ta.ScenarioParametersId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ScenarioParameters>()
                .HasMany(sp => sp.Injects)
                .WithOne(i => i.ScenarioParameters)
                .HasForeignKey(i => i.ScenarioParametersId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ScenarioParameters>()
                .HasMany(sp => sp.UserPools)
                .WithOne(up => up.ScenarioParameters)
                .HasForeignKey(up => up.ScenarioParametersId)
                .OnDelete(DeleteBehavior.Cascade);

            // TechnicalEnvironment relationships
            modelBuilder.Entity<TechnicalEnvironment>()
                .HasMany(te => te.Vulnerabilities)
                .WithOne(v => v.TechnicalEnvironment)
                .HasForeignKey(v => v.TechnicalEnvironmentId)
                .OnDelete(DeleteBehavior.Cascade);

            // Timeline relationships
            modelBuilder.Entity<ScenarioTimeline>()
                .HasMany(t => t.ScenarioTimelineEvents)
                .WithOne(e => e.Timeline)
                .HasForeignKey(e => e.ScenarioTimelineId)
                .OnDelete(DeleteBehavior.Cascade);

            // Execution relationships
            modelBuilder.Entity<Scenario>()
                .HasMany(s => s.Executions)
                .WithOne(e => e.Scenario)
                .HasForeignKey(e => e.ScenarioId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Execution>()
                .HasMany(e => e.Events)
                .WithOne(ev => ev.Execution)
                .HasForeignKey(ev => ev.ExecutionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Execution>()
                .HasMany(e => e.MetricSnapshots)
                .WithOne(ms => ms.Execution)
                .HasForeignKey(ms => ms.ExecutionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Execution indexes for performance
            modelBuilder.Entity<Execution>().HasIndex(e => e.ScenarioId);
            modelBuilder.Entity<Execution>().HasIndex(e => e.Status);
            modelBuilder.Entity<Execution>().HasIndex(e => e.CreatedAt);
            modelBuilder.Entity<Execution>().HasIndex(e => e.StartedAt);
            modelBuilder.Entity<ExecutionEvent>().HasIndex(ev => new { ev.ExecutionId, ev.Timestamp });
            modelBuilder.Entity<ExecutionMetricSnapshot>().HasIndex(ms => new { ms.ExecutionId, ms.Timestamp });

            // ── Objectives relationships ──

            modelBuilder.Entity<Scenario>()
                .HasMany(s => s.Objectives)
                .WithOne(o => o.Scenario)
                .HasForeignKey(o => o.ScenarioId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Objective>()
                .HasMany(o => o.Children)
                .WithOne(o => o.Parent)
                .HasForeignKey(o => o.ParentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Objective>().HasIndex(o => o.ScenarioId);
            modelBuilder.Entity<Objective>().HasIndex(o => o.Status);
            modelBuilder.Entity<Objective>().HasIndex(o => o.Type);
            modelBuilder.Entity<Objective>().HasIndex(o => o.ParentId);

            modelBuilder.Entity<ScenarioTimelineEvent>()
                .Property(e => e.ObjectiveIds)
                .HasColumnName("objective_ids");

            // ── Scenario Builder relationships ──

            // Scenario → Sources
            modelBuilder.Entity<Scenario>()
                .HasMany(s => s.Sources)
                .WithOne(src => src.Scenario)
                .HasForeignKey(src => src.ScenarioId)
                .OnDelete(DeleteBehavior.Cascade);

            // Scenario → Entities
            modelBuilder.Entity<Scenario>()
                .HasMany(s => s.Entities)
                .WithOne(e => e.Scenario)
                .HasForeignKey(e => e.ScenarioId)
                .OnDelete(DeleteBehavior.Cascade);

            // Scenario → Edges
            modelBuilder.Entity<Scenario>()
                .HasMany(s => s.Edges)
                .WithOne(e => e.Scenario)
                .HasForeignKey(e => e.ScenarioId)
                .OnDelete(DeleteBehavior.Cascade);

            // Scenario → Enrichments
            modelBuilder.Entity<Scenario>()
                .HasMany(s => s.Enrichments)
                .WithOne(e => e.Scenario)
                .HasForeignKey(e => e.ScenarioId)
                .OnDelete(DeleteBehavior.Cascade);

            // Scenario → Compilations
            modelBuilder.Entity<Scenario>()
                .HasMany(s => s.Compilations)
                .WithOne(c => c.Scenario)
                .HasForeignKey(c => c.ScenarioId)
                .OnDelete(DeleteBehavior.Cascade);

            // Source → Chunks
            modelBuilder.Entity<ScenarioSource>()
                .HasMany(s => s.Chunks)
                .WithOne(c => c.Source)
                .HasForeignKey(c => c.SourceId)
                .OnDelete(DeleteBehavior.Cascade);

            // Entity → Edges (outgoing and incoming)
            modelBuilder.Entity<ScenarioEntity>()
                .HasMany(e => e.OutgoingEdges)
                .WithOne(edge => edge.SourceEntity)
                .HasForeignKey(edge => edge.SourceEntityId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ScenarioEntity>()
                .HasMany(e => e.IncomingEdges)
                .WithOne(edge => edge.TargetEntity)
                .HasForeignKey(edge => edge.TargetEntityId)
                .OnDelete(DeleteBehavior.Restrict); // avoid multiple cascade paths

            // Entity → NPC (optional, SetNull)
            modelBuilder.Entity<ScenarioEntity>()
                .HasOne(e => e.Npc)
                .WithMany()
                .HasForeignKey(e => e.NpcId)
                .OnDelete(DeleteBehavior.SetNull);

            // Entity → Source provenance (optional)
            modelBuilder.Entity<ScenarioEntity>()
                .HasOne(e => e.Source)
                .WithMany()
                .HasForeignKey(e => e.SourceId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ScenarioEntity>()
                .HasOne(e => e.SourceChunk)
                .WithMany()
                .HasForeignKey(e => e.SourceChunkId)
                .OnDelete(DeleteBehavior.SetNull);

            // Enrichment → Entity (optional)
            modelBuilder.Entity<ScenarioEnrichment>()
                .HasOne(e => e.Entity)
                .WithMany()
                .HasForeignKey(e => e.EntityId)
                .OnDelete(DeleteBehavior.SetNull);

            // JSONB columns for builder entities
            modelBuilder.Entity<ScenarioEntity>().Property(e => e.Properties).HasColumnType("jsonb");
            modelBuilder.Entity<ScenarioEdge>().Property(e => e.Properties).HasColumnType("jsonb");
            modelBuilder.Entity<ScenarioEnrichment>().Property(e => e.Data).HasColumnType("jsonb");
            modelBuilder.Entity<ScenarioCompilation>().Property(e => e.PackageData).HasColumnType("jsonb");

            // Scenario Builder indexes
            modelBuilder.Entity<ScenarioSource>().HasIndex(s => s.ScenarioId);
            modelBuilder.Entity<ScenarioSourceChunk>().HasIndex(c => c.SourceId);
            modelBuilder.Entity<ScenarioSourceChunk>().HasIndex(c => new { c.ScenarioId, c.ExtractionStatus });
            modelBuilder.Entity<ScenarioEntity>().HasIndex(e => e.ScenarioId);
            modelBuilder.Entity<ScenarioEntity>().HasIndex(e => new { e.ScenarioId, e.EntityType });
            modelBuilder.Entity<ScenarioEntity>().HasIndex(e => e.NpcId);
            modelBuilder.Entity<ScenarioEntity>().HasIndex(e => e.ExternalId);
            modelBuilder.Entity<ScenarioEdge>().HasIndex(e => e.ScenarioId);
            modelBuilder.Entity<ScenarioEdge>().HasIndex(e => e.SourceEntityId);
            modelBuilder.Entity<ScenarioEdge>().HasIndex(e => e.TargetEntityId);
            modelBuilder.Entity<ScenarioEnrichment>().HasIndex(e => e.ScenarioId);
            modelBuilder.Entity<ScenarioEnrichment>().HasIndex(e => e.EntityId);
            modelBuilder.Entity<ScenarioCompilation>().HasIndex(c => c.ScenarioId);

            // NPC assignments – scoped to a compilation
            modelBuilder.Entity<ScenarioNpcAssignment>()
                .HasOne(a => a.Scenario)
                .WithMany()
                .HasForeignKey(a => a.ScenarioId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ScenarioNpcAssignment>()
                .HasOne(a => a.Compilation)
                .WithMany()
                .HasForeignKey(a => a.CompilationId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ScenarioNpcAssignment>()
                .HasIndex(a => new { a.CompilationId, a.NpcId })
                .IsUnique(); // one machine per NPC per compilation

            modelBuilder.Entity<ScenarioNpcAssignment>().HasIndex(a => a.ScenarioId);
            modelBuilder.Entity<ScenarioNpcAssignment>().HasIndex(a => a.MachineId);

            // ── ATT&CK reference data relationships ──

            // Technique self-reference (parent → subtechniques)
            modelBuilder.Entity<AttackTechnique>()
                .HasOne(t => t.Parent)
                .WithMany(t => t.Subtechniques)
                .HasForeignKey(t => t.ParentId)
                .OnDelete(DeleteBehavior.SetNull);

            // Group-Technique junction composite key
            modelBuilder.Entity<AttackGroupTechnique>()
                .HasKey(gt => new { gt.GroupId, gt.TechniqueId });

            modelBuilder.Entity<AttackGroupTechnique>()
                .HasOne(gt => gt.Group)
                .WithMany(g => g.TechniqueUsages)
                .HasForeignKey(gt => gt.GroupId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AttackGroupTechnique>()
                .HasOne(gt => gt.Technique)
                .WithMany(t => t.GroupUsages)
                .HasForeignKey(gt => gt.TechniqueId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AttackTechnique>().HasIndex(t => t.Tactics);

            // ── Map Feature indexes ──
            modelBuilder.Entity<MapFeature>().Property(e => e.Properties).HasColumnType("jsonb");
            modelBuilder.Entity<MapFeature>().Property(e => e.Geometry).HasColumnType("jsonb");
            modelBuilder.Entity<MapFeature>().HasIndex(e => new { e.FeatureType, e.ExecutionId });
            modelBuilder.Entity<MapFeature>().HasIndex(e => new { e.EntityId, e.FeatureType });
            modelBuilder.Entity<MapFeature>().HasIndex(e => e.ScenarioId);
            modelBuilder.Entity<MapFeature>().HasIndex(e => e.ExecutionId);
            modelBuilder.Entity<MapFeature>().HasIndex(e => e.ValidFrom);

            foreach (var entity in modelBuilder.Model.GetEntityTypes())
            {
                entity.SetTableName(entity.GetTableName().ToCondensedLowerCase());
                foreach (var property in entity.GetProperties())
                    property.SetColumnName(property.Name.ToCondensedLowerCase());
                foreach (var key in entity.GetKeys())
                    key.SetName(key.GetName().ToCondensedLowerCase());
                foreach (var key in entity.GetForeignKeys())
                    key.SetConstraintName(key.GetConstraintName().ToCondensedLowerCase());
                foreach (var index in entity.GetIndexes())
                    index.SetDatabaseName(index.GetDatabaseName().ToCondensedLowerCase());
            }
        }
    }

    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public ApplicationDbContext CreateDbContext(string[] args)
        {
            var path = $"{Directory.GetCurrentDirectory()}/../ghosts.api/";
            _log.Trace(path);
            var configuration = new ConfigurationBuilder()
                .SetBasePath(path)
                .AddJsonFile("appsettings.json")
                .Build();

            var connectionString = configuration.GetConnectionString("DefaultConnection");

            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly("ghosts.api");
                npgsqlOptions.ConfigureDataSource(dataSourceBuilder =>
                {
                    var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
                    {
                        NumberHandling = JsonNumberHandling.AllowReadingFromString
                    };
                    serializerOptions.Converters.Add(new JsonStringEnumConverter());
                    dataSourceBuilder.ConfigureJsonOptions(serializerOptions);
                });
            });

            return new ApplicationDbContext(optionsBuilder.Options);
        }
    }
}
