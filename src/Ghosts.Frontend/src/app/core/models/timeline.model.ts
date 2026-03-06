export type TimelineStatus = 'Run' | 'Stop';

export type TimelineScheduleType = 'Other' | 'Cron';

export interface TimelineEvent {
  trackableId?: string | null;
  command?: string | null;
  commandArgs?: unknown[] | null;
  delayAfter?: number | null;
  delayBefore?: number | null;
}

export interface TimelineHandler {
  handlerType: string;
  initial?: string | null;
  utcTimeOn?: string;
  utcTimeOff?: string;
  handlerArgs?: Record<string, unknown> | null;
  loop?: boolean;
  timeLineEvents?: TimelineEvent[] | null;
  scheduleType?: TimelineScheduleType;
  schedule?: string | null;
}

export interface Timeline {
  id?: string;
  name: string;
  status?: TimelineStatus;
  timeLineHandlers: TimelineHandler[];
}

export interface PostTimelineRequest {
  machineId?: string;
  groupId?: string;
  timeline: Timeline;
  updateType?: UpdateType;
}

export enum UpdateType {
  Timeline = 'Timeline',
  TimelinePartial = 'TimelinePartial',
  TimelineHandler = 'TimelineHandler'
}

export interface LocalTimeline extends Timeline {
  id: string;
}

export type CreateLocalTimelineRequest = Omit<Timeline, 'id' | 'status'>;
