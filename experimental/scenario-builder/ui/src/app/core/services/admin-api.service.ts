import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import {
  Kind, KindAdminSummary, KindDetail, KindVersionView, LibraryDetail, LibraryDraft, LibrarySummary, PreviewRequest, VersionView
} from '../models/builder.model';

/** What a new kind starts from: its name, what it is, and the kind it copies, if any. */
export interface NewKind {
  name: string;
  description: string;
  topology: boolean;
  basedOn?: string;
}

/** The admin API: template libraries, kinds and the template preview. The API refuses anyone not listed as an admin. */
@Injectable({ providedIn: 'root' })
export class AdminApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/admin';

  libraries(): Observable<LibrarySummary[]> {
    return this.http.get<LibrarySummary[]>(`${this.base}/libraries`);
  }

  create(name: string, description: string, kinds: string[]): Observable<LibraryDetail> {
    return this.http.post<LibraryDetail>(`${this.base}/libraries`, { name, description, kinds });
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

  kinds(): Observable<KindAdminSummary[]> {
    return this.http.get<KindAdminSummary[]>(`${this.base}/kinds`);
  }

  createKind(kind: NewKind): Observable<KindDetail> {
    return this.http.post<KindDetail>(`${this.base}/kinds`, kind);
  }

  kind(id: string): Observable<KindDetail> {
    return this.http.get<KindDetail>(`${this.base}/kinds/${encodeURIComponent(id)}`);
  }

  saveKind(id: string, kind: Kind): Observable<KindDetail> {
    return this.http.put<KindDetail>(`${this.base}/kinds/${encodeURIComponent(id)}`, kind);
  }

  publishKind(id: string): Observable<KindVersionView> {
    return this.http.post<KindVersionView>(`${this.base}/kinds/${encodeURIComponent(id)}/publish`, null);
  }

  retireKind(id: string, retired: boolean): Observable<KindDetail> {
    return this.http.put<KindDetail>(`${this.base}/kinds/${encodeURIComponent(id)}/retired`, { retired });
  }

  deleteKind(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/kinds/${encodeURIComponent(id)}`);
  }

  /** The latest published version as a .kind.json download. */
  kindExportUrl(id: string): string {
    return `${this.base}/kinds/${encodeURIComponent(id)}/export`;
  }

  /** A .kind.json file's text, sent as it is, so the API can say what is wrong with a file that isn't one. */
  importKind(file: string): Observable<KindDetail> {
    return this.http.post<KindDetail>(`${this.base}/kinds/import`, file, { headers: new HttpHeaders({ 'Content-Type': 'application/json' }) });
  }

  /** The model plans the draft's stages, replacing the ones it had. */
  draftStages(id: string, model: string): Observable<KindDetail> {
    return this.http.post<KindDetail>(`${this.base}/kinds/${encodeURIComponent(id)}/draft/stages`, { model });
  }

  /** The model writes one stage's questions, replacing the ones it had. */
  draftStage(id: string, stage: number, model: string): Observable<KindDetail> {
    return this.http.post<KindDetail>(`${this.base}/kinds/${encodeURIComponent(id)}/draft/stages/${stage}`, { model });
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
