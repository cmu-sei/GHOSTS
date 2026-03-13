export interface Activity {
  id?: string;
  machineId?: string;
  machineName?: string;
  handler?: string;
  command?: string;
  commandArg?: string;
  result?: string;
  trackableId?: string;
  createdUtc?: string;
}

export interface ActivityQueryParams {
  machineId?: string;
  groupId?: string;
  skip?: number;
  take?: number;
}

// NPC Activity Models
export enum NpcActivityType {
  SocialMediaPost = 0,
  NextAction = 10
}

export interface NpcActivity {
  id: number;
  npcId: string;
  activityType: NpcActivityType;
  detail: string;
  createdUtc: string;
}

export interface NpcRecord {
  id: string;
  machineId?: string;
  campaign?: string;
  enclave?: string;
  team?: string;
  npcProfile?: any; // References NpcProfile from npc.model
  npcSocialGraph?: NpcSocialGraph;
  lastActivity?: string;
  lastActivityType?: string;
  lastActivityTime?: string;
}

export interface NpcSocialGraph {
  id: string;
  name: string;
  currentStep: number;
  connections?: NpcSocialConnection[];
  knowledge?: NpcLearning[];
  beliefs?: NpcBelief[];
}

export interface NpcSocialConnection {
  id: string;
  socialGraphId: string;
  connectedNpcId: string;
  name: string;
  distance?: string;
  relationshipStatus: number;
  interactions?: NpcInteraction[];
}

export interface NpcInteraction {
  id: number;
  socialConnectionId: string;
  step: number;
  value: number;
}

export interface NpcLearning {
  id: number;
  socialGraphId: string;
  toNpcId: string;
  fromNpcId: string;
  topic: string;
  step: number;
  value: number;
}

export interface NpcBelief {
  id: number;
  socialGraphId: string;
  toNpcId: string;
  fromNpcId: string;
  name: string;
  step: number;
  likelihood: number;
  posterior: number;
}

export interface NpcNameId {
  id: string;
  name: string;
}

export interface AiActionRequest {
  handler: string;
  action: string;
  scale: number;
  who: string;
  reasoning?: string;
  sentiment?: string;
  original?: string;
}
