import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  Activity,
  ActivityQueryParams,
  NpcActivity,
  NpcRecord,
  NpcNameId,
  AiActionRequest
} from '../models';
import { ConfigService } from './config.service';

@Injectable({
  providedIn: 'root'
})
export class ActivityService {
  private readonly http = inject(HttpClient);
  private readonly configService = inject(ConfigService);

  private get apiUrl(): string {
    return this.configService.apiUrl;
  }

  // Machine Activities
  getActivities(params: ActivityQueryParams): Observable<Activity[]> {
    const queryParams: string[] = [];

    if (params.skip !== undefined) queryParams.push(`skip=${params.skip}`);
    if (params.take !== undefined) queryParams.push(`take=${params.take}`);

    const query = queryParams.length > 0 ? `?${queryParams.join('&')}` : '';

    if (params.machineId) {
      return this.http.get<Activity[]>(`${this.apiUrl}/machines/${params.machineId}/activity${query}`);
    } else if (params.groupId) {
      return this.http.get<Activity[]>(`${this.apiUrl}/machinegroups/${params.groupId}/activity${query}`);
    }

    throw new Error('Either machineId or groupId must be provided');
  }

  // NPC Activities
  getAllNpcs(): Observable<NpcRecord[]> {
    return this.http.get<NpcRecord[]>(`${this.apiUrl}/npcs`);
  }

  getNpcList(): Observable<NpcNameId[]> {
    return this.http.get<NpcNameId[]>(`${this.apiUrl}/npcs/list`);
  }

  getNpcById(id: string): Observable<NpcRecord> {
    return this.http.get<NpcRecord>(`${this.apiUrl}/npcs/${id}`);
  }

  getNpcsByEnclave(campaign: string, enclave: string): Observable<NpcRecord[]> {
    return this.http.get<NpcRecord[]>(`${this.apiUrl}/npcs/${campaign}/${enclave}`);
  }

  getNpcsByTeam(campaign: string, enclave: string, team: string): Observable<NpcRecord[]> {
    return this.http.get<NpcRecord[]>(`${this.apiUrl}/npcs/${campaign}/${enclave}/${team}`);
  }

  createNpc(npcProfile: any): Observable<NpcRecord> {
    return this.http.post<NpcRecord>(`${this.apiUrl}/npcs`, npcProfile);
  }

  deleteNpc(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/npcs/${id}`);
  }

  deleteNpcsByEnclave(campaign: string, enclave: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/npcs/${campaign}/${enclave}`);
  }

  sendNpcCommand(request: AiActionRequest): Observable<any> {
    return this.http.post(`${this.apiUrl}/npcs/command`, request);
  }

  getNpcActivities(skip?: number, take?: number): Observable<NpcActivity[]> {
    const params: string[] = [];
    if (skip !== undefined) params.push(`skip=${skip}`);
    if (take !== undefined) params.push(`take=${take}`);
    const query = params.length > 0 ? `?${params.join('&')}` : '';
    return this.http.get<NpcActivity[]>(`${this.apiUrl}/npc-activities${query}`);
  }
}
