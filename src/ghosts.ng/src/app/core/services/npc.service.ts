import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Npc, CreateNpcRequest, GenerateNpcRequest } from '../models';
import { ConfigService } from './config.service';

@Injectable({
  providedIn: 'root'
})
export class NpcService {
  private readonly http = inject(HttpClient);
  private readonly config = inject(ConfigService);

  private get apiUrl(): string {
    return `${this.config.apiUrl}/npcs`;
  }

  getNpcs(): Observable<Npc[]> {
    return this.http.get<Npc[]>(this.apiUrl);
  }

  getNpc(id: string): Observable<Npc> {
    return this.http.get<Npc>(`${this.apiUrl}/${id}`);
  }

  createNpc(request: CreateNpcRequest): Observable<Npc> {
    return this.http.post<Npc>(this.apiUrl, request);
  }

  deleteNpc(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  generateNpc(): Observable<Npc> {
    return this.http.post<Npc>(`${this.apiUrl}/generateone`, {});
  }

  generateNpcs(request: GenerateNpcRequest): Observable<Npc[]> {
    // Transform the simple request into the API's expected GenerationConfiguration format
    const config = {
      campaign: request.campaign,
      scenarioId: request.scenarioId,
      enclaves: [
        {
          name: request.enclave,
          teams: [
            {
              name: request.team,
              npcs: {
                number: request.number,
                configuration: {}
              }
            }
          ]
        }
      ]
    };

    return this.http.post<Npc[]>(`${this.config.apiUrl}/npcsgenerate`, config);
  }

  getNpcsByCampaignEnclave(campaign: string, enclave: string): Observable<Npc[]> {
    return this.http.get<Npc[]>(`${this.apiUrl}/${campaign}/${enclave}`);
  }

  getNpcsByCampaignEnclaveTeam(campaign: string, enclave: string, team: string): Observable<Npc[]> {
    return this.http.get<Npc[]>(`${this.apiUrl}/${campaign}/${enclave}/${team}`);
  }

  exportToCSV(campaign: string, enclave: string): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/${campaign}/${enclave}/csv`, {
      responseType: 'blob'
    });
  }

  getNpcPhoto(id: string): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/${id}/photo`, {
      responseType: 'blob'
    });
  }
}
