import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ConfigService } from './config.service';
import {
  ScenarioSource, ScenarioSourceChunk, ScenarioEntity, ScenarioEdge,
  ScenarioGraph, ScenarioEnrichment, ScenarioCompilation, ExtractionResult,
  CreateTextSource, CreateUrlSource, CreateEntity, UpdateEntity, CreateEdge,
  ApplyTechnique, ApplyGroup, CompileRequest,
  AssistantRequest, AssistantResponse
} from '../models/scenario-builder.model';

@Injectable({ providedIn: 'root' })
export class ScenarioBuilderService {
  private readonly http = inject(HttpClient);
  private readonly config = inject(ConfigService);

  private builderUrl(scenarioId: number): string {
    return `${this.config.apiUrl}/scenarios/${scenarioId}/builder`;
  }

  // ── Sources ──

  getSources(scenarioId: number): Observable<ScenarioSource[]> {
    return this.http.get<ScenarioSource[]>(`${this.builderUrl(scenarioId)}/sources`);
  }

  getSource(scenarioId: number, sourceId: number): Observable<ScenarioSource> {
    return this.http.get<ScenarioSource>(`${this.builderUrl(scenarioId)}/sources/${sourceId}`);
  }

  addText(scenarioId: number, dto: CreateTextSource): Observable<ScenarioSource> {
    return this.http.post<ScenarioSource>(`${this.builderUrl(scenarioId)}/sources/text`, dto);
  }

  addUrl(scenarioId: number, dto: CreateUrlSource): Observable<ScenarioSource> {
    return this.http.post<ScenarioSource>(`${this.builderUrl(scenarioId)}/sources/url`, dto);
  }

  uploadFile(scenarioId: number, file: File): Observable<ScenarioSource> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<ScenarioSource>(`${this.builderUrl(scenarioId)}/sources/file`, formData);
  }

  deleteSource(scenarioId: number, sourceId: number): Observable<void> {
    return this.http.delete<void>(`${this.builderUrl(scenarioId)}/sources/${sourceId}`);
  }

  getChunks(scenarioId: number, sourceId: number): Observable<ScenarioSourceChunk[]> {
    return this.http.get<ScenarioSourceChunk[]>(`${this.builderUrl(scenarioId)}/sources/${sourceId}/chunks`);
  }

  // ── Extraction ──

  extractAll(scenarioId: number): Observable<ExtractionResult> {
    return this.http.post<ExtractionResult>(`${this.builderUrl(scenarioId)}/extract`, {});
  }

  extractChunk(scenarioId: number, chunkId: number): Observable<ExtractionResult> {
    return this.http.post<ExtractionResult>(`${this.builderUrl(scenarioId)}/extract/${chunkId}`, {});
  }

  // ── Graph ──

  getGraph(scenarioId: number): Observable<ScenarioGraph> {
    return this.http.get<ScenarioGraph>(`${this.builderUrl(scenarioId)}/graph`);
  }

  getGraphStats(scenarioId: number): Observable<Record<string, number>> {
    return this.http.get<Record<string, number>>(`${this.builderUrl(scenarioId)}/graph/stats`);
  }

  // ── Entities ──

  getEntities(scenarioId: number, type?: string): Observable<ScenarioEntity[]> {
    const params = type ? `?type=${type}` : '';
    return this.http.get<ScenarioEntity[]>(`${this.builderUrl(scenarioId)}/entities${params}`);
  }

  getEntity(scenarioId: number, entityId: string): Observable<ScenarioEntity> {
    return this.http.get<ScenarioEntity>(`${this.builderUrl(scenarioId)}/entities/${entityId}`);
  }

  createEntity(scenarioId: number, dto: CreateEntity): Observable<ScenarioEntity> {
    return this.http.post<ScenarioEntity>(`${this.builderUrl(scenarioId)}/entities`, dto);
  }

  updateEntity(scenarioId: number, entityId: string, dto: UpdateEntity): Observable<ScenarioEntity> {
    return this.http.put<ScenarioEntity>(`${this.builderUrl(scenarioId)}/entities/${entityId}`, dto);
  }

  deleteEntity(scenarioId: number, entityId: string): Observable<void> {
    return this.http.delete<void>(`${this.builderUrl(scenarioId)}/entities/${entityId}`);
  }

  mergeEntities(scenarioId: number, keepId: string, mergeId: string): Observable<ScenarioEntity> {
    return this.http.post<ScenarioEntity>(`${this.builderUrl(scenarioId)}/entities/${keepId}/merge/${mergeId}`, {});
  }

  // ── Edges ──

  getEdges(scenarioId: number, type?: string): Observable<ScenarioEdge[]> {
    const params = type ? `?type=${type}` : '';
    return this.http.get<ScenarioEdge[]>(`${this.builderUrl(scenarioId)}/edges${params}`);
  }

  createEdge(scenarioId: number, dto: CreateEdge): Observable<ScenarioEdge> {
    return this.http.post<ScenarioEdge>(`${this.builderUrl(scenarioId)}/edges`, dto);
  }

  deleteEdge(scenarioId: number, edgeId: string): Observable<void> {
    return this.http.delete<void>(`${this.builderUrl(scenarioId)}/edges/${edgeId}`);
  }

  // ── Enrichments ──

  getEnrichments(scenarioId: number): Observable<ScenarioEnrichment[]> {
    return this.http.get<ScenarioEnrichment[]>(`${this.builderUrl(scenarioId)}/enrichments`);
  }

  applyTechnique(scenarioId: number, dto: ApplyTechnique): Observable<ScenarioEnrichment> {
    return this.http.post<ScenarioEnrichment>(`${this.builderUrl(scenarioId)}/enrichments/technique`, dto);
  }

  applyGroup(scenarioId: number, dto: ApplyGroup): Observable<ScenarioEnrichment> {
    return this.http.post<ScenarioEnrichment>(`${this.builderUrl(scenarioId)}/enrichments/group`, dto);
  }

  deleteEnrichment(scenarioId: number, enrichmentId: number): Observable<void> {
    return this.http.delete<void>(`${this.builderUrl(scenarioId)}/enrichments/${enrichmentId}`);
  }

  // ── Compilations ──

  compile(scenarioId: number, dto: CompileRequest): Observable<ScenarioCompilation> {
    return this.http.post<ScenarioCompilation>(`${this.builderUrl(scenarioId)}/compile`, dto);
  }

  getCompilations(scenarioId: number): Observable<ScenarioCompilation[]> {
    return this.http.get<ScenarioCompilation[]>(`${this.builderUrl(scenarioId)}/compilations`);
  }

  getCompilation(scenarioId: number, compilationId: number): Observable<ScenarioCompilation> {
    return this.http.get<ScenarioCompilation>(`${this.builderUrl(scenarioId)}/compilations/${compilationId}`);
  }

  getPackage(scenarioId: number, compilationId: number): Observable<any> {
    return this.http.get<any>(`${this.builderUrl(scenarioId)}/compilations/${compilationId}/package`);
  }

  deleteCompilation(scenarioId: number, compilationId: number): Observable<void> {
    return this.http.delete<void>(`${this.builderUrl(scenarioId)}/compilations/${compilationId}`);
  }

  // ── Assistant ──

  chat(scenarioId: number, request: AssistantRequest): Observable<AssistantResponse> {
    return this.http.post<AssistantResponse>(`${this.builderUrl(scenarioId)}/assistant`, request);
  }
}
