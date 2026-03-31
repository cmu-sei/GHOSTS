// Scenario Builder Models

export interface ScenarioSource {
  id: number;
  name: string;
  sourceType: string;
  mimeType: string;
  originalFileName: string;
  fileSizeBytes: number;
  status: string;
  errorMessage: string;
  createdAt: string;
  chunkCount: number;
}

export interface ScenarioSourceChunk {
  id: number;
  sourceId: number;
  chunkIndex: number;
  content: string;
  tokenCount: number;
  extractionStatus: string;
  createdAt: string;
}

export interface ScenarioEntity {
  id: string;
  name: string;
  entityType: string;
  description: string;
  properties: string;
  confidence: number;
  origin: string;
  sourceId: number | null;
  npcId: string | null;
  externalId: string;
  isReviewed: boolean;
  createdAt: string;
}

export interface ScenarioEdge {
  id: string;
  sourceEntityId: string;
  targetEntityId: string;
  edgeType: string;
  label: string;
  weight: number;
  confidence: number;
  origin: string;
  isReviewed: boolean;
}

export interface ScenarioGraph {
  nodes: ScenarioEntity[];
  edges: ScenarioEdge[];
}

export interface ScenarioEnrichment {
  id: number;
  entityId: string | null;
  enrichmentType: string;
  externalId: string;
  name: string;
  description: string;
  data: string;
  source: string;
  createdAt: string;
}

export interface ScenarioCompilation {
  id: number;
  name: string;
  status: string;
  npcCount: number;
  timelineEventCount: number;
  injectCount: number;
  createdAt: string;
  completedAt: string | null;
  errorMessage: string;
}

export interface NpcForAssignment {
  npcId: string;
  npcName: string;
  entityName: string;
  assignedMachineId: string | null;
  assignedMachineName: string | null;
  assignmentId: number | null;
}

export interface NpcAssignment {
  id: number;
  compilationId: number;
  npcId: string;
  npcName: string;
  machineId: string;
  machineName: string;
  createdAt: string;
}

export interface DeploymentReadiness {
  isReady: boolean;
  totalNpcs: number;
  assignedNpcs: number;
  unassignedNpcs: number;
  issues: string[];
}

export interface ExtractionResult {
  entitiesCreated: number;
  edgesCreated: number;
  chunksProcessed: number;
  errors: string[];
}

export interface CreateTextSource {
  name: string;
  content: string;
}

export interface CreateUrlSource {
  name: string;
  url: string;
}

export interface CreateEntity {
  name: string;
  entityType: string;
  description: string;
  properties: string;
  confidence: number;
}

export interface UpdateEntity {
  name: string;
  entityType: string;
  description: string;
  properties: string;
  confidence: number;
  isReviewed: boolean;
}

export interface CreateEdge {
  sourceEntityId: string;
  targetEntityId: string;
  edgeType: string;
  label: string;
  weight: number;
  confidence: number;
}

export interface ApplyTechnique {
  entityId: string;
  techniqueId: string;
}

export interface ApplyGroup {
  entityId: string;
  groupId: string;
}

export interface CompileRequest {
  name: string;
  generateNpcs: boolean;
  generateTimeline: boolean;
  mapAttackToInjects: boolean;
}

export interface AssistantMessage {
  role: string;
  content: string;
}

export interface AssistantRequest {
  message: string;
  history: AssistantMessage[];
}

export interface AssistantResponse {
  response: string;
  action: string;
  actionData: any;
}

// ATT&CK models
export interface AttackTechnique {
  id: string;
  name: string;
  description: string;
  tactics: string;
  platforms: string;
  url: string;
  isSubtechnique: boolean;
  parentId: string;
}

export interface AttackTechniqueSummary {
  id: string;
  name: string;
  tactics: string;
  isSubtechnique: boolean;
}

export interface AttackGroup {
  id: string;
  name: string;
  aliases: string;
  description: string;
  url: string;
  techniques: AttackTechniqueSummary[];
}

export interface AttackGroupSummary {
  id: string;
  name: string;
  aliases: string;
  techniqueCount: number;
}

// Entity type constants
export const ENTITY_TYPES = [
  'Person', 'Organization', 'System', 'Network', 'Location',
  'Software', 'ThreatActor', 'Campaign', 'Vulnerability',
  'DataAsset', 'Service', 'Custom'
] as const;

export const EDGE_TYPES = [
  'MemberOf', 'Targets', 'Exploits', 'Uses', 'LocatedAt',
  'CommunicatesWith', 'DependsOn', 'Accesses', 'Owns',
  'ReportsTo', 'AffiliatedWith', 'DefendedBy', 'CommandsAndControl', 'Custom'
] as const;

export const ENTITY_COLORS: Record<string, string> = {
  Person: '#4CAF50',
  Organization: '#2196F3',
  System: '#FF9800',
  Network: '#9C27B0',
  Location: '#795548',
  Software: '#00BCD4',
  ThreatActor: '#F44336',
  Campaign: '#E91E63',
  Vulnerability: '#FF5722',
  DataAsset: '#607D8B',
  Service: '#3F51B5',
  Custom: '#9E9E9E'
};

export const ORIGIN_STYLES: Record<string, { border: string; dash: string }> = {
  Extracted: { border: 'solid', dash: '' },
  Operator: { border: 'dashed', dash: '5,5' },
  Enriched: { border: 'double', dash: '' },
  Generated: { border: 'dotted', dash: '2,2' }
};
