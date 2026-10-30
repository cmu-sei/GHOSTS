import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { LibraryDetail, LibraryDraft, LibrarySummary, PreviewRequest, VersionView } from '../models/builder.model';

/** The admin API: template libraries and the template preview. The API refuses anyone not listed as an admin. */
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

  /** The rendered page, as its audience would get it. */
  preview(request: PreviewRequest): Observable<string> {
    return this.http.post(`${this.base}/preview`, request, { responseType: 'text' });
  }
}
