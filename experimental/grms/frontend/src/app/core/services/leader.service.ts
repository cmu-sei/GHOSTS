import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ConfigService } from './config.service';
import { LeaderDecisionContext, LeaderProfile } from '../models/leader.model';

@Injectable({ providedIn: 'root' })
export class LeaderService {
  private readonly http = inject(HttpClient);
  private readonly config = inject(ConfigService);

  private get baseUrl(): string {
    return `${this.config.apiUrl}/api/v1/leaders`;
  }

  getAll(): Observable<LeaderProfile[]> {
    return this.http.get<LeaderProfile[]>(this.baseUrl);
  }

  getById(id: string): Observable<LeaderProfile> {
    return this.http.get<LeaderProfile>(`${this.baseUrl}/${id}`);
  }

  create(profile: Partial<LeaderProfile>): Observable<LeaderProfile> {
    return this.http.post<LeaderProfile>(this.baseUrl, profile);
  }

  updateContext(id: string, context: LeaderDecisionContext): Observable<LeaderProfile> {
    return this.http.put<LeaderProfile>(`${this.baseUrl}/${id}/context`, context);
  }
}
