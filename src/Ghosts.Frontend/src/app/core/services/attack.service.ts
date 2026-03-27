import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ConfigService } from './config.service';
import {
  AttackTechnique, AttackTechniqueSummary,
  AttackGroup, AttackGroupSummary
} from '../models/scenario-builder.model';

@Injectable({ providedIn: 'root' })
export class AttackService {
  private readonly http = inject(HttpClient);
  private readonly config = inject(ConfigService);

  private get baseUrl(): string {
    return `${this.config.apiUrl}/attack`;
  }

  searchTechniques(query?: string, tactic?: string): Observable<AttackTechniqueSummary[]> {
    const params: string[] = [];
    if (query) params.push(`q=${encodeURIComponent(query)}`);
    if (tactic) params.push(`tactic=${encodeURIComponent(tactic)}`);
    const qs = params.length ? `?${params.join('&')}` : '';
    return this.http.get<AttackTechniqueSummary[]>(`${this.baseUrl}/techniques${qs}`);
  }

  getTechnique(id: string): Observable<AttackTechnique> {
    return this.http.get<AttackTechnique>(`${this.baseUrl}/techniques/${id}`);
  }

  searchGroups(query?: string): Observable<AttackGroupSummary[]> {
    const qs = query ? `?q=${encodeURIComponent(query)}` : '';
    return this.http.get<AttackGroupSummary[]>(`${this.baseUrl}/groups${qs}`);
  }

  getGroup(id: string): Observable<AttackGroup> {
    return this.http.get<AttackGroup>(`${this.baseUrl}/groups/${id}`);
  }

  importData(): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.baseUrl}/import`, {});
  }
}
