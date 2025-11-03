import { Machine } from './machine.model';

export interface MachineGroup {
  id: string;
  name: string;
  machines?: Machine[];
  groupIds?: string[];
  createdUtc?: string;
}

export interface CreateMachineGroupRequest {
  name: string;
}

export interface UpdateMachineGroupRequest extends CreateMachineGroupRequest {
  id: string;
}

export interface AddMachineToGroupRequest {
  groupId: string;
  machineId: string;
}

export interface RemoveMachineFromGroupRequest {
  groupId: string;
  machineId: string;
}
