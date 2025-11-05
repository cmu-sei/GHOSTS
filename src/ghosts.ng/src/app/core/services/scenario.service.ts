import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Scenario, CreateScenario } from '../models';
import { ConfigService } from './config.service';

@Injectable({
  providedIn: 'root'
})
export class ScenarioService {
  private readonly http = inject(HttpClient);
  private readonly config = inject(ConfigService);

  private get apiUrl(): string {
    return `${this.config.apiUrl}/scenarios`;
  }

  getScenarios(): Observable<Scenario[]> {
    return this.http.get<Scenario[]>(this.apiUrl);
  }

  getScenario(id: number): Observable<Scenario> {
    return this.http.get<Scenario>(`${this.apiUrl}/${id}`);
  }

  createScenario(scenario: CreateScenario): Observable<Scenario> {
    return this.http.post<Scenario>(this.apiUrl, scenario);
  }

  updateScenario(id: number, scenario: CreateScenario): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${id}`, scenario);
  }

  deleteScenario(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  startScenario(id: number): Observable<Scenario> {
    return this.http.post<Scenario>(`${this.apiUrl}/${id}/start`, {});
  }
}
