import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, tap } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import {
  N8nWorkflow,
  N8nWorkflowListResponse,
  N8nWorkflowRunResponse
} from '../models';

@Injectable({
  providedIn: 'root'
})
export class N8nWorkflowService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/animations/workflows`;
  private activeWorkflowsCache: N8nWorkflow[] | null = null;

  getActiveWorkflows(forceRefresh = false): Observable<N8nWorkflow[]> {
    if (!forceRefresh && this.activeWorkflowsCache) {
      return of(this.activeWorkflowsCache);
    }

    const params = forceRefresh ? new HttpParams().set('forceRefresh', 'true') : undefined;

    return this.http.get<N8nWorkflowListResponse | N8nWorkflow[]>(
      this.apiUrl,
      { params }
    ).pipe(
      map(response => this.toActiveWorkflows(response)),
      tap(workflows => {
        this.activeWorkflowsCache = workflows;
      })
    );
  }

  runWorkflow(workflowId: string | number, cronSchedule: string): Observable<N8nWorkflowRunResponse> {
    return this.http.post<N8nWorkflowRunResponse>(
      this.apiUrl,
      { workflowId, cronSchedule }
    );
  }

  private toActiveWorkflows(response: N8nWorkflowListResponse | N8nWorkflow[] | null | undefined): N8nWorkflow[] {
    if (!response) {
      return [];
    }

    const workflows = Array.isArray(response) ? response : (response.data || []);
    return workflows.filter(workflow => workflow.active);
  }
}
