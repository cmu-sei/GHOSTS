import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ConfigService } from './config.service';
import {
  AuthoringSession, AuthoringSessionSummary, AuthoringSessionRecord, AuthoringModels,
  AuthoringDocumentDetail, AuthoringChunk,
  StartAuthoringSessionRequest, AuthoringTurnRequest, AuthoringImportRequest, AuthoringImportResult,
} from '../models/scenario-authoring.model';

@Injectable({ providedIn: 'root' })
export class ScenarioAuthoringService {
  private readonly http = inject(HttpClient);
  private readonly config = inject(ConfigService);

  private get apiUrl(): string {
    return `${this.config.apiUrl}/scenario-authoring`;
  }

  getModels(): Observable<AuthoringModels> {
    return this.http.get<AuthoringModels>(`${this.apiUrl}/models`);
  }

  startSession(scenarioId: number | null, model: string): Observable<AuthoringSession> {
    const body: StartAuthoringSessionRequest = { scenarioId, model };
    return this.http.post<AuthoringSession>(`${this.apiUrl}/sessions`, body);
  }

  getSessions(scenarioId: number): Observable<AuthoringSessionSummary[]> {
    return this.http.get<AuthoringSessionSummary[]>(`${this.apiUrl}/sessions`, { params: { scenarioId } });
  }

  getSession(id: string): Observable<AuthoringSessionRecord> {
    return this.http.get<AuthoringSessionRecord>(`${this.apiUrl}/sessions/${id}`);
  }

  /** Starts a turn in the background; the result arrives over the hub (authoringProgress → turn-ended) and on reload. */
  runTurn(id: string, message: string): Observable<{ sessionId: string }> {
    const body: AuthoringTurnRequest = { message };
    return this.http.post<{ sessionId: string }>(`${this.apiUrl}/sessions/${id}/turns`, body);
  }

  /** Returns the document and records that the developer was shown it (A3). */
  getDocument(id: string, hash: string): Observable<AuthoringDocumentDetail> {
    return this.http.get<AuthoringDocumentDetail>(`${this.apiUrl}/sessions/${id}/documents/${hash}`);
  }

  getChunk(id: string, chunkId: number): Observable<AuthoringChunk> {
    return this.http.get<AuthoringChunk>(`${this.apiUrl}/sessions/${id}/chunks/${chunkId}`);
  }

  import(id: string, hash: string, again = false, replace = false): Observable<AuthoringImportResult> {
    const body: AuthoringImportRequest = { hash, again, replace };
    return this.http.post<AuthoringImportResult>(`${this.apiUrl}/sessions/${id}/import`, body);
  }
}
