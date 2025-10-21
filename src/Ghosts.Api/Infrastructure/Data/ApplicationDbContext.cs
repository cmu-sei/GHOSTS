// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.IO;
using Ghosts.Api.Areas.Animator.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Extensions;
using Ghosts.Domain.Messages.MesssagesForServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
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

        public DbSet<NpcSocialGraph> NpcSocialGraphs { get; set; }
        public DbSet<NpcSocialConnection> NpcSocialConnections { get; set; }
        public DbSet<NpcInteraction> NpcInteractions { get; set; }
        public DbSet<NpcLearning> NpcLearnings { get; set; }
        public DbSet<NpcBelief> NpcBeliefs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // Add your customizations after calling base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfiguration(new MachineUpdateConfiguration());


            modelBuilder.Entity<NpcRecord>().Property(o => o.NpcProfile).HasColumnType("jsonb");

            // NpcSocialGraph relationships and indexes
            modelBuilder.Entity<NpcSocialGraph>()
                .HasMany(sg => sg.Connections)
                .WithOne(c => c.SocialGraph)
                .HasForeignKey(c => c.SocialGraphId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<NpcSocialGraph>()
                .HasMany(sg => sg.Knowledge)
                .WithOne(k => k.SocialGraph)
                .HasForeignKey(k => k.SocialGraphId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<NpcSocialGraph>()
                .HasMany(sg => sg.Beliefs)
                .WithOne(b => b.SocialGraph)
                .HasForeignKey(b => b.SocialGraphId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<NpcSocialConnection>()
                .HasMany(c => c.Interactions)
                .WithOne(i => i.SocialConnection)
                .HasForeignKey(i => i.SocialConnectionId)
                .OnDelete(DeleteBehavior.Cascade);

            // NpcSocialConnection indexes
            modelBuilder.Entity<NpcSocialConnection>().HasIndex(o => o.SocialGraphId);
            modelBuilder.Entity<NpcSocialConnection>().HasIndex(o => o.ConnectedNpcId);
            modelBuilder.Entity<NpcSocialConnection>().HasIndex(o => new { o.SocialGraphId, o.ConnectedNpcId });

            // NpcInteraction indexes
            modelBuilder.Entity<NpcInteraction>().HasIndex(o => o.SocialConnectionId);
            modelBuilder.Entity<NpcInteraction>().HasIndex(o => o.Step);

            // NpcLearning indexes
            modelBuilder.Entity<NpcLearning>().HasIndex(o => o.SocialGraphId);
            modelBuilder.Entity<NpcLearning>().HasIndex(o => new { o.ToNpcId, o.FromNpcId });
            modelBuilder.Entity<NpcLearning>().HasIndex(o => o.Step);
            modelBuilder.Entity<NpcLearning>().HasIndex(o => o.Topic);

            // NpcBelief indexes
            modelBuilder.Entity<NpcBelief>().HasIndex(o => o.SocialGraphId);
            modelBuilder.Entity<NpcBelief>().HasIndex(o => new { o.ToNpcId, o.FromNpcId });
            modelBuilder.Entity<NpcBelief>().HasIndex(o => o.Step);
            modelBuilder.Entity<NpcBelief>().HasIndex(o => o.Name);

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
            optionsBuilder.UseNpgsql(connectionString, x => x.MigrationsAssembly("ghosts.api"));

            return new ApplicationDbContext(optionsBuilder.Options);
        }
    }
}
