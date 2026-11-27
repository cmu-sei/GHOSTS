import { Machine } from './machine.model';

export interface GroupMachine {
  id?: number;
  groupId?: number;
  machineId: string;
}

// Mirrors Ghosts.Api Group: the API has no nested groups or created time
export interface MachineGroup {
  id: string;
  name: string;
  machines?: Machine[];
  groupMachines?: GroupMachine[];
  status?: string;
}

export interface CreateMachineGroupRequest {
  name: string;
  groupMachines?: GroupMachine[];
}

/** PUT replaces the group's whole machine list, so always send groupMachines */
export interface UpdateMachineGroupRequest extends CreateMachineGroupRequest {
  id: string;
  status?: string;
  groupMachines: GroupMachine[];
}

export interface AddMachineToGroupRequest {
  groupId: string;
  machineId: string;
}

export interface RemoveMachineFromGroupRequest {
  groupId: string;
  machineId: string;
}
