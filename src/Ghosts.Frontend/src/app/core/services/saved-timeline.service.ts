import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Observable, catchError, forkJoin, map, of, switchMap, tap } from 'rxjs';
import { SavedTimeline, SavedTimelineRequest } from '../models';
import { ConfigService } from './config.service';

/** Where the timeline editor kept timelines before they moved to the server */
const BROWSER_STORAGE_KEY = 'ghosts-local-timelines';

@Injectable({
  providedIn: 'root'
})
export class SavedTimelineService {
  private readonly http = inject(HttpClient);
  private readonly configService = inject(ConfigService);
  private readonly snackBar = inject(MatSnackBar);

  private get apiUrl(): string {
    return `${this.configService.apiUrl}/savedtimelines`;
  }

  getAll(): Observable<SavedTimeline[]> {
    return this.moveBrowserTimelines().pipe(switchMap(() => this.http.get<SavedTimeline[]>(this.apiUrl)));
  }

  getById(id: string): Observable<SavedTimeline> {
    return this.http.get<SavedTimeline>(`${this.apiUrl}/${id}`);
  }

  create(request: SavedTimelineRequest): Observable<SavedTimeline> {
    return this.http.post<SavedTimeline>(this.apiUrl, request).pipe(
      tap(timeline => this.openSnackBar(`Timeline "${timeline.name}" created`))
    );
  }

  update(id: string, request: SavedTimelineRequest): Observable<SavedTimeline> {
    return this.http.put<SavedTimeline>(`${this.apiUrl}/${id}`, request).pipe(
      tap(timeline => this.openSnackBar(`Timeline "${timeline.name}" updated`))
    );
  }

  delete(timeline: SavedTimeline): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${timeline.id}`).pipe(
      tap(() => this.openSnackBar(`Timeline "${timeline.name}" deleted`))
    );
  }

  /** Uploads timelines left in this browser by the old editor, once; they stay in the browser if the upload fails */
  private moveBrowserTimelines(): Observable<unknown> {
    let stored: SavedTimeline[] = [];
    try {
      stored = JSON.parse(localStorage.getItem(BROWSER_STORAGE_KEY) ?? '[]');
    } catch {
      return of(null);
    }
    if (!Array.isArray(stored) || stored.length === 0) {
      return of(null);
    }

    const uploads = stored.map(t => this.http.post<SavedTimeline>(this.apiUrl, {
      name: t.name,
      timeLineHandlers: t.timeLineHandlers ?? []
    }));
    return forkJoin(uploads).pipe(
      tap(() => {
        localStorage.removeItem(BROWSER_STORAGE_KEY);
        this.openSnackBar(`Moved ${stored.length} timeline(s) from this browser to the server`);
      }),
      map(() => null),
      catchError(err => {
        this.openSnackBar(`Couldn't move this browser's timelines to the server: ${err.message}`);
        return of(null);
      })
    );
  }

  private openSnackBar(message: string): void {
    this.snackBar.open(message, 'Close', {
      duration: 3000,
      horizontalPosition: 'end',
      verticalPosition: 'top'
    });
  }
}
