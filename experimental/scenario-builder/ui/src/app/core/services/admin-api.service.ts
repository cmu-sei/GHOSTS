import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import {
  ScenarioType, ScenarioTypeAdminSummary, ScenarioTypeDetail, ScenarioTypeVersionView, LibraryDetail, LibraryDraft, LibrarySummary, PreviewRequest, VersionView
} from '../models/builder.model';

/** What a new scenario type starts from: its name, what it is, and the scenario type it copies, if any. */
export interface NewScenarioType {
  name: string;
  description: string;
  topology: boolean;
  basedOn?: string;
}

/** The admin API: template libraries, scenario types and the template preview. The API refuses anyone not listed as an admin. */
@Injectable({ providedIn: 'root' })
export class AdminApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/admin';

  libraries(): Observable<LibrarySummary[]> {
    return this.http.get<LibrarySummary[]>(`${this.base}/libraries`);
  }

  create(name: string, description: string, scenarioTypes: string[]): Observable<LibraryDetail> {
    return this.http.post<LibraryDetail>(`${this.base}/libraries`, { name, description, scenarioTypes });
  }

  library(id: number): Observable<LibraryDetail> {
    return this.http.get<LibraryDetail>(`${this.base}/libraries/${id}`);
  }

  save(id: number, library: LibraryDraft): Observable<LibraryDetail> {
    return this.http.put<LibraryDetail>(`${this.base}/libraries/${id}`, library);
  }

  publish(id: number): Observable<VersionView> {
    return this.http.post<VersionView>(`${this.base}/libraries/${id}/publish`, null);
  }

  /** The latest published version as a .library.json download. */
  exportUrl(id: number): string {
    return `${this.base}/libraries/${id}/export`;
  }

  /** A .library.json file's text, sent as it is, so the API can say what is wrong with a file that isn't one. */
  import(file: string): Observable<LibraryDetail> {
    return this.http.post<LibraryDetail>(`${this.base}/libraries/import`, file, { headers: new HttpHeaders({ 'Content-Type': 'application/json' }) });
  }

  scenarioTypes(): Observable<ScenarioTypeAdminSummary[]> {
    return this.http.get<ScenarioTypeAdminSummary[]>(`${this.base}/scenario-types`);
  }

  createScenarioType(scenarioType: NewScenarioType): Observable<ScenarioTypeDetail> {
    return this.http.post<ScenarioTypeDetail>(`${this.base}/scenario-types`, scenarioType);
  }

  scenarioType(id: string): Observable<ScenarioTypeDetail> {
    return this.http.get<ScenarioTypeDetail>(`${this.base}/scenario-types/${encodeURIComponent(id)}`);
  }

  saveScenarioType(id: string, scenarioType: ScenarioType): Observable<ScenarioTypeDetail> {
    return this.http.put<ScenarioTypeDetail>(`${this.base}/scenario-types/${encodeURIComponent(id)}`, scenarioType);
  }

  publishScenarioType(id: string): Observable<ScenarioTypeVersionView> {
    return this.http.post<ScenarioTypeVersionView>(`${this.base}/scenario-types/${encodeURIComponent(id)}/publish`, null);
  }

  retireScenarioType(id: string, retired: boolean): Observable<ScenarioTypeDetail> {
    return this.http.put<ScenarioTypeDetail>(`${this.base}/scenario-types/${encodeURIComponent(id)}/retired`, { retired });
  }

  deleteScenarioType(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/scenario-types/${encodeURIComponent(id)}`);
  }

  /** The latest published version as a .scenario type.json download. */
  scenarioTypeExportUrl(id: string): string {
    return `${this.base}/scenario-types/${encodeURIComponent(id)}/export`;
  }

  /** A .scenario type.json file's text, sent as it is, so the API can say what is wrong with a file that isn't one. */
  importScenarioType(file: string): Observable<ScenarioTypeDetail> {
    return this.http.post<ScenarioTypeDetail>(`${this.base}/scenario-types/import`, file, { headers: new HttpHeaders({ 'Content-Type': 'application/json' }) });
  }

  /** The model plans the draft's stages, replacing the ones it had. */
  draftStages(id: string, model: string): Observable<ScenarioTypeDetail> {
    return this.http.post<ScenarioTypeDetail>(`${this.base}/scenario-types/${encodeURIComponent(id)}/draft/stages`, { model });
  }

  /** The model writes one stage's questions, replacing the ones it had. */
  draftStage(id: string, stage: number, model: string): Observable<ScenarioTypeDetail> {
    return this.http.post<ScenarioTypeDetail>(`${this.base}/scenario-types/${encodeURIComponent(id)}/draft/stages/${stage}`, { model });
  }

  /** The models the API can reach, by name. */
  models(): Observable<string[]> {
    return this.http.get<{ data: { id: string }[] }>('/api/model/models').pipe(map(list => list.data.map(m => m.id)));
  }

  /** The rendered page, as its audience would get it. */
  preview(request: PreviewRequest): Observable<string> {
    return this.http.post(`${this.base}/preview`, request, { responseType: 'text' });
  }
}
