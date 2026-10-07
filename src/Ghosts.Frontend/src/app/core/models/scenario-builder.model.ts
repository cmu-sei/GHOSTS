// Scenario Builder Models

import { AuthoringModelChoice } from './scenario-authoring.model';

/** The model a scenario's Builder uses for extraction and the conversation, and the configured choices (GET/PUT builder/model). */
export interface BuilderModel {
  model: string;
  /** The model's effort level for the conversation (H3), or null for the model's default. */
  effort: string | null;
  models: AuthoringModelChoice[];
}

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
  contentPreview: string;
  /** Chunks extraction has read into entities and the graph, and chunks still waiting for it. */
  extractedChunkCount: number;
  pendingChunkCount: number;
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

export interface ExtractionResult {
  entitiesCreated: number;
  edgesCreated: number;
  chunksProcessed: number;
  errors: string[];
}

/** Pushed over the Scenario Builder's hub ("extractionProgress") after each chunk extraction reads. */
export interface ExtractionProgress {
  scenarioId: number;
  status: string;
  chunksProcessed: number;
  totalChunks: number;
  entitiesCreated: number;
  edgesCreated: number;
  timestamp: string;
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

// ATT&CK models
// Entity type constants
export const ENTITY_TYPES = [
  'Person', 'Organization', 'System', 'Network', 'Location',
  'Software', 'ThreatActor', 'Campaign', 'Vulnerability',
  'DataAsset', 'Service', 'Custom'
] as const;

export const EDGE_TYPES = [
  'MemberOf', 'Targets', 'Exploits', 'Uses', 'LocatedAt',
  'CommunicatesWith', 'DependsOn', 'Accesses', 'Owns',
  'ReportsTo', 'AffiliatedWith', 'DefendedBy', 'CommandsAndControl',
  'AttributedTo', 'Conducts', 'Delivers', 'Impacts', 'Custom'
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
