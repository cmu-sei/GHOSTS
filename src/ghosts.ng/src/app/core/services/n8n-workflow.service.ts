import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, tap } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import {
  N8nWorkflow,
  N8nWorkflowListResponse,
  N8nWorkflowRunResponse,
  WorkflowControl,
  WorkflowTriggerType
} from '../models';

@Injectable({
  providedIn: 'root'
})
export class N8nWorkflowService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/animations/workflows`;
  private readonly n8nBaseUrl = this.buildN8nBaseUrl(environment.n8nApiUrl);
  private readonly webhookBaseUrl = this.buildWebhookBaseUrl(this.n8nBaseUrl);
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

  runWorkflow(workflowId: string | number, cronSchedule: string, webhookUrl: string): Observable<N8nWorkflowRunResponse> {
    return this.http.post<N8nWorkflowRunResponse>(
      this.apiUrl,
      this.toControlPayload(workflowId, cronSchedule, webhookUrl)
    );
  }

  stopWorkflow(workflowId: string | number): Observable<N8nWorkflowRunResponse> {
    return this.http.post<N8nWorkflowRunResponse>(
      `${this.apiUrl}/stop`,
      this.toControlPayload(workflowId)
    );
  }

  private toActiveWorkflows(response: N8nWorkflowListResponse | N8nWorkflow[] | null | undefined): N8nWorkflow[] {
    if (!response) {
      return [];
    }

    const workflows = Array.isArray(response) ? response : (response.data || []);
    return workflows.map(workflow => {
      const triggerType = this.determineTriggerType(workflow);
      const webhookUrl = triggerType === 'webhook'
        ? this.resolveWebhookUrl(workflow)
        : undefined;
      const n8nUrl = this.buildN8nWorkflowUrl(workflow, webhookUrl);

      return {
        ...workflow,
        isRunning: Boolean(workflow.isRunning),
        triggerType,
        webhookUrl,
        n8nUrl
      };
    }).sort((a, b) => {
      if (!!a.active !== !!b.active) {
        return a.active ? -1 : 1;
      }

      const nameA = a.name?.toLowerCase() || '';
      const nameB = b.name?.toLowerCase() || '';
      return nameA.localeCompare(nameB);
    });
  }

  private toControlPayload(workflowId: string | number, cronSchedule?: string, webhookUrl?: string): WorkflowControl {
    const payload: WorkflowControl = { id: String(workflowId) };
    if (cronSchedule !== undefined) {
      payload.schedule = cronSchedule;
    }
    if (webhookUrl !== undefined) {
      payload.webhookUrl = webhookUrl;
    }
    return payload;
  }

  private resolveWebhookUrl(workflow: N8nWorkflow): string | undefined {
    if (workflow.webhookUrl) {
      return workflow.webhookUrl;
    }

    const fromParameters = this.buildWebhookUrl(workflow.parameters?.path);
    if (fromParameters) {
      return fromParameters;
    }

    if (Array.isArray(workflow.nodes)) {
      for (const node of workflow.nodes) {
        const fromNode = this.buildWebhookUrl(node?.parameters?.path);
        if (fromNode) {
          return fromNode;
        }
      }
    }

    return undefined;
  }

  private buildWebhookUrl(path?: string): string | undefined {
    if (!path) {
      return undefined;
    }

    const trimmedPath = path.trim().replace(/^\/+/, '');
    if (!trimmedPath) {
      return undefined;
    }

    if (!this.webhookBaseUrl) {
      return undefined;
    }

    return `${this.webhookBaseUrl}${trimmedPath}`;
  }

  private buildN8nWorkflowUrl(workflow: N8nWorkflow, webhookUrl?: string): string | undefined {
    const candidateSources = [
      webhookUrl,
      workflow.url,
      workflow.apiUrl,
      this.n8nBaseUrl
    ];

    for (const source of candidateSources) {
      if (!source) {
        continue;
      }

      try {
        const parsed = new URL(source);
        parsed.pathname = `/workflow/${workflow.id}`;
        parsed.search = '';
        parsed.hash = '';
        return parsed.toString();
      } catch {
        // Ignore parse errors and try next source
      }
    }

    return undefined;
  }

  private determineTriggerType(workflow: N8nWorkflow): WorkflowTriggerType | undefined {
    if (!Array.isArray(workflow.nodes)) {
      return undefined;
    }

    const chatNode = workflow.nodes.find(node => this.isChatTrigger(node?.type));
    if (chatNode) {
      return 'chat';
    }

    const formNode = workflow.nodes.find(node => this.isFormTrigger(node?.type));
    if (formNode) {
      return 'form';
    }

    const webhookNode = workflow.nodes.find(node => this.isWebhookTrigger(node?.type));
    if (webhookNode) {
      return 'webhook';
    }

    return undefined;
  }

  private isChatTrigger(nodeType?: string): boolean {
    return nodeType === '@n8n/n8n-nodes-langchain.chatTrigger';
  }

  private isFormTrigger(nodeType?: string): boolean {
    if (!nodeType) {
      return false;
    }

    const normalized = nodeType.toLowerCase();
    return normalized === 'n8n-nodes-base.formtrigger'
      || normalized === 'n8n-nodes-base.formtriggerpublished';
  }

  private isWebhookTrigger(nodeType?: string): boolean {
    return nodeType === 'n8n-nodes-base.webhook';
  }

  private buildN8nBaseUrl(n8nApiUrl?: string): string | undefined {
    if (!n8nApiUrl) {
      return undefined;
    }

    try {
      const parsed = new URL(n8nApiUrl);
      return `${parsed.protocol}//${parsed.host}`;
    } catch {
      return undefined;
    }
  }

  private buildWebhookBaseUrl(baseUrl?: string): string | undefined {
    if (!baseUrl) {
      return undefined;
    }

    return `${baseUrl.replace(/\/$/, '')}/webhook/`;
  }
}
