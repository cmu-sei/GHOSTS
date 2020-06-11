// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Api.Infrastructure.Extensions;
using Ghosts.Api.Models;
using Ghosts.Domain.Messages.MesssagesForServer;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Ghosts.Api.Infrastructure.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
            Database.EnsureCreated();
        }

        public DbSet<Machine> Machines { get; set; }
        public DbSet<Group> Groups { get; set; }

        public DbSet<Machine.MachineHistoryItem> HistoryMachine { get; set; }
        public DbSet<HistoryHealth> HistoryHealth { get; set; }
        public DbSet<HistoryTimeline> HistoryTimeline { get; set; }
        public DbSet<HistoryTrackable> HistoryTrackables { get; set; }

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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // Add your customizations after calling base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Machine>().HasIndex(o => new {o.CreatedUtc});
            modelBuilder.Entity<Machine>().HasIndex(o => new {o.Status});
            modelBuilder.Entity<Machine>().HasIndex(o => new {o.LastReportedUtc});

            modelBuilder.Entity<Machine.MachineHistoryItem>().HasIndex(o => new {o.CreatedUtc});

            modelBuilder.Entity<HistoryHealth>().HasIndex(o => new {o.CreatedUtc});

            modelBuilder.Entity<HistoryTimeline>().HasIndex(o => new {o.CreatedUtc});

            modelBuilder.Entity<MachineUpdate>().HasIndex(o => new {o.CreatedUtc});
            modelBuilder.Entity<MachineUpdate>().HasIndex(o => new {o.ActiveUtc});
            modelBuilder.Entity<MachineUpdate>().HasIndex(o => new {o.Status});

            modelBuilder.Entity<GroupMachine>().HasIndex(o => new {o.MachineId});

            modelBuilder.Entity<Group>().HasIndex(o => new {o.Name});

            modelBuilder.Entity<Webhook>().HasIndex(o => new {o.Status});
            modelBuilder.Entity<Webhook>().HasIndex(o => new {o.CreatedUtc});

            modelBuilder.Entity<Survey>().HasIndex(o => new {o.MachineId});
            modelBuilder.Entity<Survey.DriveInfo>().HasIndex(o => new {o.SurveyId});
            modelBuilder.Entity<Survey.EventLog>().HasIndex(o => new {o.SurveyId});
            modelBuilder.Entity<Survey.Interface>().HasIndex(o => new {o.SurveyId});
            modelBuilder.Entity<Survey.LocalProcess>().HasIndex(o => new {o.SurveyId});
            modelBuilder.Entity<Survey.LocalUser>().HasIndex(o => new {o.SurveyId});
            modelBuilder.Entity<Survey.Port>().HasIndex(o => new {o.SurveyId});
            modelBuilder.Entity<Survey.EventLog.EventLogEntry>().HasIndex(o => new {o.EventLogId});
            modelBuilder.Entity<Survey.Interface.InterfaceBinding>().HasIndex(o => new {o.InterfaceId});

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
                    index.SetName(index.GetName().ToCondensedLowerCase());
            }
        }
    }
}