export interface Scenario {
  id: number;
  name: string;
  description: string;
  createdAt: Date;
  updatedAt: Date;
  scenarioParameters?: ScenarioParameters;
  technicalEnvironment?: TechnicalEnvironment;
  simulationMechanics?: SimulationMechanics;
  timeline?: ScenarioTimeline;
}

export interface CreateScenario {
  name: string;
  description: string;
  scenarioParameters?: ScenarioParameters;
  technicalEnvironment?: TechnicalEnvironment;
  simulationMechanics?: SimulationMechanics;
  timeline?: ScenarioTimeline;
}

export interface ScenarioParameters {
  nations: Nation[];
  threatActors: ThreatActor[];
  injects: Inject[];
  userPools: UserPool[];
  objectives?: string;
  politicalContext?: string;
  rulesOfEngagement?: string;
  victoryConditions?: string;
}

export interface Nation {
  name: string;
  alignment: string; // friendly, adversary, neutral
}

export interface ThreatActor {
  name: string;
  type: string; // state, criminal, hacktivist, insider
  capability: number;
  ttps: string[];
  ttpsString?: string; // Helper property for editing
}

export interface Inject {
  trigger: string;
  title: string;
}

export interface UserPool {
  role: string;
  count: number;
}

export interface TechnicalEnvironment {
  networkTopology?: string;
  services?: string;
  assets?: string;
  defenses: string[];
  vulnerabilities: Vulnerability[];
}

export interface Vulnerability {
  asset: string;
  cve: string;
  severity: string;
}

export interface SimulationMechanics {
  timelineType: string; // real-time, compressed, turn-based
  durationHours: number;
  adjudicationType: string; // manual, automated, hybrid
  escalationLadder?: string;
  branchingLogic?: string;
  telemetry: Telemetry;
  performanceMetrics?: string;
}

export interface Telemetry {
  collectLogs: boolean;
  collectNetwork: boolean;
  collectEndpoint: boolean;
  collectChat: boolean;
}

export interface ScenarioTimeline {
  exerciseDuration: number;
  events: ScenarioTimelineEvent[];
}

export interface ScenarioTimelineEvent {
  time: string;
  number: number;
  assigned: string; // White Cell, Red Team, Blue Team, Green Cell
  description: string;
  status: string; // Pending, Active, Complete
}
