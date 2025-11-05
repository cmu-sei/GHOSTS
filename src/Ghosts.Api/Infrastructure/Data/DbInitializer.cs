// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ghosts.Api.Infrastructure.Data
{
    public class DbInitializer
    {
        public static async Task Initialize(ApplicationDbContext context, ILogger<DbInitializer> logger)
        {
            await context.Database.EnsureCreatedAsync();

            // Check if database is already seeded
            if (await context.Scenarios.AnyAsync())
            {
                logger.LogInformation("Database already seeded");
                return;
            }

            logger.LogInformation("Seeding database with sample scenarios...");

            await SeedScenarios(context, logger);

            await context.SaveChangesAsync();
            logger.LogInformation("Database seeding completed");
        }

        private static async Task SeedScenarios(ApplicationDbContext context, ILogger<DbInitializer> logger)
        {
            var scenarios = new[]
            {
                // Classic cyber (2)
                CreateAptIntrusionScenario(),
                CreateRansomwareScenario(),

                // Influence / IO (DATEWORLD, 2)
                CreateInfluenceAmberVeil(),     // Limaria → Gorgas/Atropia narrative ops
                CreateInfluenceIronOrchard(),   // Limarian agrarian unrest leveraged by proxies

                // Next-gen / hybrid (3)
                CreateNextGenAutonomyConvoy(),  // Autonomy misrouting + EMS spoof + human-in-the-loop
                CreateNextGenCognitiveTwin(),   // Digital-twin corruption + model poisoning
                CreateNextGenSpacePntSpoofing() // Space/PNT spoofing with civil-military spillover
            };

            await context.Scenarios.AddRangeAsync(scenarios);
            logger.LogInformation("Added {Count} sample scenarios", scenarios.Length);
        }

        // ===== Classic Cyber =====

        private static Scenario CreateAptIntrusionScenario()
        {
            return new Scenario
            {
                Name = "APT Intrusion — Quiet Anchor",
                Description = "Suspected state actor establishes foothold via email infrastructure, blends into admin traffic, and hunts long-term access. Blue team must detect, contain, and evict without disrupting mission ops.",
                CreatedAt = DateTime.UtcNow.AddDays(-28),
                UpdatedAt = DateTime.UtcNow.AddDays(-28),
                ScenarioParameters = new ScenarioParameters
                {
                    Objectives = "Detect initial access; map persistence; contain lateral movement; preserve forensics; eradicate C2; restore services with minimal downtime.",
                    PoliticalContext = "Heightened tensions; premature attribution risks diplomatic fallout.",
                    RulesOfEngagement = "Blue: defensive only. Red: maintain persistence and stage exfil. White: injects and adjudication.",
                    VictoryConditions = "All persistence removed within 48h; no confirmed exfiltration; core services uptime ≥ 95%.",
                    Nations = new[]
                    {
                        new Nation { Name = "Atropia", Alignment = "friendly" },
                        new Nation { Name = "Donovia", Alignment = "adversary" }
                    },
                    ThreatActors = new[]
                    {
                        new ThreatActor
                        {
                            Name = "State-aligned APT (Donovian)",
                            Type = "state",
                            Capability = 9,
                            Ttps = "T1566.001,T1078,T1003,T1021.001,T1059.001,T1071.001,T1041"
                        }
                    },
                    Injects = new[]
                    {
                        new Inject { Trigger = "T+30m", Title = "Suspicious OAuth grant on mail tenant" },
                        new Inject { Trigger = "T+2h", Title = "Service account password reset alert" },
                        new Inject { Trigger = "T+4h", Title = "Odd PowerShell transcript artifact" },
                        new Inject { Trigger = "OnDetect", Title = "Attempted log tampering on DC" }
                    },
                    UserPools = new[]
                    {
                        new UserPool { Role = "SOC Analyst", Count = 4 },
                        new UserPool { Role = "Incident Commander", Count = 1 },
                        new UserPool { Role = "Red Team Operator", Count = 2 },
                        new UserPool { Role = "White Cell Controller", Count = 2 }
                    }
                },
                TechnicalEnvironment = new TechnicalEnvironment
                {
                    NetworkTopology = "Enterprise network with DMZ, segmented user/server/admin nets, SaaS email.",
                    Services = "AD, Exchange/365, Web apps, DBs, File shares, VPN",
                    Assets = "180 workstations, 40 servers, 4 DCs",
                    Defenses = "[\"SIEM\",\"EDR\",\"Firewall\",\"IDS/IPS\",\"Email gateway\",\"Proxy\"]",
                    Vulnerabilities = new[]
                    {
                        new Vulnerability { Asset = "Mail Tenant", Cve = "Legacy-Auth-Enabled", Severity = "High" },
                        new Vulnerability { Asset = "DC", Cve = "CVE-2020-1472", Severity = "Critical" },
                        new Vulnerability { Asset = "VPN", Cve = "Weak-MFA-Policy", Severity = "High" }
                    }
                },
                GameMechanics = new GameMechanics
                {
                    TimelineType = "compressed",
                    DurationHours = 8,
                    AdjudicationType = "hybrid",
                    EscalationLadder = "Detection → Containment → Investigation → Eradication → Recovery",
                    BranchingLogic = "If no detection by T+4h, attacker deploys backup C2 and credentials; if detected, attacker swaps to living-off-the-land.",
                    CollectLogs = true,
                    CollectNetwork = true,
                    CollectEndpoint = true,
                    CollectChat = true,
                    PerformanceMetrics = "MTTD, MTTC, hosts compromised, privilege level removed, false positives"
                },
                ScenarioTimeline = new ScenarioTimeline
                {
                    ExerciseDuration = 480,
                    ScenarioTimelineEvents = new[]
                    {
                        new ScenarioTimelineEvent { Time = "T+0m", Number = 1, Assigned = "Red Team", Description = "Initial phish; OAuth consent link", Status = "Pending" },
                        new ScenarioTimelineEvent { Time = "T+30m", Number = 2, Assigned = "White Cell", Description = "User reports odd MFA prompt", Status = "Pending" },
                        new ScenarioTimelineEvent { Time = "T+2h", Number = 3, Assigned = "Red Team", Description = "Service account pivot", Status = "Pending" },
                        new ScenarioTimelineEvent { Time = "T+4h", Number = 4, Assigned = "Blue Team", Description = "Detection window—SIEM correlation", Status = "Pending" },
                        new ScenarioTimelineEvent { Time = "T+6h", Number = 5, Assigned = "White Cell", Description = "Adjudication: containment effectiveness", Status = "Pending" },
                        new ScenarioTimelineEvent { Time = "T+8h", Number = 6, Assigned = "White Cell", Description = "Hot wash", Status = "Pending" }
                    }
                }
            };
        }

        private static Scenario CreateRansomwareScenario()
        {
            return new Scenario
            {
                Name = "Ransomware — Grey Ledger",
                Description = "Rapidly spreading encryption event stresses decision-making under regulatory pressure and incomplete backups.",
                CreatedAt = DateTime.UtcNow.AddDays(-20),
                UpdatedAt = DateTime.UtcNow.AddDays(-20),
                ScenarioParameters = new ScenarioParameters
                {
                    Objectives = "Contain spread; identify patient zero; recover Tier-0/1 systems; manage exec/media comms.",
                    PoliticalContext = "Publicly traded; disclosure clock running.",
                    RulesOfEngagement = "Blue: full IR. Red: maximize blast radius. Green: exec/media/board.",
                    VictoryConditions = "Contain ≤ 2h; restore critical services ≤ 24h; no ransom paid.",
                    Nations = new[] { new Nation { Name = "Atropia", Alignment = "friendly" } },
                    ThreatActors = new[]
                    {
                        new ThreatActor
                        {
                            Name = "REvil-style crew",
                            Type = "criminal",
                            Capability = 7,
                            Ttps = "T1486,T1490,T1489,T1047,T1059.001,T1021.002,T1070.004"
                        }
                    },
                    Injects = new[]
                    {
                        new Inject { Trigger = "T+0m", Title = "Helpdesk flooded: encrypted files" },
                        new Inject { Trigger = "T+1h", Title = "DB server encryption" },
                        new Inject { Trigger = "T+2h", Title = "Media inquiry" },
                        new Inject { Trigger = "T+4h", Title = "Ransom doubles" }
                    },
                    UserPools = new[]
                    {
                        new UserPool { Role = "IR Lead", Count = 1 },
                        new UserPool { Role = "Analyst", Count = 3 },
                        new UserPool { Role = "Red Operator", Count = 1 },
                        new UserPool { Role = "Green Exec/Media", Count = 2 },
                        new UserPool { Role = "White Cell", Count = 2 }
                    }
                },
                TechnicalEnvironment = new TechnicalEnvironment
                {
                    NetworkTopology = "Flat with remote VPN; cloud backups.",
                    Services = "Files, Email, ERP, DB, Backup",
                    Assets = "100 workstations, 20 servers",
                    Defenses = "[\"Firewall\",\"AV\",\"Cloud backup\",\"Email filtering\"]",
                    Vulnerabilities = new[]
                    {
                        new Vulnerability { Asset = "RDP", Cve = "CVE-2019-0708", Severity = "Critical" },
                        new Vulnerability { Asset = "Backup", Cve = "Misconfigured-Access", Severity = "High" }
                    }
                },
                GameMechanics = new GameMechanics
                {
                    TimelineType = "real-time",
                    DurationHours = 6,
                    AdjudicationType = "manual",
                    EscalationLadder = "Detection → Containment → Exec brief → Recovery → Restoration",
                    BranchingLogic = "If containment fails, backups get hit; if payment considered, legal/ethics injects fire.",
                    CollectLogs = true,
                    CollectNetwork = false,
                    CollectEndpoint = true,
                    CollectChat = true,
                    PerformanceMetrics = "Time to containment, % encrypted, recovery time, comms quality"
                },
                ScenarioTimeline = new ScenarioTimeline
                {
                    ExerciseDuration = 360,
                    ScenarioTimelineEvents = new[]
                    {
                        new ScenarioTimelineEvent { Time = "T+0m", Number = 1, Assigned = "Red Team", Description = "Encryption starts", Status = "Pending" },
                        new ScenarioTimelineEvent { Time = "T+30m", Number = 2, Assigned = "White Cell", Description = "Helpdesk overload inject", Status = "Pending" },
                        new ScenarioTimelineEvent { Time = "T+1h", Number = 3, Assigned = "Green Cell", Description = "CEO demands brief", Status = "Pending" },
                        new ScenarioTimelineEvent { Time = "T+2h", Number = 4, Assigned = "White Cell", Description = "Media inquiry", Status = "Pending" },
                        new ScenarioTimelineEvent { Time = "T+6h", Number = 5, Assigned = "White Cell", Description = "Debrief", Status = "Pending" }
                    }
                }
            };
        }

        // ===== Influence / IO (DATEWORLD) =====

        private static Scenario CreateInfluenceAmberVeil()
        {
            return new Scenario
            {
                Name = "Amber Veil — Coalition Friction",
                Description = "Limarian operators seed distrust in Gorgas border towns, reframing Atropian aid as covert annexation. Synthetic veterans groups and deepfake audio drive protests that disrupt joint logistics.",
                CreatedAt = DateTime.UtcNow.AddDays(-12),
                UpdatedAt = DateTime.UtcNow.AddDays(-12),
                ScenarioParameters = new ScenarioParameters
                {
                    Objectives = "Detect/attribute influence network; protect convoy legitimacy; maintain coalition cohesion; counter-narratives without backfire.",
                    PoliticalContext = "Atropia–Gorgas humanitarian corridor under scrutiny; Limaria domestic politics volatile.",
                    RulesOfEngagement = "Blue: information defense, legal constraints. Red: influence amplification, plausible deniability. White: civ/NGO/media injects.",
                    VictoryConditions = "Convoys run on schedule; protest intensity reduced; credible attribution brief delivered.",
                    Nations = new[]
                    {
                        new Nation { Name = "Atropia", Alignment = "friendly" },
                        new Nation { Name = "Gorgas", Alignment = "friendly" },
                        new Nation { Name = "Limaria", Alignment = "adversary" }
                    },
                    ThreatActors = new[]
                    {
                        new ThreatActor
                        {
                            Name = "Limarian IO Cell",
                            Type = "state-proxy",
                            Capability = 6,
                            Ttps = "AMITT:S-SeedRumor,AMITT:A-AuthHack,AMITT:D-DeepfakeAudio,AMITT:M-MemeWarfare"
                        }
                    },
                    Injects = new[]
                    {
                        new Inject { Trigger = "T+0m", Title = "Hashtag storm targets aid convoy" },
                        new Inject { Trigger = "T+45m", Title = "Deepfake audio of 'Atropian general' leaks" },
                        new Inject { Trigger = "T+2h", Title = "NGO statement misquoted by bots" },
                        new Inject { Trigger = "OnCounter", Title = "Narrative backfire risk: 'censorship' claims" }
                    },
                    UserPools = new[]
                    {
                        new UserPool { Role = "InfoOps Analyst", Count = 3 },
                        new UserPool { Role = "Legal/Policy Advisor", Count = 1 },
                        new UserPool { Role = "Coalition Liaison", Count = 1 },
                        new UserPool { Role = "Red IO Operator", Count = 2 },
                        new UserPool { Role = "White Media/NGO", Count = 2 }
                    }
                },
                TechnicalEnvironment = new TechnicalEnvironment
                {
                    NetworkTopology = "Open social platforms + encrypted chat monitoring feeds; OSINT tooling.",
                    Services = "Media monitoring, Bot detection, Crisis comms",
                    Assets = "Convoy schedule data, Public affairs channels",
                    Defenses = "[\"OSINT dashboards\",\"Bot classifiers\",\"Rapid comms playbook\"]",
                    Vulnerabilities = new[]
                    {
                        new Vulnerability { Asset = "Public Affairs", Cve = "Slow-Approval-Cycle", Severity = "High" },
                        new Vulnerability { Asset = "Convoy Info", Cve = "Over-Disclosure", Severity = "Medium" }
                    }
                },
                GameMechanics = new GameMechanics
                {
                    TimelineType = "compressed",
                    DurationHours = 4,
                    AdjudicationType = "manual",
                    EscalationLadder = "Narrative detection → Attribution → Counter-messaging → Community engagement",
                    BranchingLogic = "Heavy-handed takedowns increase backlash; community partner outreach reduces volatility.",
                    CollectLogs = false,
                    CollectNetwork = false,
                    CollectEndpoint = false,
                    CollectChat = true,
                    PerformanceMetrics = "Narrative reach, bot/troll suppression, protest size, convoy delays"
                },
                ScenarioTimeline = new ScenarioTimeline
                {
                    ExerciseDuration = 240,
                    ScenarioTimelineEvents = new[]
                    {
                        new ScenarioTimelineEvent { Time = "T+0m", Number = 1, Assigned = "Red Team", Description = "Coordinated hashtag surge", Status = "Pending" },
                        new ScenarioTimelineEvent { Time = "T+45m", Number = 2, Assigned = "White Cell", Description = "Deepfake drop", Status = "Pending" },
                        new ScenarioTimelineEvent { Time = "T+2h", Number = 3, Assigned = "Blue Team", Description = "Counter-narrative and partner outreach", Status = "Pending" },
                        new ScenarioTimelineEvent { Time = "T+4h", Number = 4, Assigned = "White Cell", Description = "After-action and metrics", Status = "Pending" }
                    }
                }
            };
        }

        private static Scenario CreateInfluenceIronOrchard()
        {
            return new Scenario
            {
                Name = "Iron Orchard — Food Sovereignty Flashpoint",
                Description = "Limarian farming protests gain viral traction after forged export data suggests Atropia is siphoning grain. Roadblocks threaten a Gorgas–Atropia relief corridor.",
                CreatedAt = DateTime.UtcNow.AddDays(-11),
                UpdatedAt = DateTime.UtcNow.AddDays(-11),
                ScenarioParameters = new ScenarioParameters
                {
                    Objectives = "Validate/kill forged datasets; sustain corridor throughput; de-escalate unrest; preserve civil liberties.",
                    PoliticalContext = "Election season in Limaria; Donovian media echoing unrest.",
                    RulesOfEngagement = "Blue: data transparency, community comms. Red: data forgery, influencer amplification.",
                    VictoryConditions = "Corridor ≥ 80% capacity; forged data publicly discredited by neutral validators.",
                    Nations = new[]
                    {
                        new Nation { Name = "Limaria", Alignment = "neutral" },
                        new Nation { Name = "Atropia", Alignment = "friendly" },
                        new Nation { Name = "Gorgas", Alignment = "friendly" },
                        new Nation { Name = "Donovia", Alignment = "adversary" }
                    },
                    ThreatActors = new[]
                    {
                        new ThreatActor
                        {
                            Name = "Proxy Media Fronts",
                            Type = "state-proxy",
                            Capability = 5,
                            Ttps = "AMITT:F-ForgedStats,AMITT:I-InfluencerAstroturf,AMITT:V-VideoRemix"
                        }
                    },
                    Injects = new[]
                    {
                        new Inject { Trigger = "T+0m", Title = "Viral 'export ledger' spreadsheet" },
                        new Inject { Trigger = "T+1h", Title = "Drone 'seizure' video surfaces" },
                        new Inject { Trigger = "T+3h", Title = "Independent fact-checker weighs in" }
                    },
                    UserPools = new[]
                    {
                        new UserPool { Role = "Data Forensics", Count = 2 },
                        new UserPool { Role = "Community Liaison", Count = 2 },
                        new UserPool { Role = "Red IO Cell", Count = 2 },
                        new UserPool { Role = "White Observers/Media", Count = 2 }
                    }
                },
                TechnicalEnvironment = new TechnicalEnvironment
                {
                    NetworkTopology = "Open web + municipal comms; logistics tracking feeds.",
                    Services = "Data portal, Fact-checking hub, Social listening",
                    Assets = "Corridor schedules, Freight telemetry",
                    Defenses = "[\"Public data provenance\",\"Chain-of-custody for videos\"]",
                    Vulnerabilities = new[]
                    {
                        new Vulnerability { Asset = "Telemetry", Cve = "Unverified-Screenshots", Severity = "Medium" }
                    }
                },
                GameMechanics = new GameMechanics
                {
                    TimelineType = "compressed",
                    DurationHours = 4,
                    AdjudicationType = "manual",
                    EscalationLadder = "Detection → Transparency release → Third-party validation → Community engagement",
                    BranchingLogic = "If transparency lags, unrest grows; if partners endorse, narratives collapse.",
                    CollectLogs = false,
                    CollectNetwork = false,
                    CollectEndpoint = false,
                    CollectChat = true,
                    PerformanceMetrics = "Throughput %, protest dispersion time, neutral-party endorsements"
                },
                ScenarioTimeline = new ScenarioTimeline
                {
                    ExerciseDuration = 240,
                    ScenarioTimelineEvents = new[]
                    {
                        new ScenarioTimelineEvent { Time = "T+0m", Number = 1, Assigned = "Red Team", Description = "Release forged ledger", Status = "Pending" },
                        new ScenarioTimelineEvent { Time = "T+1h", Number = 2, Assigned = "White Cell", Description = "Drone clip inject", Status = "Pending" },
                        new ScenarioTimelineEvent { Time = "T+3h", Number = 3, Assigned = "Blue Team", Description = "Data provenance briefing", Status = "Pending" },
                        new ScenarioTimelineEvent { Time = "T+4h", Number = 4, Assigned = "White Cell", Description = "Hot wash", Status = "Pending" }
                    }
                }
            };
        }

        // ===== Next-Gen / Hybrid =====

        private static Scenario CreateNextGenAutonomyConvoy()
        {
            return new Scenario
            {
                Name = "Autonomy Misrouting — Phantom Detour",
                Description = "Mixed human/robotic convoy is misrouted by adversary EMS/PNT spoofing and spoofed 'authority' messages. Teams must detect manipulation, re-route safely, and communicate under uncertainty.",
                CreatedAt = DateTime.UtcNow.AddDays(-8),
                UpdatedAt = DateTime.UtcNow.AddDays(-8),
                ScenarioParameters = new ScenarioParameters
                {
                    Objectives = "Detect spoofed advisories; validate PNT; regain convoy control; maintain safety and tempo.",
                    PoliticalContext = "Civilian traffic impacted; municipal authorities cautious.",
                    RulesOfEngagement = "Blue: cyber/EM defenses, comms. Red: spoof/spear-phish/false signage. White: safety adjudication.",
                    VictoryConditions = "Convoy arrives within tolerance; no safety incidents; credible incident report delivered.",
                    Nations = new[]
                    {
                        new Nation { Name = "Gorgas", Alignment = "friendly" },
                        new Nation { Name = "Limaria", Alignment = "adversary" }
                    },
                    ThreatActors = new[]
                    {
                        new ThreatActor
                        {
                            Name = "Hybrid EW/IO Cell",
                            Type = "state",
                            Capability = 7,
                            Ttps = "T1598.003,T1557.002,AMITT:A-FakeAuthority,EW:PNT-Spoof"
                        }
                    },
                    Injects = new[]
                    {
                        new Inject { Trigger = "T+0m", Title = "Emergency detour SMS signed 'Municipal Ops'" },
                        new Inject { Trigger = "T+30m", Title = "GPS divergence between convoy units" },
                        new Inject { Trigger = "T+1h", Title = "Social posts claim 'accident on primary route'" }
                    },
                    UserPools = new[]
                    {
                        new UserPool { Role = "Convoy Commander", Count = 1 },
                        new UserPool { Role = "Autonomy Engineer", Count = 2 },
                        new UserPool { Role = "Blue EW/Cyber", Count = 2 },
                        new UserPool { Role = "Red Operator", Count = 2 },
                        new UserPool { Role = "White Safety", Count = 1 }
                    }
                },
                TechnicalEnvironment = new TechnicalEnvironment
                {
                    NetworkTopology = "Vehicle mesh + LTE/MCX backhaul; GPS/GNSS receivers; roadside units.",
                    Services = "Fleet mgmt, Maps, OTA updates, V2X",
                    Assets = "Autonomous shuttles, Command vehicle, RSUs",
                    Defenses = "[\"PNT anomaly detection\",\"Message signing\",\"Geofencing\",\"Manual override SOPs\"]",
                    Vulnerabilities = new[]
                    {
                        new Vulnerability { Asset = "V2X", Cve = "Weak-Origin-Auth", Severity = "High" },
                        new Vulnerability { Asset = "Nav Stack", Cve = "Overtrust-Map-Updates", Severity = "Medium" }
                    }
                },
                GameMechanics = new GameMechanics
                {
                    TimelineType = "real-time",
                    DurationHours = 3,
                    AdjudicationType = "hybrid",
                    EscalationLadder = "Anomaly detection → Source validation → Route correction → Public comms",
                    BranchingLogic = "If spoof not caught, convoy enters choke point; if caught, safe reroute with time penalty.",
                    CollectLogs = true,
                    CollectNetwork = true,
                    CollectEndpoint = false,
                    CollectChat = true,
                    PerformanceMetrics = "Arrival delay, safety incidents, correct spoof attribution, comms latency"
                },
                ScenarioTimeline = new ScenarioTimeline
                {
                    ExerciseDuration = 180,
                    ScenarioTimelineEvents = new[]
                    {
                        new ScenarioTimelineEvent { Time = "T+0m", Number = 1, Assigned = "Red Team", Description = "Spoof detour advisory", Status = "Pending" },
                        new ScenarioTimelineEvent { Time = "T+30m", Number = 2, Assigned = "Blue Team", Description = "Detect PNT anomalies", Status = "Pending" },
                        new ScenarioTimelineEvent { Time = "T+120m", Number = 3, Assigned = "White Cell", Description = "Safety adjudication checkpoint", Status = "Pending" }
                    }
                }
            };
        }

        private static Scenario CreateNextGenCognitiveTwin()
        {
            return new Scenario
            {
                Name = "Cognitive/Digital-Twin — Mirage Analytics",
                Description = "Adversary poisons training data for a logistics digital-twin, biasing forecast models. Teams must detect drift, quarantine tainted data, and restore trustworthy analytics.",
                CreatedAt = DateTime.UtcNow.AddDays(-7),
                UpdatedAt = DateTime.UtcNow.AddDays(-7),
                ScenarioParameters = new ScenarioParameters
                {
                    Objectives = "Detect model/data drift; trace data lineage; rebuild models; communicate uncertainty to commanders.",
                    PoliticalContext = "Stakeholders rely on the twin for resource allocation.",
                    RulesOfEngagement = "Blue: data forensics/ML ops. Red: subtle poisoning, realistic noise. White: decision-maker injects.",
                    VictoryConditions = "Model accuracy restored; contaminated streams identified; decision brief delivered with calibrated confidence.",
                    Nations = new[]
                    {
                        new Nation { Name = "Atropia", Alignment = "friendly" },
                        new Nation { Name = "Donovia", Alignment = "adversary" }
                    },
                    ThreatActors = new[]
                    {
                        new ThreatActor
                        {
                            Name = "Data Access Broker",
                            Type = "proxy",
                            Capability = 6,
                            Ttps = "ML:DataPoison,ML:LabelFlip,ML:BackdoorTrigger,ML:Drift"
                        }
                    },
                    Injects = new[]
                    {
                        new Inject { Trigger = "T+0m", Title = "KPIs degrade quietly; no single alarm" },
                        new Inject { Trigger = "T+90m", Title = "Conflicting reports from field vs twin" },
                        new Inject { Trigger = "OnDetect", Title = "Backdoor trigger discovered in supplier feed" }
                    },
                    UserPools = new[]
                    {
                        new UserPool { Role = "ML Ops", Count = 2 },
                        new UserPool { Role = "Data Engineer", Count = 2 },
                        new UserPool { Role = "Blue Intel", Count = 1 },
                        new UserPool { Role = "Red Data Manipulator", Count = 2 },
                        new UserPool { Role = "White Commander", Count = 1 }
                    }
                },
                TechnicalEnvironment = new TechnicalEnvironment
                {
                    NetworkTopology = "Data lake + ETL + feature store + model serving.",
                    Services = "Forecasting, Optimization, Dashboards",
                    Assets = "Pipelines, Datasets, Models, Lineage graphs",
                    Defenses = "[\"Canary models\",\"Data validation\",\"Lineage tracking\",\"Backtesting\"]",
                    Vulnerabilities = new[]
                    {
                        new Vulnerability { Asset = "Supplier Feed", Cve = "No-Content-Signature", Severity = "High" },
                        new Vulnerability { Asset = "ETL", Cve = "Insufficient-Data-Contracts", Severity = "Medium" }
                    }
                },
                GameMechanics = new GameMechanics
                {
                    TimelineType = "compressed",
                    DurationHours = 5,
                    AdjudicationType = "manual",
                    EscalationLadder = "Drift detection → Source tracing → Model rebuild → Confidence briefing",
                    BranchingLogic = "If poisoning persists, forecast error drives bad deployment orders.",
                    CollectLogs = true,
                    CollectNetwork = false,
                    CollectEndpoint = false,
                    CollectChat = true,
                    PerformanceMetrics = "Time to detect drift, % contaminated sources isolated, post-fix MAPE"
                },
                ScenarioTimeline = new ScenarioTimeline
                {
                    ExerciseDuration = 300,
                    ScenarioTimelineEvents = new[]
                    {
                        new ScenarioTimelineEvent { Time = "T+0m", Number = 1, Assigned = "White Cell", Description = "KPI anomaly notice", Status = "Pending" },
                        new ScenarioTimelineEvent { Time = "T+120m", Number = 2, Assigned = "Blue Team", Description = "Lineage investigation", Status = "Pending" },
                        new ScenarioTimelineEvent { Time = "T+300m", Number = 3, Assigned = "White Cell", Description = "Commander decision brief", Status = "Pending" }
                    }
                }
            };
        }

        private static Scenario CreateNextGenSpacePntSpoofing()
        {
            return new Scenario
            {
                Name = "Space/PNT — Quiet Sky",
                Description = "Localized GNSS spoofing degrades timing and navigation across civil and mission systems. Teams must isolate the source, implement holdover strategies, and coordinate with civil authorities.",
                CreatedAt = DateTime.UtcNow.AddDays(-6),
                UpdatedAt = DateTime.UtcNow.AddDays(-6),
                ScenarioParameters = new ScenarioParameters
                {
                    Objectives = "Detect PNT anomalies; switch to resilient timing; coordinate spectrum monitoring; maintain critical services.",
                    PoliticalContext = "Civil aviation and finance timing impacted; public scrutiny high.",
                    RulesOfEngagement = "Blue: technical mitigation + public comms. Red: mobile spoofers + deception. White: regulator/aviation/finance injects.",
                    VictoryConditions = "Critical services stay within timing SLA; spoofing geo-localized and suppressed; transparent public report.",
                    Nations = new[]
                    {
                        new Nation { Name = "Gorgas", Alignment = "friendly" },
                        new Nation { Name = "Unknown Proxies", Alignment = "adversary" }
                    },
                    ThreatActors = new[]
                    {
                        new ThreatActor
                        {
                            Name = "PNT Disruption Cell",
                            Type = "state-proxy",
                            Capability = 6,
                            Ttps = "EW:PNT-Spoof,EW:Jamming,OPS:Mobile-Emitter"
                        }
                    },
                    Injects = new[]
                    {
                        new Inject { Trigger = "T+0m", Title = "Airport reports RNAV deviations" },
                        new Inject { Trigger = "T+45m", Title = "Finance NTP drift alarms" },
                        new Inject { Trigger = "T+2h", Title = "Public rumor of 'satellite hack'" }
                    },
                    UserPools = new[]
                    {
                        new UserPool { Role = "PNT Engineer", Count = 2 },
                        new UserPool { Role = "Network/Sys", Count = 2 },
                        new UserPool { Role = "Blue EW", Count = 1 },
                        new UserPool { Role = "Red EW Cell", Count = 2 },
                        new UserPool { Role = "White Regulator/Media", Count = 2 }
                    }
                },
                TechnicalEnvironment = new TechnicalEnvironment
                {
                    NetworkTopology = "Critical infra timing nets + aviation approach aids + finance DCs.",
                    Services = "NTP/PTP, RNAV/RNP, Monitoring",
                    Assets = "Stratum servers, GNSS receivers, Spectrum sensors",
                    Defenses = "[\"Holdover clocks\",\"Multi-constellation receivers\",\"Angle-of-arrival sensors\"]",
                    Vulnerabilities = new[]
                    {
                        new Vulnerability { Asset = "Receivers", Cve = "Single-Constellation-Trust", Severity = "High" },
                        new Vulnerability { Asset = "Timing", Cve = "No-Holdover-Policy", Severity = "Medium" }
                    }
                },
                GameMechanics = new GameMechanics
                {
                    TimelineType = "real-time",
                    DurationHours = 4,
                    AdjudicationType = "hybrid",
                    EscalationLadder = "Anomaly → Localization → Mitigation → Public brief",
                    BranchingLogic = "If localization fails, wide commercial impact escalates public risk.",
                    CollectLogs = true,
                    CollectNetwork = true,
                    CollectEndpoint = false,
                    CollectChat = true,
                    PerformanceMetrics = "Timing SLA adherence, localization accuracy, incident comms quality"
                },
                ScenarioTimeline = new ScenarioTimeline
                {
                    ExerciseDuration = 240,
                    ScenarioTimelineEvents = new[]
                    {
                        new ScenarioTimelineEvent { Time = "T+0m", Number = 1, Assigned = "White Cell", Description = "Aviation deviation report", Status = "Pending" },
                        new ScenarioTimelineEvent { Time = "T+90m", Number = 2, Assigned = "Blue Team", Description = "PNT mitigation rollout", Status = "Pending" },
                        new ScenarioTimelineEvent { Time = "T+240m", Number = 3, Assigned = "White Cell", Description = "Public/Regulator briefing", Status = "Pending" }
                    }
                }
            };
        }
    }
}
