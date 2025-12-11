import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { shareReplay } from 'rxjs/operators';
import { ApiStatus } from '../models/status.model';
import { ConfigService } from './config.service';

@Injectable({
  providedIn: 'root'
})
export class StatusService {
  private readonly http = inject(HttpClient);
  private readonly configService = inject(ConfigService);
  private statusCache$?: Observable<ApiStatus>;

  private get apiUrl(): string {
    return this.configService.apiUrl.replace('/api', ''); // Base URL without /api suffix
  }

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
