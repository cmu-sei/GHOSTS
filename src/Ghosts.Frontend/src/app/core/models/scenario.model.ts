export interface Scenario {
  id: number;
  name: string;
  description: string;
  createdAt: Date;
  updatedAt: Date;
  scenarioParameters?: ScenarioParameters;
  technicalEnvironment?: TechnicalEnvironment;
  gameMechanics?: GameMechanics;
  timeline?: ScenarioTimeline;
  simulationMechanics?: GameMechanics; // alias for gameMechanics
}

export interface ScenarioParameters {
  nations: Nation[];
  threatActors: ThreatActor[];
  injects: Inject[];
  userPools: UserPool[];
  objectives: string;
  politicalContext: string;
  rulesOfEngagement: string;
  victoryConditions: string;
}

export interface Nation {
  name: string;
  alignment: string;
}

export interface ThreatActor {
  name: string;
  type: string;
  capability: number;
  ttps: string[];
  ttpsString?: string; // For editing purposes
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
  networkTopology: string;
  services: string;
  assets: string;
  platforms?: {
    websites?: string[];
    socialMedia?: string[];
    emailProviders?: string[];
    cloudServices?: string[];
    collaborationTools?: string[];
    [key: string]: string[] | undefined;
  };
  defenses: string[];
  vulnerabilities: Vulnerability[];
}

export interface Vulnerability {
  asset: string;
  cve: string;
  severity: string;
}

export interface GameMechanics {
  timelineType: string;
  durationHours: number;
  adjudicationType: string;
  escalationLadder: string;
  branchingLogic: string;
  telemetry: Telemetry;
  performanceMetrics: string;
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
  assigned: string;
  description: string;
  status: string;
}

export interface CreateScenario {
  name: string;
  description: string;
  scenarioParameters: ScenarioParameters;
  technicalEnvironment: TechnicalEnvironment;
  gameMechanics?: GameMechanics;
  simulationMechanics?: GameMechanics; // Alias for gameMechanics used in UI
  timeline: ScenarioTimeline;
}

export interface ScenarioListItem {
  id: number;
  name: string;
}
