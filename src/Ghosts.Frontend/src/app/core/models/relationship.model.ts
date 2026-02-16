// Relationship and Social Graph models
// These extend the base models from activity.model.ts

export interface SocialGraphWithDetails {
  id: string;
  name: string;
  currentStep: number;
  npcId?: string;
  npcName?: string;
  campaign?: string;
  enclave?: string;
  team?: string;
  connections?: SocialConnectionDetail[];
  knowledge?: KnowledgeDetail[];
  beliefs?: BeliefDetail[];
}

export interface SocialConnectionDetail {
  id: number;
  socialGraphId: string;
  connectedNpcId: string;
  name: string;
  distance?: string;
  relationshipStatus: number;
  interactionCount?: number;
  totalInteractionValue?: number;
  connectionScore?: number;
  interactions?: InteractionDetail[];
}

export interface InteractionDetail {
  id: number;
  socialConnectionId: number;
  step: number;
  value: number;
  timestamp?: string;
}

export interface KnowledgeDetail {
  id: number;
  socialGraphId: string;
  toNpcId: string;
  fromNpcId: string;
  toNpcName?: string;
  fromNpcName?: string;
  topic: string;
  step: number;
  value: number;
}

export interface BeliefDetail {
  id: number;
  socialGraphId: string;
  toNpcId: string;
  fromNpcId: string;
  toNpcName?: string;
  fromNpcName?: string;
  name: string;
  step: number;
  likelihood: number;
  posterior: number;
}

export interface RelationshipSummary {
  npcId: string;
  npcName: string;
  campaign?: string;
  enclave?: string;
  team?: string;
  totalConnections: number;
  totalKnowledgeTopics: number;
  totalBeliefs: number;
  averageRelationshipStatus: number;
}

export interface RelationshipExport {
  npcId: string;
  source: string;
  target: string;
  type: string;
  value?: number;
}

export interface InteractionMapNode {
  id: string;
  start: string;
  end: string;
}

export interface InteractionMapLink {
  source: string;
  target: string;
  start: string;
  end: string;
}

export interface InteractionMap {
  nodes: InteractionMapNode[];
  links: InteractionMapLink[];
}
