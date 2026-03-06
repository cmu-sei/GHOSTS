export enum AnimationJobTypes {
  SOCIALGRAPH = 'SOCIALGRAPH',
  SOCIALSHARING = 'SOCIALSHARING',
  SOCIALBELIEF = 'SOCIALBELIEF',
  CHAT = 'CHAT',
  FULLAUTONOMY = 'FULLAUTONOMY'
}

export interface JobInfo {
  id: string;
  name: string;
  startTime: string;
}

export interface AnimationConfiguration {
  jobId: string;
  jobConfiguration: string;
}

export interface AnimationSettings {
  isEnabled: boolean;
  isMultiThreaded: boolean;
  isInteracting: boolean;
  turnLength: number;
  maximumSteps: number;
}

export interface SocialGraphSettings extends AnimationSettings {
  chanceOfKnowledgeTransfer: number;
  decay?: {
    isEnabled: boolean;
    rate: number;
  };
}

export interface ChatSettings extends AnimationSettings {
  isSendingTimelinesToGhostsApi: boolean;
  percentReplyVsNew: number;
  postProbabilities?: { [key: string]: number };
  postUrl?: string;
  contentEngine?: ContentEngineSettings;
}

export interface SocialSharingSettings extends AnimationSettings {
  isSendingTimelinesToGhostsApi: boolean;
  isSendingTimelinesDirectToSocializer: boolean;
  postUrl?: string;
  contentEngine?: ContentEngineSettings;
}

export interface SocialBeliefSettings extends AnimationSettings {
  // No additional properties beyond base AnimationSettings
}

export interface FullAutonomySettings extends AnimationSettings {
  isSendingTimelinesToGhostsApi: boolean;
  contentEngine?: ContentEngineSettings;
}

export interface ContentEngineSettings {
  source: string;
  model?: string;
  apiKey?: string;
  host?: string;
  temperature?: number;
}

export interface AllAnimationSettings {
  socialGraph?: SocialGraphSettings;
  chat?: ChatSettings;
  socialSharing?: SocialSharingSettings;
  socialBelief?: SocialBeliefSettings;
  fullAutonomy?: FullAutonomySettings;
}

export interface AnimationStartRequest {
  jobId: AnimationJobTypes;
  jobConfiguration: string;
}

export interface AnimationStopRequest {
  jobId: string;
}
