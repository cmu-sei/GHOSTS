import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ConfigService } from './config.service';
import {
  Execution,
  ExecutionSummary,
  ExecutionEvent,
  ExecutionMetricSnapshot,
  CreateExecutionRequest,
  UpdateExecutionRequest,
  CreateExecutionEventRequest,
} from '../models/execution.model';

@Injectable({
  providedIn: 'root',
})
export class ExecutionService {
  private readonly http = inject(HttpClient);
  private readonly configService = inject(ConfigService);

  private get apiUrl(): string {
    return `${this.configService.apiUrl}/executions`;
  }

  // Get all executions or filter by scenario
  getExecutions(scenarioId?: number): Observable<ExecutionSummary[]> {
    let params = new HttpParams();
    if (scenarioId !== undefined) {
      params = params.set('scenarioId', scenarioId.toString());
    }
    return this.http.get<ExecutionSummary[]>(this.apiUrl, { params });
  }

  // Get single execution by ID
  getExecution(id: number): Observable<Execution> {
    return this.http.get<Execution>(`${this.apiUrl}/${id}`);
  }

  // Get execution events
  getExecutionEvents(
    id: number,
    eventType?: string,
    limit?: number
  ): Observable<ExecutionEvent[]> {
    let params = new HttpParams();
    if (eventType) {
      params = params.set('eventType', eventType);
    }
    if (limit !== undefined) {
      params = params.set('limit', limit.toString());
    }
    return this.http.get<ExecutionEvent[]>(`${this.apiUrl}/${id}/events`, {
      params,
    });
  }

  // Get execution metric snapshots
  getExecutionMetrics(
    id: number,
    limit?: number
  ): Observable<ExecutionMetricSnapshot[]> {
    let params = new HttpParams();
    if (limit !== undefined) {
      params = params.set('limit', limit.toString());
    }
    return this.http.get<ExecutionMetricSnapshot[]>(
      `${this.apiUrl}/${id}/metrics`,
      { params }
    );
  }

  // Create new execution
  createExecution(
    request: CreateExecutionRequest
  ): Observable<Execution> {
    return this.http.post<Execution>(this.apiUrl, request);
  }

  // Update execution
  updateExecution(
    id: number,
    request: UpdateExecutionRequest
  ): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${id}`, request);
  }

  // Delete execution
  deleteExecution(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  // Lifecycle operations
  startExecution(id: number): Observable<Execution> {
    return this.http.post<Execution>(`${this.apiUrl}/${id}/start`, {});
  }

  pauseExecution(id: number): Observable<Execution> {
    return this.http.post<Execution>(`${this.apiUrl}/${id}/pause`, {});
  }

  stopExecution(id: number, failed = false): Observable<Execution> {
    return this.http.post<Execution>(`${this.apiUrl}/${id}/stop`, null, {
      params: { failed: failed.toString() },
    });
  }

  cancelExecution(id: number): Observable<Execution> {
    return this.http.post<Execution>(`${this.apiUrl}/${id}/cancel`, {});
  }

  // Add event to execution
  addExecutionEvent(
    id: number,
    request: CreateExecutionEventRequest
  ): Observable<ExecutionEvent> {
    return this.http.post<ExecutionEvent>(
      `${this.apiUrl}/${id}/events`,
      request
    );
  }

  // Add metric snapshot
  addMetricSnapshot(id: number, metrics: any): Observable<ExecutionMetricSnapshot> {
    return this.http.post<ExecutionMetricSnapshot>(
      `${this.apiUrl}/${id}/metrics`,
      metrics
    );
  }

  // Update current execution metrics
  updateExecutionMetrics(id: number, metrics: any): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${id}/metrics`, metrics);
  }

  // Set execution error
  setExecutionError(id: number, error: any): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${id}/error`, error);
  }
}
