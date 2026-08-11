import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { NpcChatConfig, NpcChatRequest, NpcChatResponse } from '../models';
import { ConfigService } from './config.service';

@Injectable({
  providedIn: 'root'
})
export class NpcChatService {
  private readonly http = inject(HttpClient);
  private readonly config = inject(ConfigService);

  private get apiUrl(): string {
    return `${this.config.apiUrl}/npcchat`;
  }

  getConfig(): Observable<NpcChatConfig> {
    return this.http.get<NpcChatConfig>(`${this.apiUrl}/config`);
  }

  chat(npcId: string, request: NpcChatRequest): Observable<NpcChatResponse> {
    return this.http.post<NpcChatResponse>(`${this.apiUrl}/${npcId}`, request);
  }
}
