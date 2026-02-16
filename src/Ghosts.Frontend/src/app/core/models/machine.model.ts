export enum MachineStatus {
  Up = 'Up',
  Down = 'Down',
  UpWithErrors = 'UpWithErrors',
  DownWithErrors = 'DownWithErrors',
  Unknown = 'Unknown'
}

export interface Machine {
  id: string;
  name: string;
  status?: MachineStatus;
  statusUp?: StatusType;
  fQDN?: string;
  host?: string;
  iPAddress?: string;
  domain?: string;
  currentUsername?: string;
  clientVersion?: string;
  history?: MachineHistoryItem[];
  createdUtc?: string;
  lastReportedUtc?: string;
}

export interface StatusType {
  status?: MachineStatus;
  statusColor?: string;
  activity?: string;
  cpuUsage?: number;
  memoryUsage?: number;
  errors?: string[];
  assets?: string[];
  internet?: boolean;
}

export interface MachineHistoryItem {
  id?: string;
  machineId?: string;
  type?: HistoryType;
  history?: string;
  createdUtc?: string;
}

export enum HistoryType {
  Activity = 0,
  Timeline = 1,
  Health = 2
}

export interface CreateMachineRequest {
  name: string;
  fQDN?: string;
  host?: string;
  iPAddress?: string;
  domain?: string;
}

export interface UpdateMachineRequest extends CreateMachineRequest {
  id: string;
}
