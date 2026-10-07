import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ConfigService } from './config.service';
import {
  ScenarioSource, ScenarioEntity, ScenarioEdge,
  ScenarioGraph, ExtractionResult, BuilderModel,
  CreateTextSource, CreateUrlSource, CreateEntity, UpdateEntity, CreateEdge
} from '../models/scenario-builder.model';

@Injectable({ providedIn: 'root' })
export class ScenarioBuilderService {
  private readonly http = inject(HttpClient);
  private readonly config = inject(ConfigService);

  private builderUrl(scenarioId: number): string {
    return `${this.config.apiUrl}/scenarios/${scenarioId}/builder`;
  }

  // ── The model: one per scenario, for extraction and the conversation ──

  getModel(scenarioId: number): Observable<BuilderModel> {
    return this.http.get<BuilderModel>(`${this.builderUrl(scenarioId)}/model`);
  }

  setModel(scenarioId: number, model: string, effort: string | null): Observable<BuilderModel> {
    return this.http.put<BuilderModel>(`${this.builderUrl(scenarioId)}/model`, { model, effort });
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

  // ── Extraction ──

  extractAll(scenarioId: number): Observable<ExtractionResult> {
    return this.http.post<ExtractionResult>(`${this.builderUrl(scenarioId)}/extract`, {});
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
}
