import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Objective, CreateObjective, UpdateObjective } from '../models/objective.model';
import { ConfigService } from './config.service';

@Injectable({
  providedIn: 'root'
})
export class ObjectiveService {
  private readonly http = inject(HttpClient);
  private readonly config = inject(ConfigService);

  private get apiUrl(): string {
    return `${this.config.apiUrl}/objectives`;
  }

  getAll(): Observable<Objective[]> {
    return this.http.get<Objective[]>(this.apiUrl);
  }

  getByScenario(scenarioId: number): Observable<Objective[]> {
    const params = new HttpParams().set('scenarioId', scenarioId);
    return this.http.get<Objective[]>(this.apiUrl, { params });
  }

  getById(id: number): Observable<Objective> {
    return this.http.get<Objective>(`${this.apiUrl}/${id}`);
  }

  create(objective: CreateObjective): Observable<Objective> {
    return this.http.post<Objective>(this.apiUrl, objective);
  }

  update(id: number, objective: UpdateObjective): Observable<Objective> {
    return this.http.put<Objective>(`${this.apiUrl}/${id}`, objective);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }
}
