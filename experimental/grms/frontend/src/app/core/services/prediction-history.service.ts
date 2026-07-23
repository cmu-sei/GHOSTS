import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ConfigService } from './config.service';
import { KnownOutcome, SavedPrediction } from '../models/prediction-history.model';

@Injectable({ providedIn: 'root' })
export class PredictionHistoryService {
  private readonly http = inject(HttpClient);
  private readonly config = inject(ConfigService);

  private get baseUrl(): string {
    return `${this.config.apiUrl}/api/v1/predictions`;
  }

  getAll(): Observable<SavedPrediction[]> {
    return this.http.get<SavedPrediction[]>(this.baseUrl);
  }

  getScored(leaderName?: string): Observable<SavedPrediction[]> {
    const params: Record<string, string> = {};
    if (leaderName) params['leader_name'] = leaderName;
    return this.http.get<SavedPrediction[]>(`${this.baseUrl}/scored`, { params });
  }

  getById(id: string): Observable<SavedPrediction> {
    return this.http.get<SavedPrediction>(`${this.baseUrl}/${id}`);
  }

  save(prediction: SavedPrediction): Observable<SavedPrediction> {
    return this.http.post<SavedPrediction>(this.baseUrl, prediction);
  }

  score(id: string, knownOutcome: KnownOutcome): Observable<SavedPrediction> {
    return this.http.post<SavedPrediction>(
      `${this.baseUrl}/${id}/score`,
      { known_outcome: knownOutcome }
    );
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
