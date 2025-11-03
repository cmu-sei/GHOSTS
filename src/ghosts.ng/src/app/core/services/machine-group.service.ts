import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  MachineGroup,
  CreateMachineGroupRequest,
  UpdateMachineGroupRequest,
  AddMachineToGroupRequest,
  RemoveMachineFromGroupRequest
} from '../models';

@Injectable({
  providedIn: 'root'
})
export class MachineGroupService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/machinegroups`;

  getMachineGroups(): Observable<MachineGroup[]> {
    return this.http.get<MachineGroup[]>(this.apiUrl);
  }

  getMachineGroup(id: string): Observable<MachineGroup> {
    return this.http.get<MachineGroup>(`${this.apiUrl}/${id}`);
  }

  createMachineGroup(request: CreateMachineGroupRequest): Observable<MachineGroup> {
    return this.http.post<MachineGroup>(this.apiUrl, request);
  }

  updateMachineGroup(id: string, request: UpdateMachineGroupRequest): Observable<MachineGroup> {
    return this.http.put<MachineGroup>(`${this.apiUrl}/${id}`, request);
  }

  deleteMachineGroup(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  addMachineToGroup(request: AddMachineToGroupRequest): Observable<void> {
    return this.http.post<void>(
      `${this.apiUrl}/${request.groupId}/machines/${request.machineId}`,
      {}
    );
  }

  removeMachineFromGroup(request: RemoveMachineFromGroupRequest): Observable<void> {
    return this.http.delete<void>(
      `${this.apiUrl}/${request.groupId}/machines/${request.machineId}`
    );
  }

  getMachineGroupActivity(id: string, skip?: number, take?: number): Observable<any[]> {
    let url = `${this.apiUrl}/${id}/activity`;
    const params: string[] = [];
    if (skip !== undefined) params.push(`skip=${skip}`);
    if (take !== undefined) params.push(`take=${take}`);
    if (params.length > 0) url += `?${params.join('&')}`;
    return this.http.get<any[]>(url);
  }

  getMachineGroupHealth(id: string): Observable<any> {
    return this.http.get<any>(`${this.apiUrl}/${id}/health`);
  }
}
