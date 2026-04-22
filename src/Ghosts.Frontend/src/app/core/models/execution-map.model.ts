export interface GeoJsonFeatureCollection {
  type: 'FeatureCollection';
  features: GeoJsonFeature[];
}

export interface GeoJsonFeature {
  type: 'Feature';
  geometry: GeoJsonGeometry;
  properties: GeoJsonProperties;
}

export interface GeoJsonGeometry {
  type: 'Point' | 'LineString' | 'Polygon' | 'MultiPoint';
  coordinates: number[] | number[][] | number[][][];
}

export interface GeoJsonProperties {
  id: string;
  featureType: string;
  entityId: string;
  label: string;
  description: string;
  status: string;
  category: string;
  team: string;
  scenarioId?: number;
  executionId?: number;
  createdAt: string;
  validFrom?: string;
  validTo?: string;
  extra?: Record<string, unknown>;
}

export interface MapLayerInfo {
  layerId: string;
  label: string;
  featureType: string;
  featureCount: number;
  defaultVisible: boolean;
}

export interface MapTimelineInfo {
  earliestEvent?: string;
  latestEvent?: string;
  totalEvents: number;
  buckets: MapTimelineBucket[];
}

export interface MapTimelineBucket {
  start: string;
  end: string;
  count: number;
}

export interface MapEntityDetail {
  entityId: string;
  featureType: string;
  label: string;
  description: string;
  status: string;
  category: string;
  team: string;
  latitude: number;
  longitude: number;
  properties: Record<string, unknown>;
  relatedEntities: MapRelatedEntity[];
  recentEvents: MapRecentEvent[];
}

export interface MapRelatedEntity {
  entityId: string;
  featureType: string;
  label: string;
  relationshipType: string;
}

export interface MapRecentEvent {
  id: number;
  timestamp: string;
  eventType: string;
  description: string;
  severity: string;
}

export interface MapSearchResult {
  entityId: string;
  featureType: string;
  label: string;
  category: string;
  latitude: number;
  longitude: number;
  score: number;
}

export type FeatureTypeKey =
  | 'poi'
  | 'npc'
  | 'machine'
  | 'site'
  | 'network'
  | 'event'
  | 'connection'
  | 'scenarioentity';

export const FEATURE_TYPE_COLORS: Record<string, string> = {
  Site: '#38bdf8',
  Machine: '#22c55e',
  Npc: '#fbbf24',
  Event: '#f87171',
  Poi: '#c084fc',
  ScenarioEntity: '#94a3b8',
  Connection: '#64748b',
  Network: '#22d3ee',
};

export const STATUS_COLORS: Record<string, string> = {
  Online: '#22c55e',
  Active: '#3b82f6',
  Degraded: '#f59e0b',
  Compromised: '#ef4444',
  Alert: '#f97316',
  Offline: '#6b7280',
  Inactive: '#6b7280',
  Info: '#3b82f6',
  Warning: '#f59e0b',
};
