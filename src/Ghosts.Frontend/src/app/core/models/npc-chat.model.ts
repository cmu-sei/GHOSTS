export type NpcChatMode = 'as' | 'about';

export interface NpcChatMessage {
  role: 'user' | 'npc';
  content: string;
}

export interface NpcChatAction {
  tool: string;
  argument?: string;
  ok: boolean;
  detail?: string;
}

export interface NpcChatRequest {
  message: string;
  history: NpcChatMessage[];
  mode: NpcChatMode;
  model?: string;
}

export interface NpcChatResponse {
  reply: string;
  mode: string;
  model: string;
  actions?: NpcChatAction[];
}

export interface NpcChatConfig {
  source: string;
  host: string;
  model: string;
  availableModels: string[];
  isReachable: boolean;
  error?: string;
}
