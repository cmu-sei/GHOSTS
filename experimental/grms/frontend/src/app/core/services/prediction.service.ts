import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ConfigService } from './config.service';
import { GeopoliticalEvent } from '../models/event.model';
import { CombinedResponse, LeaderResponse, PopulationResponse } from '../models/response.model';

@Injectable({ providedIn: 'root' })
export class PredictionService {
  private readonly http = inject(HttpClient);
  private readonly config = inject(ConfigService);

  predictLeader(leaderId: string, event: GeopoliticalEvent): Observable<LeaderResponse> {
    return this.http.post<LeaderResponse>(
      `${this.config.apiUrl}/api/v1/predict/leader`,
      { leader_id: leaderId, event }
    );
  }

  predictPopulation(country: string, event: GeopoliticalEvent, leaderActionSummary?: string): Observable<PopulationResponse> {
    return this.http.post<PopulationResponse>(
      `${this.config.apiUrl}/api/v1/predict/population`,
      { country, event, leader_action_summary: leaderActionSummary }
    );
  }

  predictCombined(leaderId: string, country: string, event: GeopoliticalEvent): Observable<CombinedResponse> {
    return this.http.post<CombinedResponse>(
      `${this.config.apiUrl}/api/v1/predict/combined`,
      { leader_id: leaderId, country, event }
    );
  }
}
