import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { shareReplay } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { ApiStatus } from '../models/status.model';

@Injectable({
  providedIn: 'root'
})
export class StatusService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = environment.apiUrl.replace('/api', ''); // Base URL without /api suffix
  private statusCache$?: Observable<ApiStatus>;

  getStatus(): Observable<ApiStatus> {
    // Cache for 60 seconds to match API behavior
    if (!this.statusCache$) {
      this.statusCache$ = this.http.get<ApiStatus>(`${this.apiUrl}/test`).pipe(
        shareReplay({ bufferSize: 1, refCount: true })
      );

      // Clear cache after 60 seconds
      setTimeout(() => {
        this.statusCache$ = undefined;
      }, 60000);
    }

    return this.statusCache$;
  }
}
