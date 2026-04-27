// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ghosts.Api.Infrastructure.Data
{
    public class DbInitializer
    {
        public static async Task Initialize(ApplicationDbContext context, ILogger<DbInitializer> logger, IServiceProvider serviceProvider)
        {
            // Ensure NPC Campaign/Enclave/Team columns exist (for databases created before these fields were added)
            await EnsureNpcColumnsExist(context, logger);

            // Ensure NPC tables have execution_id column for per-execution filtering
            await EnsureNpcExecutionIdColumns(context, logger);

            // Import MITRE ATT&CK data if not already loaded
            await ImportMitreAttackData(context, logger, serviceProvider);

            // Seed all exercise data from config/SeedData/seed.json (scenarios, objectives, NPCs, etc.)
            await SeedFromJson(context, logger);

            // Ensure the map_features table exists (EnsureCreated is a no-op for existing DBs)
            await EnsureMapFeaturesTableExists(context, logger);

            // Seed map features independently (has its own guard)
            await SeedMapFeatures(context, logger);
        }

        private static async Task EnsureNpcColumnsExist(ApplicationDbContext context, ILogger<DbInitializer> logger)
        {
            try
            {
                var connection = context.Database.GetDbConnection();
                await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    DO $$
                    BEGIN
                        -- Add Campaign column if it doesn't exist
                        IF NOT EXISTS (
                            SELECT 1 FROM information_schema.columns
                            WHERE table_name = 'npcs' AND column_name = 'campaign'
                        ) THEN
                            ALTER TABLE npcs ADD COLUMN campaign TEXT;
                            RAISE NOTICE 'Added campaign column to npcs table';
                        END IF;

                        -- Add Enclave column if it doesn't exist
                        IF NOT EXISTS (
                            SELECT 1 FROM information_schema.columns
                            WHERE table_name = 'npcs' AND column_name = 'enclave'
                        ) THEN
                            ALTER TABLE npcs ADD COLUMN enclave TEXT;
                            RAISE NOTICE 'Added enclave column to npcs table';
                        END IF;

                        -- Add Team column if it doesn't exist
                        IF NOT EXISTS (
                            SELECT 1 FROM information_schema.columns
                            WHERE table_name = 'npcs' AND column_name = 'team'
                        ) THEN
                            ALTER TABLE npcs ADD COLUMN team TEXT;
                            RAISE NOTICE 'Added team column to npcs table';
                        END IF;
                    END $$;
                ";

                await command.ExecuteNonQueryAsync();
                logger.LogInformation("Verified NPC Campaign/Enclave/Team columns exist");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not verify/add NPC columns, they may already exist or database may not be ready");
            }
        }

        private static async Task EnsureNpcExecutionIdColumns(ApplicationDbContext context, ILogger<DbInitializer> logger)
        {
            try
            {
                var connection = context.Database.GetDbConnection();
                if (connection.State != System.Data.ConnectionState.Open)
                    await connection.OpenAsync();

                // Add executionid to npcs table (the single source of truth)
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = @"
                        DO $$
                        BEGIN
                            IF NOT EXISTS (
                                SELECT 1 FROM information_schema.columns
                                WHERE table_name = 'npcs' AND column_name = 'executionid'
                            ) THEN
                                ALTER TABLE npcs ADD COLUMN executionid INTEGER REFERENCES executions(id) ON DELETE SET NULL;
                                CREATE INDEX ix_npcs_executionid ON npcs (executionid);
                                RAISE NOTICE 'Added executionid column to npcs';
                            END IF;
                        END $$;
                    ";
                    await cmd.ExecuteNonQueryAsync();
                }

                // Drop executionid from child tables (now inferred via NPC join)
                var childTables = new[] { "npc_activity", "npc_learning", "npc_preferences", "npc_social_connections", "npc_interactions", "npc_beliefs" };
                foreach (var table in childTables)
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = $@"
                        DO $$
                        BEGIN
                            IF EXISTS (
                                SELECT 1 FROM information_schema.columns
                                WHERE table_name = '{table}' AND column_name = 'executionid'
                            ) THEN
                                ALTER TABLE {table} DROP COLUMN executionid;
                                RAISE NOTICE 'Dropped executionid column from {table}';
                            END IF;
                        END $$;
                    ";
                    await cmd.ExecuteNonQueryAsync();
                }

                logger.LogInformation("Verified NPC execution_id on npcs table, removed from child tables");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not verify/migrate NPC execution_id columns");
            }
        }

        private static async Task ImportMitreAttackData(ApplicationDbContext context, ILogger<DbInitializer> logger, IServiceProvider serviceProvider)
        {
            try
            {
                // Check if MITRE data is already loaded
                if (await context.AttackTechniques.AnyAsync())
                {
                    logger.LogInformation("MITRE ATT&CK data already loaded");
                    return;
                }

                logger.LogInformation("Loading MITRE ATT&CK data...");

                var stixPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "AttackData", "enterprise-attack.json");
                if (!File.Exists(stixPath))
                {
                    logger.LogWarning($"MITRE ATT&CK data file not found at {stixPath}. Skipping MITRE data import.");
                    return;
                }

                // Get the enrichment service from DI
                using var scope = serviceProvider.CreateScope();
                var enrichmentService = scope.ServiceProvider.GetRequiredService<IScenarioEnrichmentService>();

                await enrichmentService.ImportAttackDataAsync(stixPath, default);

                logger.LogInformation("MITRE ATT&CK data loaded successfully");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error loading MITRE ATT&CK data. Scenario Builder enrichment features may not work properly.");
            }
        }

        // ===== Unified Seed from JSON =====

        private static async Task SeedFromJson(ApplicationDbContext context, ILogger<DbInitializer> logger)
        {
            var seedPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "SeedData", "seed.json");
            if (!File.Exists(seedPath))
            {
                logger.LogInformation("No seed file at {Path} — skipping", seedPath);
                return;
            }

            var alreadySeeded = await context.Scenarios.AnyAsync() || await context.Npcs.AnyAsync();
            if (alreadySeeded)
            {
                logger.LogInformation("Database already has scenario/NPC data — skipping seed");
                return;
            }

            logger.LogInformation("Loading seed data from {Path}...", seedPath);

            try
            {
                var json = await File.ReadAllTextAsync(seedPath);
                var opts = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    NumberHandling = JsonNumberHandling.AllowReadingFromString
                };
                var seed = JsonSerializer.Deserialize<SeedFile>(json, opts);
                if (seed == null) return;

                var now = DateTime.UtcNow;

                // --- Scenarios ---
                var scenarioMap = new Dictionary<string, int>(); // name → db id
                if (seed.Scenarios?.Count > 0)
                {
                    foreach (var s in seed.Scenarios)
                    {
                        var scenario = new Scenario
                        {
                            Name = s.Name,
                            Description = s.Description,
                            CreatedAt = now.AddDays(-28),
                            UpdatedAt = now.AddDays(-28),
                            ScenarioParameters = new ScenarioParameters
                            {
                                Objectives = s.ScenarioParameters?.Objectives,
                                PoliticalContext = s.ScenarioParameters?.PoliticalContext,
                                RulesOfEngagement = s.ScenarioParameters?.RulesOfEngagement,
                                VictoryConditions = s.ScenarioParameters?.VictoryConditions,
                                Nations = s.ScenarioParameters?.Nations?.Select(n => new Nation { Name = n.Name, Alignment = n.Alignment }).ToList() ?? new List<Nation>(),
                                ThreatActors = s.ScenarioParameters?.ThreatActors?.Select(t => new ThreatActor { Name = t.Name, Type = t.Type, Capability = t.Capability, Ttps = t.Ttps }).ToList() ?? new List<ThreatActor>(),
                                Injects = s.ScenarioParameters?.Injects?.Select(i => new Inject { Trigger = i.Trigger, Title = i.Title }).ToList() ?? new List<Inject>(),
                                UserPools = s.ScenarioParameters?.UserPools?.Select(u => new UserPool { Role = u.Role, Count = u.Count }).ToList() ?? new List<UserPool>()
                            },
                            TechnicalEnvironment = s.TechnicalEnvironment == null ? null : new TechnicalEnvironment
                            {
                                NetworkTopology = s.TechnicalEnvironment.NetworkTopology,
                                Services = s.TechnicalEnvironment.Services,
                                Assets = s.TechnicalEnvironment.Assets,
                                Defenses = s.TechnicalEnvironment.Defenses,
                                Vulnerabilities = s.TechnicalEnvironment.Vulnerabilities?.Select(v => new Vulnerability { Asset = v.Asset, Cve = v.Cve, Severity = v.Severity }).ToList() ?? new List<Vulnerability>()
                            },
                            GameMechanics = s.GameMechanics == null ? null : new GameMechanics
                            {
                                TimelineType = s.GameMechanics.TimelineType,
                                DurationHours = s.GameMechanics.DurationHours,
                                AdjudicationType = s.GameMechanics.AdjudicationType,
                                EscalationLadder = s.GameMechanics.EscalationLadder,
                                BranchingLogic = s.GameMechanics.BranchingLogic,
                                CollectLogs = s.GameMechanics.CollectLogs,
                                CollectNetwork = s.GameMechanics.CollectNetwork,
                                CollectEndpoint = s.GameMechanics.CollectEndpoint,
                                CollectChat = s.GameMechanics.CollectChat,
                                PerformanceMetrics = s.GameMechanics.PerformanceMetrics
                            },
                            ScenarioTimeline = s.ScenarioTimeline == null ? null : new ScenarioTimeline
                            {
                                ExerciseDuration = s.ScenarioTimeline.ExerciseDuration,
                                ScenarioTimelineEvents = s.ScenarioTimeline.Events?.Select((e, idx) => new ScenarioTimelineEvent
                                {
                                    Time = e.Time,
                                    Number = e.Number > 0 ? e.Number : idx + 1,
                                    Assigned = e.Assigned,
                                    Description = e.Description,
                                    Status = e.Status ?? "Pending"
                                }).ToList() ?? new List<ScenarioTimelineEvent>()
                            }
                        };
                        context.Scenarios.Add(scenario);
                        await context.SaveChangesAsync();
                        scenarioMap[scenario.Name] = scenario.Id;
                    }
                    logger.LogInformation("Seeded {Count} scenarios", seed.Scenarios.Count);
                }

                // --- Objectives ---
                if (seed.Objectives?.Count > 0)
                {
                    var objectiveMap = new Dictionary<string, int>(); // "scenarioName|objectiveName" → db id
                    // First pass: top-level (no parent)
                    foreach (var o in seed.Objectives.Where(x => string.IsNullOrEmpty(x.ParentName)))
                    {
                        var scenarioId = scenarioMap.GetValueOrDefault(o.ScenarioName);
                        var obj = new Objective
                        {
                            ScenarioId = scenarioId > 0 ? scenarioId : null,
                            Name = o.Name,
                            Description = o.Description,
                            Type = o.Type ?? "MET",
                            Status = "Active",
                            Score = "U",
                            Priority = o.Priority,
                            SuccessCriteria = o.SuccessCriteria,
                            Assigned = o.Assigned,
                            CreatedAt = now,
                            UpdatedAt = now
                        };
                        context.Objectives.Add(obj);
                        await context.SaveChangesAsync();
                        objectiveMap[$"{o.ScenarioName}|{o.Name}"] = obj.Id;
                    }
                    // Second pass: children
                    foreach (var o in seed.Objectives.Where(x => !string.IsNullOrEmpty(x.ParentName)))
                    {
                        var scenarioId = scenarioMap.GetValueOrDefault(o.ScenarioName);
                        var parentId = objectiveMap.GetValueOrDefault($"{o.ScenarioName}|{o.ParentName}");
                        var obj = new Objective
                        {
                            ScenarioId = scenarioId > 0 ? scenarioId : null,
                            ParentId = parentId > 0 ? parentId : null,
                            Name = o.Name,
                            Description = o.Description,
                            Type = o.Type ?? "Rehearsal",
                            Status = "Active",
                            Score = "U",
                            Priority = o.Priority,
                            SuccessCriteria = o.SuccessCriteria,
                            Assigned = o.Assigned,
                            CreatedAt = now,
                            UpdatedAt = now
                        };
                        context.Objectives.Add(obj);
                    }
                    await context.SaveChangesAsync();
                    logger.LogInformation("Seeded {Count} objectives", seed.Objectives.Count);
                }

                // --- Machines ---
                if (seed.Machines?.Count > 0)
                {
                    foreach (var m in seed.Machines)
                    {
                        context.Machines.Add(new Machine
                        {
                            Id = Guid.Parse(m.Id),
                            Name = m.Name,
                            FQDN = m.Fqdn,
                            Host = m.Host,
                            HostIp = m.HostIp,
                            IPAddress = m.HostIp,
                            CurrentUsername = m.CurrentUsername,
                            ClientVersion = m.ClientVersion,
                            StatusUp = (Machine.UpDownStatus)m.StatusUp,
                            LastReportedUtc = now.AddHours(-1)
                        });
                    }
                    await context.SaveChangesAsync();
                    logger.LogInformation("Seeded {Count} machines", seed.Machines.Count);
                }

                // --- NPCs ---
                if (seed.Npcs?.Count > 0)
                {
                    foreach (var n in seed.Npcs)
                    {
                        context.Npcs.Add(new NpcRecord
                        {
                            Id = Guid.Parse(n.Id),
                            Campaign = n.Campaign,
                            Enclave = n.Enclave,
                            Team = n.Team,
                            CreatedUtc = now.AddDays(-2),
                            NpcProfile = n.NpcProfile
                        });
                    }
                    await context.SaveChangesAsync();
                    logger.LogInformation("Seeded {Count} NPCs", seed.Npcs.Count);
                }

                // --- Timeline History ---
                if (seed.TimelineHistory?.Count > 0)
                {
                    foreach (var t in seed.TimelineHistory)
                    {
                        context.HistoryTimeline.Add(new HistoryTimeline
                        {
                            MachineId = Guid.Parse(t.MachineId),
                            Handler = t.Handler,
                            Command = t.Command,
                            CommandArg = t.CommandArg ?? "",
                            Result = t.Result ?? "",
                            CreatedUtc = now.AddHours(t.OffsetHours)
                        });
                    }
                    await context.SaveChangesAsync();
                    logger.LogInformation("Seeded {Count} timeline events", seed.TimelineHistory.Count);
                }

                // --- Health History ---
                if (seed.HealthHistory?.Count > 0)
                {
                    foreach (var h in seed.HealthHistory)
                    {
                        context.HistoryHealth.Add(new HistoryHealth
                        {
                            MachineId = Guid.Parse(h.MachineId),
                            Internet = h.Internet,
                            Permissions = h.Permissions,
                            ExecutionTime = h.ExecutionTime,
                            CreatedUtc = now.AddHours(h.OffsetHours)
                        });
                    }
                    await context.SaveChangesAsync();
                    logger.LogInformation("Seeded {Count} health records", seed.HealthHistory.Count);
                }

                // --- Machine History ---
                if (seed.MachineHistory?.Count > 0)
                {
                    foreach (var mh in seed.MachineHistory)
                    {
                        context.HistoryMachine.Add(new Machine.MachineHistoryItem
                        {
                            MachineId = Guid.Parse(mh.MachineId),
                            Type = (Machine.MachineHistoryItem.HistoryType)mh.Type,
                            Object = mh.Object ?? "{}",
                            CreatedUtc = now.AddHours(mh.OffsetHours)
                        });
                    }
                    await context.SaveChangesAsync();
                    logger.LogInformation("Seeded {Count} machine history records", seed.MachineHistory.Count);
                }

                // --- Social Connections ---
                if (seed.SocialConnections?.Count > 0)
                {
                    foreach (var c in seed.SocialConnections)
                    {
                        context.NpcSocialConnections.Add(new NpcSocialConnection
                        {
                            Id = c.Id,
                            NpcId = Guid.Parse(c.NpcId),
                            ConnectedNpcId = Guid.Parse(c.ConnectedNpcId),
                            Name = c.Name,
                            Distance = c.Distance,
                            RelationshipStatus = c.RelationshipStatus,
                            CreatedUtc = now.AddHours(c.OffsetHours),
                            UpdatedUtc = now.AddHours(c.OffsetHours)
                        });
                    }
                    await context.SaveChangesAsync();
                    logger.LogInformation("Seeded {Count} social connections", seed.SocialConnections.Count);
                }

                // --- Interactions ---
                if (seed.Interactions?.Count > 0)
                {
                    foreach (var i in seed.Interactions)
                    {
                        context.NpcInteractions.Add(new NpcInteraction
                        {
                            SocialConnectionId = i.SocialConnectionId,
                            Step = i.Step,
                            Value = i.Value,
                            CreatedUtc = now.AddHours(i.OffsetHours)
                        });
                    }
                    await context.SaveChangesAsync();
                    logger.LogInformation("Seeded {Count} interactions", seed.Interactions.Count);
                }

                // --- Activities ---
                if (seed.Activities?.Count > 0)
                {
                    foreach (var a in seed.Activities)
                    {
                        context.NpcActivities.Add(new NpcActivity
                        {
                            NpcId = Guid.Parse(a.NpcId),
                            ActivityType = (NpcActivity.ActivityTypes)a.ActivityType,
                            Detail = a.Detail,
                            CreatedUtc = now.AddHours(a.OffsetHours)
                        });
                    }
                    await context.SaveChangesAsync();
                    logger.LogInformation("Seeded {Count} activities", seed.Activities.Count);
                }

                // --- Beliefs ---
                if (seed.Beliefs?.Count > 0)
                {
                    foreach (var b in seed.Beliefs)
                    {
                        context.NpcBeliefs.Add(new NpcBelief
                        {
                            NpcId = Guid.Parse(b.NpcId),
                            ToNpcId = Guid.Parse(b.ToNpcId),
                            FromNpcId = Guid.Parse(b.FromNpcId),
                            Name = b.Name,
                            Step = b.Step,
                            Likelihood = b.Likelihood,
                            Posterior = b.Posterior,
                            CreatedUtc = now.AddHours(b.OffsetHours)
                        });
                    }
                    await context.SaveChangesAsync();
                    logger.LogInformation("Seeded {Count} beliefs", seed.Beliefs.Count);
                }

                // --- Learning ---
                if (seed.Learning?.Count > 0)
                {
                    foreach (var l in seed.Learning)
                    {
                        context.NpcLearning.Add(new NpcLearning(
                            Guid.Parse(l.NpcId),
                            Guid.Parse(l.ToNpcId),
                            Guid.Parse(l.FromNpcId),
                            l.Topic,
                            l.Step,
                            l.Value
                        ) { CreatedUtc = now.AddHours(l.OffsetHours) });
                    }
                    await context.SaveChangesAsync();
                    logger.LogInformation("Seeded {Count} learning records", seed.Learning.Count);
                }

                // --- Preferences ---
                if (seed.Preferences?.Count > 0)
                {
                    foreach (var p in seed.Preferences)
                    {
                        context.NpcPreferences.Add(new NpcPreference(
                            0,
                            Guid.Parse(p.NpcId),
                            Guid.Parse(p.ToNpcId),
                            Guid.Parse(p.FromNpcId),
                            p.Name,
                            p.Step,
                            p.Weight,
                            p.Strength
                        ) { CreatedUtc = now.AddHours(p.OffsetHours) });
                    }
                    await context.SaveChangesAsync();
                    logger.LogInformation("Seeded {Count} preferences", seed.Preferences.Count);
                }

                logger.LogInformation("Seed data loaded successfully");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load seed data from {Path}", seedPath);
            }
        }

        // ===== Seed DTOs =====

        private class SeedFile
        {
            public List<SeedScenario> Scenarios { get; set; }
            public List<SeedObjective> Objectives { get; set; }
            public List<SeedMachine> Machines { get; set; }
            public List<SeedNpc> Npcs { get; set; }
            public List<SeedTimelineHistory> TimelineHistory { get; set; }
            public List<SeedHealthHistory> HealthHistory { get; set; }
            public List<SeedMachineHistory> MachineHistory { get; set; }
            public List<SeedSocialConnection> SocialConnections { get; set; }
            public List<SeedInteraction> Interactions { get; set; }
            public List<SeedActivity> Activities { get; set; }
            public List<SeedBelief> Beliefs { get; set; }
            public List<SeedLearning> Learning { get; set; }
            public List<SeedPreference> Preferences { get; set; }
        }

        private class SeedScenario
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public SeedScenarioParameters ScenarioParameters { get; set; }
            public SeedTechnicalEnvironment TechnicalEnvironment { get; set; }
            public SeedGameMechanics GameMechanics { get; set; }
            public SeedScenarioTimeline ScenarioTimeline { get; set; }
        }

        private class SeedScenarioParameters
        {
            public string Objectives { get; set; }
            public string PoliticalContext { get; set; }
            public string RulesOfEngagement { get; set; }
            public string VictoryConditions { get; set; }
            public List<SeedNation> Nations { get; set; }
            public List<SeedThreatActor> ThreatActors { get; set; }
            public List<SeedInject> Injects { get; set; }
            public List<SeedUserPool> UserPools { get; set; }
        }

        private class SeedNation { public string Name { get; set; } public string Alignment { get; set; } }
        private class SeedThreatActor { public string Name { get; set; } public string Type { get; set; } public int Capability { get; set; } public string Ttps { get; set; } }
        private class SeedInject { public string Trigger { get; set; } public string Title { get; set; } }
        private class SeedUserPool { public string Role { get; set; } public int Count { get; set; } }

        private class SeedTechnicalEnvironment
        {
            public string NetworkTopology { get; set; }
            public string Services { get; set; }
            public string Assets { get; set; }
            public string Defenses { get; set; }
            public List<SeedVulnerability> Vulnerabilities { get; set; }
        }

        private class SeedVulnerability { public string Asset { get; set; } public string Cve { get; set; } public string Severity { get; set; } }

        private class SeedGameMechanics
        {
            public string TimelineType { get; set; }
            public int DurationHours { get; set; }
            public string AdjudicationType { get; set; }
            public string EscalationLadder { get; set; }
            public string BranchingLogic { get; set; }
            public bool CollectLogs { get; set; }
            public bool CollectNetwork { get; set; }
            public bool CollectEndpoint { get; set; }
            public bool CollectChat { get; set; }
            public string PerformanceMetrics { get; set; }
        }

        private class SeedScenarioTimeline
        {
            public int ExerciseDuration { get; set; }
            public List<SeedTimelineEvent> Events { get; set; }
        }

        private class SeedTimelineEvent
        {
            public string Time { get; set; }
            public int Number { get; set; }
            public string Assigned { get; set; }
            public string Description { get; set; }
            public string Status { get; set; }
        }

        private class SeedObjective
        {
            public string ScenarioName { get; set; }
            public string ParentName { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public string Type { get; set; }
            public int Priority { get; set; }
            public string SuccessCriteria { get; set; }
            public string Assigned { get; set; }
        }

        private class SeedMachine
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Fqdn { get; set; }
            public string Host { get; set; }
            public string HostIp { get; set; }
            public string CurrentUsername { get; set; }
            public string ClientVersion { get; set; }
            public int StatusUp { get; set; }
        }

        private class SeedNpc
        {
            public string Id { get; set; }
            public string Campaign { get; set; }
            public string Enclave { get; set; }
            public string Team { get; set; }
            public Ghosts.Animator.Models.NpcProfile NpcProfile { get; set; }
        }

        private class SeedTimelineHistory
        {
            public string MachineId { get; set; }
            public string Handler { get; set; }
            public string Command { get; set; }
            public string CommandArg { get; set; }
            public string Result { get; set; }
            public double OffsetHours { get; set; }
        }

        private class SeedHealthHistory
        {
            public string MachineId { get; set; }
            public bool Internet { get; set; }
            public bool Permissions { get; set; }
            public long ExecutionTime { get; set; }
            public double OffsetHours { get; set; }
        }

        private class SeedMachineHistory
        {
            public string MachineId { get; set; }
            public int Type { get; set; }
            public string Object { get; set; }
            public double OffsetHours { get; set; }
        }

        private class SeedSocialConnection
        {
            public string Id { get; set; }
            public string NpcId { get; set; }
            public string ConnectedNpcId { get; set; }
            public string Name { get; set; }
            public string Distance { get; set; }
            public decimal RelationshipStatus { get; set; }
            public double OffsetHours { get; set; }
        }

        private class SeedInteraction
        {
            public string SocialConnectionId { get; set; }
            public long Step { get; set; }
            public int Value { get; set; }
            public double OffsetHours { get; set; }
        }

        private class SeedActivity
        {
            public string NpcId { get; set; }
            public int ActivityType { get; set; }
            public string Detail { get; set; }
            public double OffsetHours { get; set; }
        }

        private class SeedBelief
        {
            public string NpcId { get; set; }
            public string ToNpcId { get; set; }
            public string FromNpcId { get; set; }
            public string Name { get; set; }
            public long Step { get; set; }
            public decimal Likelihood { get; set; }
            public decimal Posterior { get; set; }
            public double OffsetHours { get; set; }
        }

        private class SeedLearning
        {
            public string NpcId { get; set; }
            public string ToNpcId { get; set; }
            public string FromNpcId { get; set; }
            public string Topic { get; set; }
            public long Step { get; set; }
            public int Value { get; set; }
            public double OffsetHours { get; set; }
        }

        private class SeedPreference
        {
            public string NpcId { get; set; }
            public string ToNpcId { get; set; }
            public string FromNpcId { get; set; }
            public string Name { get; set; }
            public long Step { get; set; }
            public decimal Weight { get; set; }
            public decimal Strength { get; set; }
            public double OffsetHours { get; set; }
        }
        // ===== Map Feature Table Creation =====

        private static async Task EnsureMapFeaturesTableExists(ApplicationDbContext context, ILogger<DbInitializer> logger)
        {
            try
            {
                var connection = context.Database.GetDbConnection();
                if (connection.State != System.Data.ConnectionState.Open)
                    await connection.OpenAsync();

                using var checkCmd = connection.CreateCommand();
                checkCmd.CommandText = "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'map_features');";
                var exists = (bool)(await checkCmd.ExecuteScalarAsync() ?? false);

                if (exists)
                {
                    logger.LogInformation("map_features table already exists");
                    return;
                }

                logger.LogInformation("Creating map_features table...");

                using var createCmd = connection.CreateCommand();
                createCmd.CommandText = @"
                    CREATE TABLE map_features (
                        id                SERIAL PRIMARY KEY,
                        featuretype       VARCHAR(50) NOT NULL DEFAULT '',
                        entityid          VARCHAR(200) NOT NULL DEFAULT '',
                        scenarioid        INTEGER,
                        executionid       INTEGER,
                        label             VARCHAR(500) NOT NULL DEFAULT '',
                        description       TEXT NOT NULL DEFAULT '',
                        latitude          DOUBLE PRECISION NOT NULL DEFAULT 0,
                        longitude         DOUBLE PRECISION NOT NULL DEFAULT 0,
                        geometry          JSONB,
                        status            VARCHAR(50) NOT NULL DEFAULT 'Active',
                        category          VARCHAR(100) NOT NULL DEFAULT '',
                        team              VARCHAR(100) NOT NULL DEFAULT '',
                        properties        JSONB NOT NULL DEFAULT '{}'::jsonb,
                        sourcefeatureid   VARCHAR(200),
                        targetfeatureid   VARCHAR(200),
                        createdat         TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
                        updatedat         TIMESTAMP WITHOUT TIME ZONE,
                        validfrom         TIMESTAMP WITHOUT TIME ZONE,
                        validto           TIMESTAMP WITHOUT TIME ZONE
                    );

                    CREATE INDEX ix_map_features_featuretype_executionid ON map_features (featuretype, executionid);
                    CREATE INDEX ix_map_features_entityid_featuretype    ON map_features (entityid, featuretype);
                    CREATE INDEX ix_map_features_scenarioid              ON map_features (scenarioid);
                    CREATE INDEX ix_map_features_executionid             ON map_features (executionid);
                    CREATE INDEX ix_map_features_validfrom               ON map_features (validfrom);
                ";
                await createCmd.ExecuteNonQueryAsync();

                logger.LogInformation("map_features table created successfully");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create map_features table");
                throw;
            }
        }

        // ===== Map Feature Seeding =====

        private static async Task SeedMapFeatures(ApplicationDbContext context, ILogger<DbInitializer> logger)
        {
            if (await context.MapFeatures.AnyAsync())
            {
                logger.LogInformation("Map features already seeded");
                return;
            }

            var scenario = await context.Scenarios.FirstOrDefaultAsync();
            if (scenario == null) return;

            // Create a demo execution for the map
            var execution = new Execution
            {
                ScenarioId = scenario.Id,
                Name = $"{scenario.Name} — Map Demo",
                Description = "Auto-generated execution for Execution Map demonstration",
                Status = ExecutionStatus.Running,
                CreatedAt = DateTime.UtcNow.AddHours(-2),
                StartedAt = DateTime.UtcNow.AddHours(-2),
                Configuration = "{}",
                ParameterOverrides = "{}",
                Metrics = "{\"activeEntities\": 24, \"eventsProcessed\": 47}",
                ErrorDetails = "{}"
            };

            context.Executions.Add(execution);
            await context.SaveChangesAsync();

            var execId = execution.Id;
            var scenId = scenario.Id;
            var baseTime = DateTime.UtcNow.AddHours(-2);

            // ── Geography: GLOBAL DISTRIBUTION ──
            // The scenario models a multinational APT campaign with infrastructure
            // and targets distributed across all continents.
            //
            // Blue — Primary targets & defenders:
            //   Tbilisi, Georgia (Caucasus HQ)         41.69°N  44.80°E
            //   Rustavi, Georgia (DMZ)                  41.55°N  45.01°E
            //   Mtskheta, Georgia (DC Alpha)            41.84°N  44.72°E
            //   Kutaisi, Georgia (DC Bravo / DR)        42.27°N  42.70°E
            //   Gori, Georgia (FOB Hammer)              41.98°N  44.11°E
            //   Telavi, Georgia (SATCOM)                41.92°N  45.47°E
            //   Stuttgart, Germany (EUCOM NOC)          48.78°N   9.18°E
            //   Fort Meade, Maryland (CYBERCOM SOC)     39.11°N -76.73°W
            //   Yokosuka, Japan (INDOPACOM relay)       35.28°N 139.67°E
            //   Nairobi, Kenya (AFRICOM liaison)        -1.29°S  36.82°E
            //   Bogotá, Colombia (SOUTHCOM liaison)      4.71°N -74.07°W
            //   Canberra, Australia (Five Eyes partner) -35.28°S 149.13°E
            //
            // Red — Adversary infrastructure:
            //   Moscow, Russia (primary C2)             55.76°N  37.62°E
            //   Rostov-on-Don, Russia (relay)           47.24°N  39.70°E
            //   Odessa, Ukraine (bulletproof hosting)   46.48°N  30.72°E
            //   Shanghai, China (proxy C2)              31.23°N 121.47°E
            //   São Paulo, Brazil (bulletproof VPS)    -23.55°S -46.63°W
            //   Lagos, Nigeria (money mule / rebound)    6.52°N   3.38°E
            //   Tehran, Iran (ally staging node)        35.69°N  51.39°E

            var features = new List<MapFeature>
            {
                // ══════════════════════════════════════════════════
                // ── Sites / Facilities (Global) ──
                // ══════════════════════════════════════════════════

                // --- Blue / Defender sites ---
                Mf("Site", "site-hq", scenId, execId, "Joint Operations Center — Tbilisi", "Primary C2 facility, Atropia MoD compound", 41.6934, 44.8015, "Online", "HQ", "Blue Team"),
                Mf("Site", "site-dmz", scenId, execId, "DMZ Enclave — Rustavi", "Internet-facing services and email gateways", 41.5493, 45.0128, "Online", "DMZ", "Blue Team"),
                Mf("Site", "site-dc1", scenId, execId, "Data Center Alpha — Mtskheta", "Primary server room and domain controllers", 41.8427, 44.7187, "Online", "DataCenter", "Blue Team"),
                Mf("Site", "site-dc2", scenId, execId, "Data Center Bravo — Kutaisi", "Backup and disaster recovery, western Atropia", 42.2679, 42.6946, "Online", "DataCenter", "Blue Team"),
                Mf("Site", "site-remote", scenId, execId, "Liaison Office — Gyumri, Gorgas", "Forward-deployed coalition liaison office", 40.7929, 43.8465, "Online", "RemoteOffice", "Blue Team"),
                Mf("Site", "site-fob", scenId, execId, "FOB Hammer — Gori", "Forward operating base, signals company", 41.9816, 44.1135, "Online", "FOB", "Blue Team"),
                Mf("Site", "site-satcom", scenId, execId, "SATCOM Relay — Telavi", "Military satellite uplink, eastern Atropia", 41.9198, 45.4735, "Online", "SATCOM", "Blue Team"),
                Mf("Site", "site-civsoc", scenId, execId, "CERT-Atropia — Tbilisi", "Civilian cyber emergency response center", 41.7152, 44.8271, "Online", "CERT", "Blue Team"),
                Mf("Site", "site-eucom", scenId, execId, "EUCOM NOC — Stuttgart", "European Command network operations center", 48.7758, 9.1829, "Online", "NOC", "Blue Team"),
                Mf("Site", "site-cybercom", scenId, execId, "CYBERCOM SOC — Fort Meade", "US Cyber Command security operations center", 39.1086, -76.7295, "Online", "SOC", "Blue Team"),
                Mf("Site", "site-indopacom", scenId, execId, "INDOPACOM Relay — Yokosuka", "Indo-Pacific Command cyber relay station", 35.2834, 139.6720, "Online", "Relay", "Blue Team"),
                Mf("Site", "site-africom", scenId, execId, "AFRICOM Liaison — Nairobi", "Africa Command cyber liaison office", -1.2921, 36.8219, "Online", "Liaison", "Blue Team"),
                Mf("Site", "site-southcom", scenId, execId, "SOUTHCOM Liaison — Bogotá", "Southern Command cyber liaison office", 4.7110, -74.0721, "Online", "Liaison", "Blue Team"),
                Mf("Site", "site-fvey", scenId, execId, "ASD / Five Eyes — Canberra", "Australian Signals Directorate partner facility", -35.2809, 149.1300, "Online", "Partner", "Blue Team"),

                // --- Red / Adversary sites ---
                Mf("Site", "site-red-c2-dc", scenId, execId, "Red C2 Hub — Moscow", "Primary adversary C2 data center", 55.7558, 37.6173, "Active", "DataCenter", "Red Team"),
                Mf("Site", "site-redcell", scenId, execId, "Red Cell Staging — Rostov-on-Don", "Adversary staging infra, southern Donovia", 47.2357, 39.7015, "Active", "Staging", "Red Team"),
                Mf("Site", "site-red-bullet", scenId, execId, "Bulletproof Hosting — Odessa", "Adversary bulletproof hosting provider", 46.4825, 30.7233, "Active", "Staging", "Red Team"),
                Mf("Site", "site-red-shanghai", scenId, execId, "Proxy C2 Node — Shanghai", "Adversary proxy C2 in APAC", 31.2304, 121.4737, "Active", "Proxy", "Red Team"),
                Mf("Site", "site-red-saopaulo", scenId, execId, "Bulletproof VPS — São Paulo", "Adversary VPS in South America", -23.5505, -46.6333, "Active", "Staging", "Red Team"),
                Mf("Site", "site-red-lagos", scenId, execId, "Rebound Node — Lagos", "Money-mule and rebound infrastructure", 6.5244, 3.3792, "Active", "Staging", "Red Team"),
                Mf("Site", "site-red-tehran", scenId, execId, "Ally Staging — Tehran", "Allied nation-state staging node", 35.6892, 51.3890, "Active", "Staging", "Red Team"),

                // ══════════════════════════════════════════════════
                // ── Machines / Endpoints ──
                // ══════════════════════════════════════════════════

                // --- Mtskheta DC cluster (Georgia) ---
                Mf("Machine", "m-dc01", scenId, execId, "DC01.hq.atropia.mil", "Primary domain controller", 41.8440, 44.7165, "Online", "Server", "Blue Team", "{\"os\":\"Windows Server 2022\",\"ip\":\"10.1.1.10\",\"role\":\"Domain Controller\"}"),
                Mf("Machine", "m-dc02", scenId, execId, "DC02.hq.atropia.mil", "Secondary domain controller", 41.8415, 44.7210, "Online", "Server", "Blue Team", "{\"os\":\"Windows Server 2022\",\"ip\":\"10.1.1.11\",\"role\":\"Domain Controller\"}"),
                Mf("Machine", "m-ca01", scenId, execId, "CA01.hq.atropia.mil", "Certificate authority server", 41.8432, 44.7190, "Online", "Server", "Blue Team", "{\"os\":\"Windows Server 2022\",\"ip\":\"10.1.1.15\",\"role\":\"PKI/CA\"}"),
                Mf("Machine", "m-sql01", scenId, execId, "SQL01.hq.atropia.mil", "Primary SQL database", 41.8425, 44.7200, "Online", "Server", "Blue Team", "{\"os\":\"Windows Server 2022\",\"ip\":\"10.1.1.30\",\"role\":\"SQL Server\"}"),
                Mf("Machine", "m-file01", scenId, execId, "FILE01.hq.atropia.mil", "Classified file server", 41.8445, 44.7175, "Online", "Server", "Blue Team", "{\"os\":\"Windows Server 2022\",\"ip\":\"10.1.1.35\",\"role\":\"File Server\"}"),

                // --- Tbilisi HQ cluster (Georgia) ---
                Mf("Machine", "m-siem01", scenId, execId, "SIEM01.hq.atropia.mil", "Security information & event mgmt", 41.6960, 44.7985, "Online", "Server", "Blue Team", "{\"os\":\"RHEL 9\",\"ip\":\"10.1.2.50\",\"role\":\"SIEM\"}"),
                Mf("Machine", "m-siem02", scenId, execId, "SIEM02.hq.atropia.mil", "SIEM log collector", 41.6955, 44.7992, "Online", "Server", "Blue Team", "{\"os\":\"RHEL 9\",\"ip\":\"10.1.2.51\",\"role\":\"Log Collector\"}"),
                Mf("Machine", "m-edr01", scenId, execId, "EDR01.hq.atropia.mil", "Endpoint detection & response console", 41.6952, 44.8000, "Online", "Server", "Blue Team", "{\"os\":\"Ubuntu 22.04\",\"ip\":\"10.1.2.60\",\"role\":\"EDR Console\"}"),
                Mf("Machine", "m-ws001", scenId, execId, "WS001.user.atropia.mil", "SOC Analyst workstation — Chen", 41.6942, 44.8035, "Online", "Workstation", "Blue Team", "{\"os\":\"Windows 11\",\"ip\":\"10.3.1.101\",\"role\":\"Workstation\"}"),
                Mf("Machine", "m-ws002", scenId, execId, "WS002.user.atropia.mil", "SOC Analyst workstation — Rivera", 41.6928, 44.8050, "Online", "Workstation", "Blue Team", "{\"os\":\"Windows 11\",\"ip\":\"10.3.1.102\",\"role\":\"Workstation\"}"),
                Mf("Machine", "m-ws003", scenId, execId, "WS003.user.atropia.mil", "Incident Commander station — Torres", 41.6950, 44.7990, "Online", "Workstation", "Blue Team", "{\"os\":\"Windows 11\",\"ip\":\"10.3.1.103\",\"role\":\"Workstation\"}"),
                Mf("Machine", "m-ws004", scenId, execId, "WS004.user.atropia.mil", "Admin workstation (compromised) — Park", 41.6920, 44.8060, "Compromised", "Workstation", "Blue Team", "{\"os\":\"Windows 11\",\"ip\":\"10.3.1.104\",\"role\":\"Admin Workstation\",\"compromise\":\"OAuth token stolen\"}"),
                Mf("Machine", "m-ws005", scenId, execId, "WS005.user.atropia.mil", "Intel analyst workstation — Nguyen", 41.6935, 44.8010, "Online", "Workstation", "Blue Team", "{\"os\":\"Windows 11\",\"ip\":\"10.3.1.105\",\"role\":\"Workstation\"}"),
                Mf("Machine", "m-ws006", scenId, execId, "WS006.user.atropia.mil", "Network engineer workstation — Okafor", 41.6948, 44.8020, "Online", "Workstation", "Blue Team", "{\"os\":\"Windows 11\",\"ip\":\"10.3.1.106\",\"role\":\"Workstation\"}"),
                Mf("Machine", "m-ws007", scenId, execId, "WS007.user.atropia.mil", "Forensics workstation — Martinez", 41.6925, 44.8045, "Online", "Workstation", "Blue Team", "{\"os\":\"Ubuntu 22.04\",\"ip\":\"10.3.1.107\",\"role\":\"Forensics\"}"),
                Mf("Machine", "m-print01", scenId, execId, "PRINT01.hq.atropia.mil", "Network MFP — HQ Ops floor", 41.6940, 44.8042, "Online", "Peripheral", "Blue Team", "{\"os\":\"Firmware\",\"ip\":\"10.3.1.200\",\"role\":\"Printer\"}"),

                // --- DMZ Rustavi (Georgia) ---
                Mf("Machine", "m-mail01", scenId, execId, "MAIL01.dmz.atropia.mil", "Exchange/365 mail gateway", 41.5510, 45.0100, "Degraded", "Server", "Blue Team", "{\"os\":\"Windows Server 2022\",\"ip\":\"10.2.1.20\",\"role\":\"Mail Gateway\"}"),
                Mf("Machine", "m-mail02", scenId, execId, "MAIL02.dmz.atropia.mil", "Backup mail gateway", 41.5505, 45.0115, "Online", "Server", "Blue Team", "{\"os\":\"Windows Server 2022\",\"ip\":\"10.2.1.22\",\"role\":\"Mail Gateway\"}"),
                Mf("Machine", "m-web01", scenId, execId, "WEB01.dmz.atropia.mil", "Public web server", 41.5475, 45.0155, "Online", "Server", "Blue Team", "{\"os\":\"Ubuntu 22.04\",\"ip\":\"10.2.1.21\",\"role\":\"Web Server\"}"),
                Mf("Machine", "m-web02", scenId, execId, "WEB02.dmz.atropia.mil", "Internal web portal", 41.5480, 45.0145, "Online", "Server", "Blue Team", "{\"os\":\"Ubuntu 22.04\",\"ip\":\"10.2.1.23\",\"role\":\"Web Server\"}"),
                Mf("Machine", "m-vpn01", scenId, execId, "VPN01.dmz.atropia.mil", "VPN concentrator", 41.5520, 45.0080, "Online", "Appliance", "Blue Team", "{\"os\":\"FortiGate\",\"ip\":\"10.2.1.5\",\"role\":\"VPN Gateway\"}"),
                Mf("Machine", "m-fw01", scenId, execId, "FW01.perimeter.atropia.mil", "Perimeter firewall", 41.5500, 45.0140, "Online", "Appliance", "Blue Team", "{\"os\":\"PaloAlto\",\"ip\":\"10.0.0.1\",\"role\":\"Firewall\"}"),
                Mf("Machine", "m-fw02", scenId, execId, "FW02.internal.atropia.mil", "Internal segmentation firewall", 41.5498, 45.0120, "Online", "Appliance", "Blue Team", "{\"os\":\"PaloAlto\",\"ip\":\"10.0.0.2\",\"role\":\"Internal Firewall\"}"),
                Mf("Machine", "m-proxy01", scenId, execId, "PROXY01.dmz.atropia.mil", "Web proxy / content filter", 41.5488, 45.0132, "Online", "Appliance", "Blue Team", "{\"os\":\"Squid/RHEL\",\"ip\":\"10.2.1.8\",\"role\":\"Web Proxy\"}"),
                Mf("Machine", "m-dns01", scenId, execId, "DNS01.dmz.atropia.mil", "External DNS resolver", 41.5515, 45.0095, "Online", "Server", "Blue Team", "{\"os\":\"BIND/RHEL\",\"ip\":\"10.2.1.53\",\"role\":\"DNS\"}"),

                // --- Kutaisi DR (Georgia) ---
                Mf("Machine", "m-dc03", scenId, execId, "DC03.dr.atropia.mil", "DR domain controller", 42.2690, 42.6960, "Online", "Server", "Blue Team", "{\"os\":\"Windows Server 2022\",\"ip\":\"10.4.1.10\",\"role\":\"Domain Controller\"}"),
                Mf("Machine", "m-backup01", scenId, execId, "BACKUP01.dr.atropia.mil", "Backup server and tape library", 42.2670, 42.6930, "Online", "Server", "Blue Team", "{\"os\":\"Windows Server 2022\",\"ip\":\"10.4.1.30\",\"role\":\"Backup\"}"),
                Mf("Machine", "m-sql02", scenId, execId, "SQL02.dr.atropia.mil", "DR SQL replica", 42.2685, 42.6950, "Online", "Server", "Blue Team", "{\"os\":\"Windows Server 2022\",\"ip\":\"10.4.1.31\",\"role\":\"SQL Replica\"}"),

                // --- FOB Hammer, Gori (Georgia) ---
                Mf("Machine", "m-fob-ws01", scenId, execId, "WS-FOB01.fob.atropia.mil", "Signals officer workstation", 41.9820, 44.1140, "Online", "Workstation", "Blue Team", "{\"os\":\"Windows 11\",\"ip\":\"10.5.1.10\",\"role\":\"Workstation\"}"),
                Mf("Machine", "m-fob-ws02", scenId, execId, "WS-FOB02.fob.atropia.mil", "SIGINT workstation", 41.9812, 44.1128, "Online", "Workstation", "Blue Team", "{\"os\":\"RHEL 9\",\"ip\":\"10.5.1.11\",\"role\":\"SIGINT\"}"),
                Mf("Machine", "m-fob-radio", scenId, execId, "RADIO01.fob.atropia.mil", "Tactical radio gateway", 41.9825, 44.1150, "Online", "Appliance", "Blue Team", "{\"os\":\"Firmware\",\"ip\":\"10.5.1.1\",\"role\":\"Radio Gateway\"}"),

                // --- SATCOM Telavi (Georgia) ---
                Mf("Machine", "m-sat-term01", scenId, execId, "SATTERM01.sat.atropia.mil", "SATCOM terminal", 41.9205, 45.4740, "Online", "Appliance", "Blue Team", "{\"os\":\"Firmware\",\"ip\":\"10.6.1.1\",\"role\":\"SATCOM Terminal\"}"),
                Mf("Machine", "m-sat-enc01", scenId, execId, "SATENC01.sat.atropia.mil", "SATCOM encryptor", 41.9190, 45.4730, "Online", "Appliance", "Blue Team", "{\"os\":\"Firmware\",\"ip\":\"10.6.1.2\",\"role\":\"Encryptor\"}"),

                // --- EUCOM Stuttgart (Germany) ---
                Mf("Machine", "m-eu-siem", scenId, execId, "SIEM-EU01.eucom.mil", "EUCOM SIEM collector", 48.7765, 9.1840, "Online", "Server", "Blue Team", "{\"os\":\"Splunk/RHEL\",\"ip\":\"10.20.1.50\",\"role\":\"SIEM\"}"),
                Mf("Machine", "m-eu-ws01", scenId, execId, "WS-EU01.eucom.mil", "EUCOM NOC analyst workstation", 48.7750, 9.1820, "Online", "Workstation", "Blue Team", "{\"os\":\"Windows 11\",\"ip\":\"10.20.1.101\",\"role\":\"Workstation\"}"),
                Mf("Machine", "m-eu-fw", scenId, execId, "FW-EU01.eucom.mil", "EUCOM perimeter firewall", 48.7770, 9.1810, "Online", "Appliance", "Blue Team", "{\"os\":\"Fortinet\",\"ip\":\"10.20.0.1\",\"role\":\"Firewall\"}"),

                // --- CYBERCOM Fort Meade (Maryland, USA) ---
                Mf("Machine", "m-cc-siem", scenId, execId, "SIEM-CC01.cybercom.mil", "CYBERCOM central SIEM", 39.1090, -76.7280, "Online", "Server", "Blue Team", "{\"os\":\"Elastic/RHEL\",\"ip\":\"10.30.1.50\",\"role\":\"SIEM\"}"),
                Mf("Machine", "m-cc-ws01", scenId, execId, "WS-CC01.cybercom.mil", "CYBERCOM analyst workstation", 39.1080, -76.7300, "Online", "Workstation", "Blue Team", "{\"os\":\"Windows 11\",\"ip\":\"10.30.1.101\",\"role\":\"Workstation\"}"),
                Mf("Machine", "m-cc-ws02", scenId, execId, "WS-CC02.cybercom.mil", "CYBERCOM threat hunter", 39.1075, -76.7310, "Online", "Workstation", "Blue Team", "{\"os\":\"Ubuntu 22.04\",\"ip\":\"10.30.1.102\",\"role\":\"Threat Hunter\"}"),
                Mf("Machine", "m-cc-tip", scenId, execId, "TIP-CC01.cybercom.mil", "Threat intelligence platform", 39.1095, -76.7275, "Online", "Server", "Blue Team", "{\"os\":\"MISP/RHEL\",\"ip\":\"10.30.1.60\",\"role\":\"TIP\"}"),

                // --- INDOPACOM Yokosuka (Japan) ---
                Mf("Machine", "m-ip-siem", scenId, execId, "SIEM-IP01.indopacom.mil", "INDOPACOM SIEM relay", 35.2840, 139.6730, "Online", "Server", "Blue Team", "{\"os\":\"Splunk/RHEL\",\"ip\":\"10.40.1.50\",\"role\":\"SIEM\"}"),
                Mf("Machine", "m-ip-ws01", scenId, execId, "WS-IP01.indopacom.mil", "INDOPACOM cyber analyst", 35.2828, 139.6710, "Online", "Workstation", "Blue Team", "{\"os\":\"Windows 11\",\"ip\":\"10.40.1.101\",\"role\":\"Workstation\"}"),

                // --- AFRICOM Nairobi (Kenya) ---
                Mf("Machine", "m-af-ws01", scenId, execId, "WS-AF01.africom.mil", "AFRICOM liaison analyst", -1.2930, 36.8230, "Online", "Workstation", "Blue Team", "{\"os\":\"Windows 11\",\"ip\":\"10.50.1.101\",\"role\":\"Workstation\"}"),
                Mf("Machine", "m-af-vpn", scenId, execId, "VPN-AF01.africom.mil", "AFRICOM VPN appliance", -1.2915, 36.8210, "Online", "Appliance", "Blue Team", "{\"os\":\"FortiGate\",\"ip\":\"10.50.0.1\",\"role\":\"VPN\"}"),

                // --- SOUTHCOM Bogotá (Colombia) ---
                Mf("Machine", "m-sc-ws01", scenId, execId, "WS-SC01.southcom.mil", "SOUTHCOM liaison analyst", 4.7115, -74.0730, "Online", "Workstation", "Blue Team", "{\"os\":\"Windows 11\",\"ip\":\"10.60.1.101\",\"role\":\"Workstation\"}"),
                Mf("Machine", "m-sc-vpn", scenId, execId, "VPN-SC01.southcom.mil", "SOUTHCOM VPN appliance", 4.7105, -74.0710, "Online", "Appliance", "Blue Team", "{\"os\":\"FortiGate\",\"ip\":\"10.60.0.1\",\"role\":\"VPN\"}"),

                // --- Five Eyes Canberra (Australia) ---
                Mf("Machine", "m-fv-siem", scenId, execId, "SIEM-FV01.asd.gov.au", "ASD SIEM exchange node", -35.2815, 149.1310, "Online", "Server", "Blue Team", "{\"os\":\"Elastic/RHEL\",\"ip\":\"10.70.1.50\",\"role\":\"SIEM\"}"),
                Mf("Machine", "m-fv-ws01", scenId, execId, "WS-FV01.asd.gov.au", "ASD cyber analyst", -35.2800, 149.1290, "Online", "Workstation", "Blue Team", "{\"os\":\"Ubuntu 22.04\",\"ip\":\"10.70.1.101\",\"role\":\"Workstation\"}"),

                // --- CERT-Atropia Tbilisi ---
                Mf("Machine", "m-cert-ws01", scenId, execId, "CERT-WS01.cert.atropia.gov", "CERT analyst workstation", 41.7158, 44.8280, "Online", "Workstation", "Blue Team", "{\"os\":\"Ubuntu 22.04\",\"ip\":\"10.7.1.10\",\"role\":\"CERT Analyst\"}"),
                Mf("Machine", "m-cert-sandbox", scenId, execId, "SANDBOX01.cert.atropia.gov", "Malware detonation sandbox", 41.7145, 44.8265, "Online", "Server", "Blue Team", "{\"os\":\"RHEL 9\",\"ip\":\"10.7.1.20\",\"role\":\"Sandbox\"}"),

                // --- Gyumri Liaison (Armenia) ---
                Mf("Machine", "m-remote-ws01", scenId, execId, "WS-LNO01.gorgas.mil", "Liaison officer workstation", 40.7935, 43.8470, "Online", "Workstation", "Blue Team", "{\"os\":\"Windows 11\",\"ip\":\"10.8.1.10\",\"role\":\"Workstation\"}"),
                Mf("Machine", "m-remote-vpn", scenId, execId, "VPN-LNO01.gorgas.mil", "Liaison VPN appliance", 40.7940, 43.8455, "Online", "Appliance", "Blue Team", "{\"os\":\"FortiGate\",\"ip\":\"10.8.1.1\",\"role\":\"VPN\"}"),

                // --- Adversary machines (Global) ---
                Mf("Machine", "m-red-c2", scenId, execId, "c2.donovia.adversary", "Primary C2 server, Moscow", 55.7558, 37.6173, "Active", "Server", "Red Team", "{\"os\":\"Kali\",\"ip\":\"203.0.113.42\",\"role\":\"C2 Server\"}"),
                Mf("Machine", "m-red-c2b", scenId, execId, "c2-backup.donovia.adversary", "Backup C2 server, Moscow", 55.7545, 37.6200, "Active", "Server", "Red Team", "{\"os\":\"Debian\",\"ip\":\"203.0.113.43\",\"role\":\"Backup C2\"}"),
                Mf("Machine", "m-red-relay", scenId, execId, "relay.proxy.adversary", "Relay node, Rostov-on-Don", 47.2313, 39.7233, "Active", "Server", "Red Team", "{\"os\":\"Ubuntu\",\"ip\":\"198.51.100.7\",\"role\":\"Relay\"}"),
                Mf("Machine", "m-red-phish", scenId, execId, "phish.proxy.adversary", "Phishing server, Rostov", 47.2340, 39.7180, "Active", "Server", "Red Team", "{\"os\":\"Ubuntu\",\"ip\":\"198.51.100.12\",\"role\":\"Phishing Server\"}"),
                Mf("Machine", "m-red-exfil", scenId, execId, "exfil.drop.adversary", "Exfil dropbox, Odessa", 46.4830, 30.7240, "Active", "Server", "Red Team", "{\"os\":\"Debian\",\"ip\":\"192.0.2.88\",\"role\":\"Exfil Drop\"}"),
                Mf("Machine", "m-red-dns", scenId, execId, "dns-tunnel.adversary", "DNS tunnel endpoint, Odessa", 46.4820, 30.7225, "Active", "Server", "Red Team", "{\"os\":\"Debian\",\"ip\":\"192.0.2.53\",\"role\":\"DNS Tunnel\"}"),
                Mf("Machine", "m-red-shanghai", scenId, execId, "proxy-cn.adversary", "Proxy C2 relay, Shanghai", 31.2310, 121.4740, "Active", "Server", "Red Team", "{\"os\":\"Ubuntu\",\"ip\":\"203.0.113.100\",\"role\":\"Proxy C2\"}"),
                Mf("Machine", "m-red-saopaulo", scenId, execId, "vps-br.adversary", "Bulletproof VPS, São Paulo", -23.5510, -46.6340, "Active", "Server", "Red Team", "{\"os\":\"Debian\",\"ip\":\"192.0.2.200\",\"role\":\"Bulletproof VPS\"}"),
                Mf("Machine", "m-red-lagos", scenId, execId, "rebound-ng.adversary", "Rebound node, Lagos", 6.5250, 3.3800, "Active", "Server", "Red Team", "{\"os\":\"Ubuntu\",\"ip\":\"192.0.2.150\",\"role\":\"Rebound\"}"),
                Mf("Machine", "m-red-tehran", scenId, execId, "staging-ir.adversary", "Ally staging node, Tehran", 35.6900, 51.3895, "Active", "Server", "Red Team", "{\"os\":\"CentOS\",\"ip\":\"198.51.100.50\",\"role\":\"Staging\"}"),

                // ══════════════════════════════════════════════════
                // ── NPCs / Agents ──
                // ══════════════════════════════════════════════════

                // Blue — Tbilisi HQ
                Mf("Npc", "npc-analyst1", scenId, execId, "SGT Chen — SOC Analyst", "Junior SOC analyst monitoring alerts", 41.6938, 44.8025, "Active", "Analyst", "Blue Team", "{\"rank\":\"SGT\",\"mos\":\"25D\",\"shift\":\"Day\"}"),
                Mf("Npc", "npc-analyst2", scenId, execId, "SPC Rivera — SOC Analyst", "SOC analyst on threat hunting duty", 41.6930, 44.8042, "Active", "Analyst", "Blue Team", "{\"rank\":\"SPC\",\"mos\":\"25D\",\"shift\":\"Day\"}"),
                Mf("Npc", "npc-analyst3", scenId, execId, "SGT Nguyen — Intel Analyst", "Intelligence analyst cross-referencing IOCs", 41.6935, 44.8010, "Active", "Analyst", "Blue Team", "{\"rank\":\"SGT\",\"mos\":\"35T\",\"shift\":\"Day\"}"),
                Mf("Npc", "npc-ic", scenId, execId, "MAJ Torres — Incident Commander", "Exercise incident commander", 41.6945, 44.7998, "Active", "Commander", "Blue Team", "{\"rank\":\"MAJ\",\"mos\":\"17A\",\"role\":\"IC\"}"),
                Mf("Npc", "npc-admin", scenId, execId, "SSG Park — Sys Admin", "System administrator (compromised)", 41.6922, 44.8055, "Active", "Admin", "Blue Team", "{\"rank\":\"SSG\",\"mos\":\"25B\",\"note\":\"Account compromised at T+30m\"}"),
                Mf("Npc", "npc-neteng", scenId, execId, "SFC Okafor — Network Engineer", "Senior network engineer", 41.6948, 44.8020, "Active", "Engineer", "Blue Team", "{\"rank\":\"SFC\",\"mos\":\"25H\",\"shift\":\"Day\"}"),
                Mf("Npc", "npc-forensics", scenId, execId, "SSG Martinez — Forensics", "Digital forensics examiner", 41.6925, 44.8045, "Active", "Forensics", "Blue Team", "{\"rank\":\"SSG\",\"mos\":\"25D\",\"shift\":\"Day\"}"),
                Mf("Npc", "npc-watch", scenId, execId, "CPL Burke — Night Watch", "Night-shift SOC analyst", 41.6940, 44.8032, "Inactive", "Analyst", "Blue Team", "{\"rank\":\"CPL\",\"mos\":\"25D\",\"shift\":\"Night\"}"),
                // Blue — DC / DMZ / FOB
                Mf("Npc", "npc-dcadmin1", scenId, execId, "SGT Yilmaz — DC Admin", "Data Center Alpha sysadmin", 41.8438, 44.7170, "Active", "Admin", "Blue Team", "{\"rank\":\"SGT\",\"mos\":\"25B\",\"site\":\"Mtskheta\"}"),
                Mf("Npc", "npc-dmzadmin", scenId, execId, "SPC Kim — DMZ Admin", "DMZ enclave administrator", 41.5505, 45.0110, "Active", "Admin", "Blue Team", "{\"rank\":\"SPC\",\"mos\":\"25B\",\"site\":\"Rustavi\"}"),
                Mf("Npc", "npc-sigoff", scenId, execId, "CPT Aziz — Signals Officer", "FOB Hammer signals CO", 41.9818, 44.1138, "Active", "Officer", "Blue Team", "{\"rank\":\"CPT\",\"mos\":\"25A\",\"site\":\"Gori\"}"),
                Mf("Npc", "npc-certlead", scenId, execId, "Dr. Khoshtaria — CERT Lead", "CERT-Atropia chief analyst", 41.7155, 44.8275, "Active", "Analyst", "Blue Team", "{\"role\":\"CERT Lead\",\"civilian\":true}"),
                // Blue — Global coalition
                Mf("Npc", "npc-eu-analyst", scenId, execId, "CPT Weber — EUCOM Analyst", "EUCOM NOC watch officer", 48.7752, 9.1825, "Active", "Analyst", "Blue Team", "{\"rank\":\"CPT\",\"mos\":\"17A\",\"site\":\"Stuttgart\"}"),
                Mf("Npc", "npc-cc-hunter", scenId, execId, "MAJ Jackson — CYBERCOM Hunter", "CYBERCOM threat hunt team lead", 39.1082, -76.7305, "Active", "Analyst", "Blue Team", "{\"rank\":\"MAJ\",\"mos\":\"17A\",\"site\":\"Fort Meade\"}"),
                Mf("Npc", "npc-cc-analyst", scenId, execId, "SGT Patel — CYBERCOM Analyst", "CYBERCOM intelligence analyst", 39.1078, -76.7312, "Active", "Analyst", "Blue Team", "{\"rank\":\"SGT\",\"mos\":\"35T\",\"site\":\"Fort Meade\"}"),
                Mf("Npc", "npc-ip-analyst", scenId, execId, "LT Tanaka — INDOPACOM Cyber", "INDOPACOM cyber analyst", 35.2830, 139.6715, "Active", "Analyst", "Blue Team", "{\"rank\":\"LT\",\"site\":\"Yokosuka\"}"),
                Mf("Npc", "npc-af-analyst", scenId, execId, "CPT Mwangi — AFRICOM Liaison", "AFRICOM cyber liaison officer", -1.2928, 36.8225, "Active", "Analyst", "Blue Team", "{\"rank\":\"CPT\",\"site\":\"Nairobi\"}"),
                Mf("Npc", "npc-sc-analyst", scenId, execId, "MAJ Vargas — SOUTHCOM Liaison", "SOUTHCOM cyber liaison", 4.7112, -74.0725, "Active", "Analyst", "Blue Team", "{\"rank\":\"MAJ\",\"site\":\"Bogotá\"}"),
                Mf("Npc", "npc-fv-analyst", scenId, execId, "SQNLDR Hayes — ASD Analyst", "Australian Signals Directorate analyst", -35.2805, 149.1295, "Active", "Analyst", "Blue Team", "{\"rank\":\"SQNLDR\",\"site\":\"Canberra\"}"),
                // Red — Global
                Mf("Npc", "npc-red1", scenId, execId, "Operator Alpha — Initial Access", "Red team primary operator, Moscow", 55.7530, 37.6220, "Active", "Operator", "Red Team", "{\"role\":\"Primary Operator\",\"focus\":\"Initial Access\"}"),
                Mf("Npc", "npc-red2", scenId, execId, "Operator Bravo — Persistence", "Red team persistence specialist, Rostov", 47.2380, 39.7150, "Active", "Operator", "Red Team", "{\"role\":\"Persistence\",\"focus\":\"C2 Maintenance\"}"),
                Mf("Npc", "npc-red3", scenId, execId, "Operator Charlie — Exfil", "Red team exfil specialist, Odessa", 46.4835, 30.7245, "Active", "Operator", "Red Team", "{\"role\":\"Exfiltration\",\"focus\":\"Data Staging & Exfil\"}"),
                Mf("Npc", "npc-red4", scenId, execId, "Operator Delta — Recon", "Red team recon operator, Moscow", 55.7520, 37.6150, "Active", "Operator", "Red Team", "{\"role\":\"Recon\",\"focus\":\"OSINT & Enumeration\"}"),
                Mf("Npc", "npc-red5", scenId, execId, "Operator Echo — APAC Proxy", "Red team APAC proxy operator, Shanghai", 31.2308, 121.4745, "Active", "Operator", "Red Team", "{\"role\":\"Proxy\",\"focus\":\"APAC C2 Relay\"}"),
                Mf("Npc", "npc-red6", scenId, execId, "Operator Foxtrot — LATAM", "Red team LATAM infrastructure, São Paulo", -23.5515, -46.6345, "Active", "Operator", "Red Team", "{\"role\":\"Infrastructure\",\"focus\":\"LATAM Bulletproof\"}"),
                // White cell
                Mf("Npc", "npc-white1", scenId, execId, "COL Davis — White Cell Lead", "Exercise director", 40.7950, 43.8430, "Active", "Controller", "White Cell", "{\"rank\":\"COL\",\"role\":\"Exercise Director\"}"),
                Mf("Npc", "npc-white2", scenId, execId, "LTC Monroe — White Cell OPFOR", "OPFOR adjudicator", 40.7945, 43.8440, "Active", "Controller", "White Cell", "{\"rank\":\"LTC\",\"role\":\"OPFOR Adjudicator\"}"),

                // ══════════════════════════════════════════════════
                // ── Points of Interest ──
                // ══════════════════════════════════════════════════
                Mf("Poi", "poi-entry", scenId, execId, "Initial Access Point", "Email gateway where phishing payload entered", 41.5510, 45.0100, "Alert", "InitialAccess", "Red Team"),
                Mf("Poi", "poi-pivot", scenId, execId, "Lateral Movement Pivot", "Service account used for lateral movement to DC", 41.8440, 44.7165, "Alert", "LateralMovement", "Red Team"),
                Mf("Poi", "poi-exfil", scenId, execId, "Exfil Staging", "DNS tunneling exfil channel via perimeter FW", 41.5500, 45.0140, "Alert", "Exfiltration", "Red Team"),
                Mf("Poi", "poi-privesc", scenId, execId, "Privilege Escalation Point", "Zerologon exploitation on DC01", 41.8440, 44.7165, "Alert", "PrivilegeEscalation", "Red Team"),
                Mf("Poi", "poi-persist1", scenId, execId, "Scheduled Task Backdoor", "Persistent scheduled task on DC01", 41.8440, 44.7165, "Alert", "Persistence", "Red Team"),
                Mf("Poi", "poi-persist2", scenId, execId, "Golden Ticket Forge", "Kerberos golden ticket forged from krbtgt", 41.8432, 44.7190, "Alert", "Persistence", "Red Team"),
                Mf("Poi", "poi-recon", scenId, execId, "AD Enumeration Point", "BloodHound data collection from DC01", 41.8440, 44.7165, "Alert", "Discovery", "Red Team"),
                Mf("Poi", "poi-cred-dump", scenId, execId, "Credential Dump", "LSASS memory dump on compromised workstation", 41.6920, 44.8060, "Alert", "CredentialAccess", "Red Team"),
                Mf("Poi", "poi-webshell", scenId, execId, "Web Shell Drop", "ASPX web shell uploaded to WEB01", 41.5475, 45.0155, "Alert", "Persistence", "Red Team"),
                Mf("Poi", "poi-c2-fallback", scenId, execId, "Fallback C2 — Odessa", "HTTPS beacon via Odessa bulletproof hosting", 46.4825, 30.7233, "Alert", "C2", "Red Team"),
                Mf("Poi", "poi-c2-shanghai", scenId, execId, "Proxy C2 — Shanghai", "APAC proxy C2 for INDOPACOM-targeting recon", 31.2304, 121.4737, "Alert", "C2", "Red Team"),
                Mf("Poi", "poi-c2-saopaulo", scenId, execId, "Bulletproof C2 — São Paulo", "LATAM bulletproof VPS used for scanning", -23.5505, -46.6333, "Alert", "C2", "Red Team"),
                Mf("Poi", "poi-rebound-lagos", scenId, execId, "Rebound Node — Lagos", "Traffic laundering through West Africa", 6.5244, 3.3792, "Alert", "C2", "Red Team"),
                Mf("Poi", "poi-staging-tehran", scenId, execId, "Ally Staging — Tehran", "Allied staging for secondary ops", 35.6892, 51.3890, "Alert", "Staging", "Red Team"),

                // ══════════════════════════════════════════════════
                // ── Scenario Entities ──
                // ══════════════════════════════════════════════════
                Mf("ScenarioEntity", "se-apt", scenId, execId, "Donovian APT Group", "State-aligned APT, GRU-linked", 55.7600, 37.6100, "Active", "ThreatActor", "Red Team"),
                Mf("ScenarioEntity", "se-apt-ally", scenId, execId, "Allied APT — Tehran Cell", "Cooperating nation-state cyber unit", 35.6895, 51.3885, "Active", "ThreatActor", "Red Team"),
                Mf("ScenarioEntity", "se-mailcve", scenId, execId, "CVE-2020-1472 (Zerologon)", "Critical DC vulnerability", 41.8435, 44.7180, "Alert", "Vulnerability", "", "{\"cve\":\"CVE-2020-1472\",\"severity\":\"Critical\",\"cvss\":10.0}"),
                Mf("ScenarioEntity", "se-exchange-cve", scenId, execId, "CVE-2021-26855 (ProxyLogon)", "Exchange Server RCE", 41.5510, 45.0100, "Alert", "Vulnerability", "", "{\"cve\":\"CVE-2021-26855\",\"severity\":\"Critical\",\"cvss\":9.8}"),
                Mf("ScenarioEntity", "se-printcve", scenId, execId, "CVE-2021-34527 (PrintNightmare)", "Print spooler RCE", 41.6940, 44.8042, "Alert", "Vulnerability", "", "{\"cve\":\"CVE-2021-34527\",\"severity\":\"High\",\"cvss\":8.8}"),
                Mf("ScenarioEntity", "se-campaign", scenId, execId, "Operation Quiet Anchor", "Sustained global access campaign", 44.5000, 41.5000, "Active", "Campaign", "Red Team"),
                Mf("ScenarioEntity", "se-malware1", scenId, execId, "QUIETHOOK Implant", "Custom backdoor, DLL side-loading", 55.7540, 37.6180, "Active", "Malware", "Red Team", "{\"type\":\"Backdoor\",\"delivery\":\"DLL Sideload\",\"c2\":\"HTTPS\"}"),
                Mf("ScenarioEntity", "se-malware2", scenId, execId, "ANCHORCHAIN Tunneler", "DNS-over-HTTPS exfil tool", 46.4830, 30.7235, "Active", "Malware", "Red Team", "{\"type\":\"Tunneler\",\"protocol\":\"DoH\",\"c2\":\"DNS\"}"),
                Mf("ScenarioEntity", "se-tool-bh", scenId, execId, "BloodHound/SharpHound", "AD recon tool", 41.8440, 44.7165, "Alert", "Tool", "Red Team"),
                Mf("ScenarioEntity", "se-tool-mimikatz", scenId, execId, "Mimikatz", "Credential dumping tool", 41.8440, 44.7165, "Alert", "Tool", "Red Team"),
            };

            // ══════════════════════════════════════════════════
            // ── Events / Incidents (time-sequenced for replay) ──
            // ══════════════════════════════════════════════════
            var events = new List<MapFeature>
            {
                // Phase 1 — Reconnaissance & Delivery (T+0 to T+20m)
                MfEvent("evt-01", scenId, execId, "Spear-phishing emails delivered", "Batch of 12 phishing emails targeting Atropia MoD staff", 41.5510, 45.0100, "Warning", "InitialAccess", baseTime.AddMinutes(0)),
                MfEvent("evt-02", scenId, execId, "OSINT enumeration of Atropia MoD", "Red team harvested LinkedIn/social for org chart", 55.7530, 37.6220, "Info", "Reconnaissance", baseTime.AddMinutes(2)),
                MfEvent("evt-03", scenId, execId, "DNS recon on atropia.mil", "Subdomain and mail record enumeration", 55.7530, 37.6220, "Info", "Reconnaissance", baseTime.AddMinutes(5)),
                MfEvent("evt-04", scenId, execId, "Web scan on WEB01", "Automated vuln scan against public web server", 41.5475, 45.0155, "Warning", "Reconnaissance", baseTime.AddMinutes(8)),
                MfEvent("evt-05", scenId, execId, "Phishing payload accepted", "Mail filter passes OAuth consent lure", 41.5510, 45.0100, "Warning", "InitialAccess", baseTime.AddMinutes(10)),
                MfEvent("evt-r01", scenId, execId, "EUCOM scan probe from Shanghai", "Port scan from Shanghai proxy against EUCOM NOC", 48.7770, 9.1810, "Warning", "Reconnaissance", baseTime.AddMinutes(3)),
                MfEvent("evt-r02", scenId, execId, "Brute-force attempt on AFRICOM VPN", "Credential stuffing from Lagos rebound node", -1.2915, 36.8210, "Warning", "CredentialAccess", baseTime.AddMinutes(7)),
                MfEvent("evt-r03", scenId, execId, "Phishing wave 0 — SOUTHCOM", "Exploratory phishing to SOUTHCOM liaison staff", 4.7115, -74.0730, "Warning", "InitialAccess", baseTime.AddMinutes(9)),

                // Phase 2 — Initial Access & Credential Theft (T+12 to T+30m)
                MfEvent("evt-06", scenId, execId, "OAuth token granted by SSG Park", "Victim granted OAuth permissions", 41.6920, 44.8060, "Alert", "CredentialAccess", baseTime.AddMinutes(12)),
                MfEvent("evt-07", scenId, execId, "Second OAuth token — SPC Kim", "DMZ admin clicked consent link", 41.5505, 45.0110, "Alert", "CredentialAccess", baseTime.AddMinutes(15)),
                MfEvent("evt-08", scenId, execId, "Mailbox accessed — SSG Park", "Adversary read mailbox via Graph API", 41.5510, 45.0100, "Alert", "Collection", baseTime.AddMinutes(18)),
                MfEvent("evt-09", scenId, execId, "GAL downloaded via Graph API", "Global Address List exfiltrated", 41.5510, 45.0100, "Alert", "Collection", baseTime.AddMinutes(20)),
                MfEvent("evt-10", scenId, execId, "LSASS dump on WS004", "Mimikatz credential dump", 41.6920, 44.8060, "Alert", "CredentialAccess", baseTime.AddMinutes(25)),
                MfEvent("evt-11", scenId, execId, "Pass-the-hash to MAIL01", "NTLM hash used to access mail server", 41.5510, 45.0100, "Alert", "LateralMovement", baseTime.AddMinutes(28)),
                MfEvent("evt-r04", scenId, execId, "São Paulo VPS scans Yokosuka", "Port enumeration of INDOPACOM relay from LATAM VPS", 35.2834, 139.6720, "Warning", "Reconnaissance", baseTime.AddMinutes(14)),
                MfEvent("evt-r05", scenId, execId, "Shanghai proxy probes Canberra", "ASD perimeter probed from APAC proxy", -35.2809, 149.1300, "Warning", "Reconnaissance", baseTime.AddMinutes(22)),

                // Phase 3 — Lateral Movement & Privilege Escalation (T+30 to T+65m)
                MfEvent("evt-12", scenId, execId, "Service account password reset", "Red team reset svc acct via compromised admin creds", 41.8440, 44.7165, "Alert", "Persistence", baseTime.AddMinutes(30)),
                MfEvent("evt-13", scenId, execId, "RDP session to DC01", "Lateral movement via RDP using service account", 41.8440, 44.7165, "Alert", "LateralMovement", baseTime.AddMinutes(35)),
                MfEvent("evt-14", scenId, execId, "BloodHound collection on DC01", "SharpHound ingestor gathered AD relationships", 41.8440, 44.7165, "Alert", "Discovery", baseTime.AddMinutes(38)),
                MfEvent("evt-15", scenId, execId, "Zerologon exploit on DC01", "CVE-2020-1472 against DC01 Netlogon service", 41.8440, 44.7165, "Alert", "PrivilegeEscalation", baseTime.AddMinutes(42)),
                MfEvent("evt-16", scenId, execId, "Domain Admin acquired", "DC01 machine account password reset via Zerologon", 41.8440, 44.7165, "Alert", "PrivilegeEscalation", baseTime.AddMinutes(43)),
                MfEvent("evt-17", scenId, execId, "DCSync replication attack", "krbtgt hash replicated via DCSync", 41.8440, 44.7165, "Alert", "CredentialAccess", baseTime.AddMinutes(45)),
                MfEvent("evt-18", scenId, execId, "Golden ticket forged", "Kerberos golden ticket from krbtgt hash", 55.7558, 37.6173, "Alert", "Persistence", baseTime.AddMinutes(47)),
                MfEvent("evt-19", scenId, execId, "Lateral to DC02 via golden ticket", "Secondary DC accessed using forged TGT", 41.8415, 44.7210, "Alert", "LateralMovement", baseTime.AddMinutes(50)),
                MfEvent("evt-20", scenId, execId, "RDP to FILE01", "Classified file shares browsed via golden ticket", 41.8445, 44.7175, "Alert", "LateralMovement", baseTime.AddMinutes(52)),
                MfEvent("evt-21", scenId, execId, "Scheduled task on DC01", "Backdoor scheduled task created", 41.8440, 44.7165, "Alert", "Persistence", baseTime.AddMinutes(55)),
                MfEvent("evt-22", scenId, execId, "Web shell on WEB01", "ASPX web shell planted on public web server", 41.5475, 45.0155, "Alert", "Persistence", baseTime.AddMinutes(58)),
                MfEvent("evt-23", scenId, execId, "PrintNightmare on PRINT01", "CVE-2021-34527 exploited for code exec", 41.6940, 44.8042, "Alert", "PrivilegeEscalation", baseTime.AddMinutes(60)),
                MfEvent("evt-24", scenId, execId, "Lateral to SQL01", "SQL server accessed with domain admin", 41.8425, 44.7200, "Alert", "LateralMovement", baseTime.AddMinutes(62)),
                MfEvent("evt-r06", scenId, execId, "Tehran node probes Tbilisi VPN", "Allied APT tests VPN gateway", 41.5520, 45.0080, "Warning", "Reconnaissance", baseTime.AddMinutes(40)),
                MfEvent("evt-r07", scenId, execId, "Lagos rebound scans EUCOM", "Rebound node probes Stuttgart perimeter", 48.7770, 9.1810, "Warning", "Reconnaissance", baseTime.AddMinutes(55)),

                // Phase 4 — Collection & Exfiltration (T+65 to T+85m)
                MfEvent("evt-25", scenId, execId, "SQL database dump", "Personnel database dumped from SQL01", 41.8425, 44.7200, "Alert", "Collection", baseTime.AddMinutes(65)),
                MfEvent("evt-26", scenId, execId, "File server data staged", "Classified docs staged on FILE01", 41.8445, 44.7175, "Alert", "Collection", baseTime.AddMinutes(68)),
                MfEvent("evt-27", scenId, execId, "Data compressed and encrypted", "Staged data RAR-encrypted before exfil", 41.8445, 44.7175, "Alert", "Exfiltration", baseTime.AddMinutes(70)),
                MfEvent("evt-28", scenId, execId, "DNS tunneling exfil begins", "ANCHORCHAIN begins DoH exfil through FW01", 41.5500, 45.0140, "Alert", "Exfiltration", baseTime.AddMinutes(72)),
                MfEvent("evt-29", scenId, execId, "Data received at Odessa drop", "Exfiltrated data arrives at bulletproof hosting", 46.4830, 30.7240, "Alert", "Exfiltration", baseTime.AddMinutes(75)),
                MfEvent("evt-30", scenId, execId, "Second exfil via web shell", "Backup exfil via WEB01 to Rostov relay", 41.5475, 45.0155, "Alert", "Exfiltration", baseTime.AddMinutes(78)),
                MfEvent("evt-31", scenId, execId, "SATCOM intercept attempt", "Red team probed SATCOM for credential reuse", 41.9205, 45.4740, "Warning", "LateralMovement", baseTime.AddMinutes(80)),
                MfEvent("evt-r08", scenId, execId, "Data mirrored to Shanghai proxy", "Copy of exfil data forwarded to APAC proxy", 31.2310, 121.4740, "Alert", "Exfiltration", baseTime.AddMinutes(77)),
                MfEvent("evt-r09", scenId, execId, "Data mirrored to São Paulo VPS", "Backup exfil copy sent to LATAM VPS", -23.5510, -46.6340, "Alert", "Exfiltration", baseTime.AddMinutes(79)),
                MfEvent("evt-r10", scenId, execId, "Tehran staging receives GAL copy", "Global Address List shared with allied APT", 35.6900, 51.3895, "Alert", "Exfiltration", baseTime.AddMinutes(82)),

                // Phase 5 — Detection & Response (T+50 to T+95m)
                MfEvent("evt-32", scenId, execId, "SIEM correlation alert", "Unusual RDP + password reset combo flagged", 41.6960, 44.7985, "Info", "Detection", baseTime.AddMinutes(50)),
                MfEvent("evt-33", scenId, execId, "EDR alert: Mimikatz", "Credential dumping tool detected on WS004", 41.6952, 44.8000, "Warning", "Detection", baseTime.AddMinutes(52)),
                MfEvent("evt-34", scenId, execId, "SOC analyst begins investigation", "SGT Chen opens investigation ticket", 41.6938, 44.8025, "Info", "Response", baseTime.AddMinutes(55)),
                MfEvent("evt-35", scenId, execId, "Threat hunt: unusual Graph API", "SPC Rivera spots anomalous Graph traffic", 41.6930, 44.8042, "Info", "Detection", baseTime.AddMinutes(58)),
                MfEvent("evt-36", scenId, execId, "SIEM: DCSync detected", "Replication from non-DC source flagged", 41.6960, 44.7985, "Alert", "Detection", baseTime.AddMinutes(62)),
                MfEvent("evt-37", scenId, execId, "Intel analyst matches IOCs", "Relay IP matched to Donovian infrastructure", 41.6935, 44.8010, "Info", "Detection", baseTime.AddMinutes(65)),
                MfEvent("evt-38", scenId, execId, "CERT-Atropia notified", "CERT alerted to potential state-sponsored intrusion", 41.7152, 44.8271, "Info", "Response", baseTime.AddMinutes(68)),
                MfEvent("evt-39", scenId, execId, "CERT sandbox detonation", "Malicious OAuth app confirmed weaponized", 41.7145, 44.8265, "Info", "Detection", baseTime.AddMinutes(72)),
                MfEvent("evt-40", scenId, execId, "Incident escalated to IC", "MAJ Torres assumes incident command", 41.6945, 44.7998, "Info", "Response", baseTime.AddMinutes(75)),
                MfEvent("evt-41", scenId, execId, "FOB Hammer on alert", "Signals company warned of compromise", 41.9816, 44.1135, "Info", "Response", baseTime.AddMinutes(78)),
                MfEvent("evt-r11", scenId, execId, "EUCOM NOC detects scan", "CPT Weber identifies Shanghai-origin scan", 48.7758, 9.1829, "Info", "Detection", baseTime.AddMinutes(56)),
                MfEvent("evt-r12", scenId, execId, "CYBERCOM opens parallel investigation", "MAJ Jackson correlates with global IOC feed", 39.1086, -76.7295, "Info", "Detection", baseTime.AddMinutes(60)),
                MfEvent("evt-r13", scenId, execId, "CYBERCOM threat intel shared", "IOCs pushed to all COCOM partners via TIP", 39.1095, -76.7275, "Info", "Response", baseTime.AddMinutes(67)),
                MfEvent("evt-r14", scenId, execId, "INDOPACOM confirms Shanghai probe", "LT Tanaka confirms same actor probed Yokosuka", 35.2834, 139.6720, "Info", "Detection", baseTime.AddMinutes(70)),
                MfEvent("evt-r15", scenId, execId, "ASD Canberra flags C2 traffic", "Five Eyes partner identifies C2 beacon pattern", -35.2809, 149.1300, "Info", "Detection", baseTime.AddMinutes(73)),
                MfEvent("evt-r16", scenId, execId, "AFRICOM blocks Lagos rebound", "Firewall rule blocks rebound node at Nairobi VPN", -1.2915, 36.8210, "Info", "Containment", baseTime.AddMinutes(80)),
                MfEvent("evt-r17", scenId, execId, "SOUTHCOM blocks phishing domain", "Phishing domains blocked at SOUTHCOM perimeter", 4.7105, -74.0710, "Info", "Containment", baseTime.AddMinutes(82)),

                // Phase 5b — Containment (T+82 to T+95m)
                MfEvent("evt-42", scenId, execId, "Containment: WS004 isolated", "Compromised workstation pulled from network", 41.6920, 44.8060, "Info", "Containment", baseTime.AddMinutes(82)),
                MfEvent("evt-43", scenId, execId, "Admin account disabled", "Compromised service account disabled on all DCs", 41.8440, 44.7165, "Info", "Containment", baseTime.AddMinutes(85)),
                MfEvent("evt-44", scenId, execId, "DMZ segment isolated", "Blue team isolates DMZ at Rustavi", 41.5500, 45.0140, "Info", "Containment", baseTime.AddMinutes(88)),
                MfEvent("evt-45", scenId, execId, "OAuth app revoked", "Malicious Azure app registrations revoked", 41.6945, 44.7998, "Info", "Containment", baseTime.AddMinutes(90)),

                // Phase 6 — Adversary Adaptation (T+95 to T+120m)
                MfEvent("evt-46", scenId, execId, "Backup C2 activated", "Adversary switches to HTTPS beacon via Odessa", 46.4825, 30.7233, "Alert", "C2", baseTime.AddMinutes(95)),
                MfEvent("evt-47", scenId, execId, "Scheduled task fires on DC01", "Persistence re-establishes C2 session", 41.8440, 44.7165, "Alert", "Persistence", baseTime.AddMinutes(98)),
                MfEvent("evt-48", scenId, execId, "Web shell activated on WEB01", "Adversary pivots to web shell", 41.5475, 45.0155, "Alert", "Persistence", baseTime.AddMinutes(100)),
                MfEvent("evt-49", scenId, execId, "Phishing wave 2 — FOB & SATCOM", "Second batch targeting FOB/SATCOM staff", 41.9816, 44.1135, "Warning", "InitialAccess", baseTime.AddMinutes(105)),
                MfEvent("evt-50", scenId, execId, "SATCOM encryptor probed", "Attempt to pivot to classified SATCOM", 41.9190, 45.4730, "Alert", "LateralMovement", baseTime.AddMinutes(110)),
                MfEvent("evt-51", scenId, execId, "SQL exfil via backup C2", "Personnel DB exfil resumes through Odessa", 41.8425, 44.7200, "Alert", "Exfiltration", baseTime.AddMinutes(112)),
                MfEvent("evt-52", scenId, execId, "DR site probed", "Recon against Kutaisi DR via golden ticket", 42.2679, 42.6946, "Warning", "Discovery", baseTime.AddMinutes(115)),
                MfEvent("evt-r18", scenId, execId, "Shanghai proxy activates backup C2", "APAC C2 channel activated as fallback", 31.2310, 121.4740, "Alert", "C2", baseTime.AddMinutes(97)),
                MfEvent("evt-r19", scenId, execId, "Tehran node launches phishing wave 3", "Allied APT targets EUCOM with phishing", 48.7758, 9.1829, "Warning", "InitialAccess", baseTime.AddMinutes(108)),
                MfEvent("evt-r20", scenId, execId, "São Paulo VPS probes Fort Meade", "LATAM VPS scans CYBERCOM perimeter", 39.1086, -76.7295, "Warning", "Reconnaissance", baseTime.AddMinutes(112)),

                // Phase 7 — Eradication & Recovery (T+118 to T+180m)
                MfEvent("evt-53", scenId, execId, "Forensic image: WS004", "Disk image of compromised workstation", 41.6920, 44.8060, "Info", "Investigation", baseTime.AddMinutes(118)),
                MfEvent("evt-54", scenId, execId, "Forensic image: DC01", "Memory dump and disk image of primary DC", 41.8440, 44.7165, "Info", "Investigation", baseTime.AddMinutes(120)),
                MfEvent("evt-55", scenId, execId, "Scheduled task removed", "Persistence mechanism removed from DC01", 41.8440, 44.7165, "Info", "Eradication", baseTime.AddMinutes(125)),
                MfEvent("evt-56", scenId, execId, "Web shell removed", "ASPX shell removed from WEB01", 41.5475, 45.0155, "Info", "Eradication", baseTime.AddMinutes(128)),
                MfEvent("evt-57", scenId, execId, "krbtgt rotated (1st)", "First krbtgt rotation to invalidate golden tickets", 41.8440, 44.7165, "Info", "Eradication", baseTime.AddMinutes(130)),
                MfEvent("evt-58", scenId, execId, "C2 beacon blocked at FW01", "All known C2 domains/IPs blocked", 41.5500, 45.0140, "Info", "Eradication", baseTime.AddMinutes(132)),
                MfEvent("evt-59", scenId, execId, "DNS tunnel blocked at proxy", "DoH exfil channel blocked", 41.5488, 45.0132, "Info", "Eradication", baseTime.AddMinutes(135)),
                MfEvent("evt-60", scenId, execId, "All OAuth tokens revoked", "Tenant-wide token revocation", 41.6945, 44.7998, "Info", "Eradication", baseTime.AddMinutes(138)),
                MfEvent("evt-61", scenId, execId, "DC01 rebuilt", "Domain controller rebuilt from clean image", 41.8440, 44.7165, "Info", "Recovery", baseTime.AddMinutes(145)),
                MfEvent("evt-62", scenId, execId, "MAIL01 patched & restored", "Exchange patched for ProxyLogon", 41.5510, 45.0100, "Info", "Recovery", baseTime.AddMinutes(150)),
                MfEvent("evt-63", scenId, execId, "WEB01 rebuilt", "Web server rebuilt, hashes blocklisted", 41.5475, 45.0155, "Info", "Recovery", baseTime.AddMinutes(152)),
                MfEvent("evt-64", scenId, execId, "krbtgt rotated (2nd)", "All golden tickets now invalid", 41.8440, 44.7165, "Info", "Eradication", baseTime.AddMinutes(155)),
                MfEvent("evt-65", scenId, execId, "DMZ restored", "DMZ reconnected after FW rule updates", 41.5500, 45.0140, "Info", "Recovery", baseTime.AddMinutes(158)),
                MfEvent("evt-66", scenId, execId, "Enhanced monitoring deployed", "New sensors at DMZ and DC boundaries", 41.5498, 45.0120, "Info", "Recovery", baseTime.AddMinutes(160)),
                MfEvent("evt-67", scenId, execId, "FOB Hammer all-clear", "FOB confirmed clean", 41.9816, 44.1135, "Info", "Recovery", baseTime.AddMinutes(165)),
                MfEvent("evt-68", scenId, execId, "SATCOM verified clean", "SATCOM confirmed uncompromised", 41.9198, 45.4735, "Info", "Recovery", baseTime.AddMinutes(168)),
                MfEvent("evt-r21", scenId, execId, "EUCOM blocks Tehran phishing", "Stuttgart blocks phishing wave 3", 48.7770, 9.1810, "Info", "Containment", baseTime.AddMinutes(140)),
                MfEvent("evt-r22", scenId, execId, "CYBERCOM blocks São Paulo scans", "Fort Meade blocks LATAM VPS probes", 39.1090, -76.7280, "Info", "Containment", baseTime.AddMinutes(142)),
                MfEvent("evt-r23", scenId, execId, "INDOPACOM blocks Shanghai C2", "Yokosuka firewall blocks APAC C2", 35.2840, 139.6730, "Info", "Containment", baseTime.AddMinutes(145)),
                MfEvent("evt-r24", scenId, execId, "ASD shares malware signatures", "Five Eyes partner distributes QUIETHOOK IOCs", -35.2815, 149.1310, "Info", "Response", baseTime.AddMinutes(148)),
                MfEvent("evt-r25", scenId, execId, "Global IOC blocklist deployed", "All COCOMs update perimeter rules simultaneously", 39.1095, -76.7275, "Info", "Eradication", baseTime.AddMinutes(155)),
                MfEvent("evt-69", scenId, execId, "White Cell: ENDEX", "COL Davis calls end of exercise", 40.7950, 43.8430, "Info", "Response", baseTime.AddMinutes(175)),
                MfEvent("evt-70", scenId, execId, "After-action review scheduled", "AAR scheduled for all participants worldwide", 41.6934, 44.8015, "Info", "Response", baseTime.AddMinutes(180)),
            };

            features.AddRange(events);

            // ══════════════════════════════════════════════════
            // ── Connections / Network Links ──
            // ══════════════════════════════════════════════════
            var connections = new List<MapFeature>
            {
                // --- Blue infrastructure backbone (Georgia) ---
                MfConn("conn-hq-dmz", scenId, execId, "Tbilisi HQ ↔ Rustavi DMZ", "Primary backbone, fiber", "site-hq", "site-dmz", "Online", "Backbone"),
                MfConn("conn-hq-dc1", scenId, execId, "Tbilisi HQ ↔ Mtskheta DC", "Server LAN, dedicated line", "site-hq", "site-dc1", "Online", "LAN"),
                MfConn("conn-dc1-dc2", scenId, execId, "Mtskheta DC ↔ Kutaisi DR", "Cross-country replication", "site-dc1", "site-dc2", "Online", "Replication"),
                MfConn("conn-hq-remote", scenId, execId, "Tbilisi HQ ↔ Gyumri", "VPN tunnel to liaison office", "site-hq", "site-remote", "Online", "VPN"),
                MfConn("conn-hq-fob", scenId, execId, "Tbilisi HQ ↔ FOB Hammer", "Encrypted MPLS to FOB", "site-hq", "site-fob", "Online", "VPN"),
                MfConn("conn-hq-satcom", scenId, execId, "Tbilisi HQ ↔ SATCOM Telavi", "SATCOM backhaul", "site-hq", "site-satcom", "Online", "Backbone"),
                MfConn("conn-hq-cert", scenId, execId, "Tbilisi HQ ↔ CERT-Atropia", "Secure coordination channel", "site-hq", "site-civsoc", "Online", "LAN"),

                // --- Blue global WAN links ---
                MfConn("conn-hq-eucom", scenId, execId, "Tbilisi HQ ↔ Stuttgart EUCOM", "Transatlantic MPLS backhaul", "site-hq", "site-eucom", "Online", "WAN"),
                MfConn("conn-eucom-cybercom", scenId, execId, "Stuttgart ↔ Fort Meade", "EUCOM-CYBERCOM dedicated link", "site-eucom", "site-cybercom", "Online", "Backbone"),
                MfConn("conn-cybercom-indopacom", scenId, execId, "Fort Meade ↔ Yokosuka", "Transpacific CYBERCOM relay", "site-cybercom", "site-indopacom", "Online", "WAN"),
                MfConn("conn-cybercom-africom", scenId, execId, "Fort Meade ↔ Nairobi", "CYBERCOM-AFRICOM VPN", "site-cybercom", "site-africom", "Online", "VPN"),
                MfConn("conn-cybercom-southcom", scenId, execId, "Fort Meade ↔ Bogotá", "CYBERCOM-SOUTHCOM VPN", "site-cybercom", "site-southcom", "Online", "VPN"),
                MfConn("conn-indopacom-fvey", scenId, execId, "Yokosuka ↔ Canberra", "Five Eyes intel sharing link", "site-indopacom", "site-fvey", "Online", "VPN"),
                MfConn("conn-eucom-fvey", scenId, execId, "Stuttgart ↔ Canberra", "Five Eyes coordination", "site-eucom", "site-fvey", "Online", "VPN"),

                // --- Red adversary C2 infrastructure ---
                MfConn("conn-c2-relay", scenId, execId, "Moscow C2 ↔ Rostov Relay", "Primary C2 link", "m-red-c2", "m-red-relay", "Active", "C2"),
                MfConn("conn-c2-c2b", scenId, execId, "Moscow C2 ↔ Backup C2", "C2 redundancy", "m-red-c2", "m-red-c2b", "Active", "C2"),
                MfConn("conn-relay-odessa", scenId, execId, "Rostov ↔ Odessa BP", "Relay to bulletproof hosting", "m-red-relay", "m-red-exfil", "Active", "C2"),
                MfConn("conn-c2-odessa", scenId, execId, "Moscow C2 ↔ Odessa DNS", "Backup DNS tunnel path", "m-red-c2", "m-red-dns", "Active", "C2"),
                MfConn("conn-c2-shanghai", scenId, execId, "Moscow C2 ↔ Shanghai Proxy", "APAC proxy C2 relay", "m-red-c2", "m-red-shanghai", "Active", "C2"),
                MfConn("conn-c2-saopaulo", scenId, execId, "Moscow C2 ↔ São Paulo VPS", "LATAM bulletproof staging", "m-red-c2", "m-red-saopaulo", "Active", "C2"),
                MfConn("conn-c2-lagos", scenId, execId, "Moscow C2 ↔ Lagos Rebound", "Rebound traffic laundering", "m-red-c2", "m-red-lagos", "Active", "C2"),
                MfConn("conn-c2-tehran", scenId, execId, "Moscow C2 ↔ Tehran Staging", "Allied nation-state coordination", "m-red-c2", "m-red-tehran", "Active", "C2"),
                MfConn("conn-odessa-c2b", scenId, execId, "Odessa ↔ Backup C2", "Fallback HTTPS C2 channel", "m-red-exfil", "m-red-c2b", "Active", "C2"),

                // --- Attack paths (primary intrusion) ---
                MfConn("conn-relay-mail", scenId, execId, "Rostov → Mail Gateway", "Phishing delivery to DMZ", "m-red-relay", "m-mail01", "Alert", "AttackPath"),
                MfConn("conn-phish-mail", scenId, execId, "Phishing Srv → Mail GW", "Phishing delivery infra", "m-red-phish", "m-mail01", "Alert", "AttackPath"),
                MfConn("conn-mail-ws004", scenId, execId, "Mail GW → Admin WS", "Credential theft, DMZ to HQ", "m-mail01", "m-ws004", "Alert", "AttackPath"),
                MfConn("conn-ws004-dc01", scenId, execId, "Admin WS → DC01", "Lateral movement to DC", "m-ws004", "m-dc01", "Alert", "AttackPath"),
                MfConn("conn-dc01-dc02", scenId, execId, "DC01 → DC02", "Golden ticket to secondary DC", "m-dc01", "m-dc02", "Alert", "AttackPath"),
                MfConn("conn-dc01-file", scenId, execId, "DC01 → FILE01", "Golden ticket to file server", "m-dc01", "m-file01", "Alert", "AttackPath"),
                MfConn("conn-dc01-sql", scenId, execId, "DC01 → SQL01", "Golden ticket to SQL", "m-dc01", "m-sql01", "Alert", "AttackPath"),
                MfConn("conn-ws004-print", scenId, execId, "Admin WS → PRINT01", "PrintNightmare exploitation", "m-ws004", "m-print01", "Alert", "AttackPath"),
                MfConn("conn-relay-web", scenId, execId, "Rostov → WEB01", "Web shell C2 path", "m-red-relay", "m-web01", "Alert", "AttackPath"),
                MfConn("conn-exfil-fw", scenId, execId, "FW01 → Odessa Drop", "DNS tunnel exfil path", "m-fw01", "m-red-exfil", "Alert", "AttackPath"),

                // --- Attack paths (global probing) ---
                MfConn("conn-shanghai-eucom", scenId, execId, "Shanghai → Stuttgart", "APAC proxy probes EUCOM", "m-red-shanghai", "m-eu-fw", "Alert", "AttackPath"),
                MfConn("conn-lagos-africom", scenId, execId, "Lagos → Nairobi VPN", "Rebound targets AFRICOM", "m-red-lagos", "m-af-vpn", "Alert", "AttackPath"),
                MfConn("conn-saopaulo-indopacom", scenId, execId, "São Paulo → Yokosuka", "LATAM VPS probes INDOPACOM", "m-red-saopaulo", "m-ip-siem", "Alert", "AttackPath"),
                MfConn("conn-saopaulo-cybercom", scenId, execId, "São Paulo → Fort Meade", "LATAM VPS probes CYBERCOM", "m-red-saopaulo", "m-cc-siem", "Alert", "AttackPath"),
                MfConn("conn-shanghai-canberra", scenId, execId, "Shanghai → Canberra", "APAC proxy probes Five Eyes", "m-red-shanghai", "m-fv-siem", "Alert", "AttackPath"),
                MfConn("conn-tehran-eucom", scenId, execId, "Tehran → Stuttgart", "Allied APT phishing EUCOM", "m-red-tehran", "m-eu-fw", "Alert", "AttackPath"),
                MfConn("conn-saopaulo-southcom", scenId, execId, "São Paulo → Bogotá", "LATAM VPS probes SOUTHCOM", "m-red-saopaulo", "m-sc-vpn", "Alert", "AttackPath"),
            };

            features.AddRange(connections);

            context.MapFeatures.AddRange(features);
            await context.SaveChangesAsync();

            // Also add an execution event to show in the details
            var execEvent = new ExecutionEvent
            {
                ExecutionId = execId,
                Timestamp = baseTime,
                EventType = "StatusChange",
                Description = "Execution started with map demo data",
                Data = "{\"mapFeatures\": " + features.Count + "}",
                Severity = "Info"
            };
            context.ExecutionEvents.Add(execEvent);
            await context.SaveChangesAsync();

            logger.LogInformation("Seeded {Count} map features for execution {ExecId}", features.Count, execId);
        }

        private static MapFeature Mf(string featureType, string entityId, int scenId, int execId,
            string label, string description, double lat, double lng,
            string status, string category, string team, string props = "{}")
        {
            return new MapFeature
            {
                FeatureType = featureType,
                EntityId = entityId,
                ScenarioId = scenId,
                ExecutionId = execId,
                Label = label,
                Description = description,
                Latitude = lat,
                Longitude = lng,
                Status = status,
                Category = category,
                Team = team,
                Properties = props,
                CreatedAt = DateTime.UtcNow
            };
        }

        private static MapFeature MfEvent(string entityId, int scenId, int execId,
            string label, string description, double lat, double lng,
            string status, string category, DateTime timestamp)
        {
            return new MapFeature
            {
                FeatureType = "Event",
                EntityId = entityId,
                ScenarioId = scenId,
                ExecutionId = execId,
                Label = label,
                Description = description,
                Latitude = lat,
                Longitude = lng,
                Status = status,
                Category = category,
                Team = "",
                Properties = "{}",
                CreatedAt = DateTime.UtcNow,
                ValidFrom = timestamp,
                ValidTo = timestamp.AddMinutes(5)
            };
        }

        private static MapFeature MfConn(string entityId, int scenId, int execId,
            string label, string description, string sourceId, string targetId,
            string status, string category)
        {
            return new MapFeature
            {
                FeatureType = "Connection",
                EntityId = entityId,
                ScenarioId = scenId,
                ExecutionId = execId,
                Label = label,
                Description = description,
                Latitude = 0,
                Longitude = 0,
                Status = status,
                Category = category,
                Team = "",
                Properties = "{}",
                SourceFeatureId = sourceId,
                TargetFeatureId = targetId,
                CreatedAt = DateTime.UtcNow
            };
        }
    }
}

