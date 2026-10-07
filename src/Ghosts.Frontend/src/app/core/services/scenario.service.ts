import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Scenario, CreateScenario, ApprovedScenarioDocument } from '../models/scenario.model';
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

  /**
   * The scenario as a scenario document: the one it was imported from until its rows are edited, then one
   * derived from the rows. JSON text, as GET api/scenarios/{id}/document returns it.
   */
  getScenarioDocument(id: number): Observable<string> {
    return this.http.get(`${this.apiUrl}/${id}/document`, { responseType: 'text' });
  }

  /** The document as an exercise plan in Markdown, rendered by the server (GET api/scenarios/{id}/document/plan). */
  getScenarioPlan(id: number): Observable<string> {
    return this.http.get(`${this.apiUrl}/${id}/document/plan`, { responseType: 'text' });
  }

  /** The approved version (A5). 404 when the scenario was never imported from a document. */
  getApprovedDocument(id: number): Observable<ApprovedScenarioDocument> {
    return this.http.get<ApprovedScenarioDocument>(`${this.apiUrl}/${id}/document/approved`);
  }

  /** Makes a draft visible to everyone and deployable. Only its author may, and only when it validates with 0 errors. */
  publishScenario(id: number): Observable<Scenario> {
    return this.http.post<Scenario>(`${this.apiUrl}/${id}/publish`, {});
  }
}
