// Social Graph specific models for knowledge and topics display

export interface SocialGraphSummary {
  id: string;
  name: string;
  npcId?: string;
  avatar?: string;
  campaign?: string;
  enclave?: string;
  team?: string;
  currentStep: number;
  knowledgeTopics: KnowledgeTopic[];
  preferences: PreferenceSummary[];
  connectionCount: number;
  beliefCount: number;
}

export interface KnowledgeTopic {
  topic: string;
  count: number;
  totalValue: number;
  averageValue: number;
  strength: number; // 0-9 for z0-z9 color coding
}

export interface ConnectionSummary {
  id: number;
  connectedNpcId: string;
  name: string;
  avatar?: string;
  relationshipStatus: number;
  interactionCount: number;
  totalInteractionValue: number;
  connectionScore: number;
  interactions: ConnectionInteraction[];
  knowledgeTransfers: KnowledgeTransfer[];
  hasDecay: boolean;
}

export interface ConnectionInteraction {
  id: number;
  step: number;
  value: number;
  change: 'up' | 'down' | 'neutral';
}

export interface KnowledgeTransfer {
  id: number;
  topic: string;
  step: number;
  value: number;
  fromNpcId: string;
  fromNpcName?: string;
}

export interface KnowledgeDecay {
  topic: string;
  totalDecay: number;
  instances: number;
}

export interface PreferenceSummary {
  name: string;
  weight: number;
  strength: number;
  count: number;
}
