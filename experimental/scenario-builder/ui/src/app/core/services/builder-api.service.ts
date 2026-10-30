import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  AnsweredView, AnswerValue, DocumentView, Kind, KindSummary, LogEntry, PluginView, Progress, ProposalView, ScenarioSummary, SourceView
} from '../models/builder.model';

/**
 * The Scenario Builder API, called as the person at the keyboard: these calls never send X-Via,
 * so a person's answer is always stated. The agent calls the same API through its own client.
 */
@Injectable({ providedIn: 'root' })
export class BuilderApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api';

  getKinds(): Observable<KindSummary[]> {
    return this.http.get<KindSummary[]>(`${this.base}/kinds`);
  }

  getKind(id: string): Observable<Kind> {
    return this.http.get<Kind>(`${this.base}/kinds/${encodeURIComponent(id)}`);
  }

  getScenarios(): Observable<ScenarioSummary[]> {
    return this.http.get<ScenarioSummary[]>(`${this.base}/scenarios`);
  }

  createScenario(name: string, kindId: string): Observable<ScenarioSummary> {
    return this.http.post<ScenarioSummary>(`${this.base}/scenarios`, { name, kindId });
  }

  /** As the owner: hide a scenario from the list, or bring it back. */
  archive(id: number, archived: boolean): Observable<ScenarioSummary> {
    return this.http.put<ScenarioSummary>(`${this.base}/scenarios/${id}/archive`, { archived });
  }

  getSources(id: number): Observable<SourceView[]> {
    return this.http.get<SourceView[]>(`${this.base}/scenarios/${id}/sources`);
  }

  addTextSource(id: number, name: string, text: string): Observable<SourceView> {
    return this.http.post<SourceView>(`${this.base}/scenarios/${id}/sources/text`, { name, text });
  }

  /** The API fetches the page now; a page it can't reach is refused with the reason. */
  addUrlSource(id: number, url: string, name?: string): Observable<SourceView> {
    return this.http.post<SourceView>(`${this.base}/scenarios/${id}/sources/url`, { url, name: name || undefined });
  }

  uploadSource(id: number, file: File): Observable<SourceView> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<SourceView>(`${this.base}/scenarios/${id}/sources/file`, form);
  }

  removeSource(id: number, sourceId: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/scenarios/${id}/sources/${sourceId}`);
  }

  getProgress(id: number): Observable<Progress> {
    return this.http.get<Progress>(`${this.base}/scenarios/${id}/progress`);
  }

  answer(id: number, questionId: string, value: AnswerValue): Observable<AnsweredView> {
    return this.http.put<AnsweredView>(`${this.base}/scenarios/${id}/decisions/${encodeURIComponent(questionId)}`, { value });
  }

  getProposals(id: number, status?: ProposalView['status']): Observable<ProposalView[]> {
    return this.http.get<ProposalView[]>(`${this.base}/scenarios/${id}/proposals`, { params: status ? { status } : {} });
  }

  applyProposal(id: number, proposalId: number): Observable<ProposalView> {
    return this.http.post<ProposalView>(`${this.base}/scenarios/${id}/proposals/${proposalId}/apply`, null);
  }

  discardProposal(id: number, proposalId: number): Observable<ProposalView> {
    return this.http.post<ProposalView>(`${this.base}/scenarios/${id}/proposals/${proposalId}/discard`, null);
  }

  draft(id: number, field: string, text: string): Observable<{ field: string; text: string }> {
    return this.http.put<{ field: string; text: string }>(`${this.base}/scenarios/${id}/drafts/${encodeURIComponent(field)}`, { text });
  }

  approve(id: number, stage: number): Observable<{ stage: number; hash: string }> {
    return this.http.post<{ stage: number; hash: string }>(`${this.base}/scenarios/${id}/gates/${stage}/approve`, null);
  }

  advance(id: number): Observable<{ stage: number }> {
    return this.http.post<{ stage: number }>(`${this.base}/scenarios/${id}/advance`, null);
  }

  /** As the owner: hand every remaining decision to the agent, or take them back. */
  setDelegation(id: number, on: boolean): Observable<{ delegated: boolean }> {
    return this.http.put<{ delegated: boolean }>(`${this.base}/scenarios/${id}/delegation`, { on });
  }

  getLog(id: number): Observable<LogEntry[]> {
    return this.http.get<LogEntry[]>(`${this.base}/scenarios/${id}/log`);
  }

  pull(id: number): Observable<Record<string, unknown>> {
    return this.http.get<Record<string, unknown>>(`${this.base}/scenarios/${id}`);
  }

  /** The pull document, or the same scenario as Markdown, as a file download; a link, so the browser saves it. */
  exportUrl(id: number, format: 'json' | 'markdown' = 'json'): string {
    return `${this.base}/scenarios/${id}/export${format === 'markdown' ? '?format=markdown' : ''}`;
  }

  exportMarkdown(id: number): Observable<string> {
    return this.http.get(this.exportUrl(id, 'markdown'), { responseType: 'text' });
  }

  getDocuments(id: number): Observable<DocumentView[]> {
    return this.http.get<DocumentView[]>(`${this.base}/scenarios/${id}/documents`);
  }

  /** The rendered document to read, or with download its .html or .md file. */
  documentUrl(id: number, library: string, template: string, download = false): string {
    const url = `${this.base}/scenarios/${id}/documents/${encodeURIComponent(library)}/${encodeURIComponent(template)}`;
    return download ? `${url}?download=true` : url;
  }

  getPlugins(): Observable<PluginView[]> {
    return this.http.get<PluginView[]>(`${this.base}/plugins`);
  }

  /** As an admin: connect a plugin to its app by the app's API address. */
  connectPlugin(plugin: string, url: string): Observable<PluginView> {
    return this.http.put<PluginView>(`${this.base}/plugins/${encodeURIComponent(plugin)}`, { url });
  }

  disconnectPlugin(plugin: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/plugins/${encodeURIComponent(plugin)}`);
  }

  /** Hands the scenario to a connected plugin's app. Sending isn't built for any app yet, so this errors with the API's reason. */
  send(id: number, target: string): Observable<never> {
    return this.http.post<never>(`${this.base}/scenarios/${id}/send/${encodeURIComponent(target)}`, null);
  }
}

/** The API's own message for a refusal, or a plain one when there is none. */
export function errorMessage(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    const body = (typeof error.error === 'string' ? parsed(error.error) : error.error) as { message?: string } | null;
    return body?.message ?? (error.status === 0 ? 'The Scenario Builder API is not reachable.' : `The API answered HTTP ${error.status}.`);
  }
  return String(error);
}

/** A text response's error body, which the API still writes as JSON. */
function parsed(text: string): unknown {
  try {
    return JSON.parse(text);
  } catch {
    return null;
  }
}
