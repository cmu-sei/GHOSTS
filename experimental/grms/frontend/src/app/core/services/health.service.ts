import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ConfigService } from './config.service';
import { HealthStatus } from '../models/health.model';

@Injectable({ providedIn: 'root' })
export class HealthService {
  private readonly http = inject(HttpClient);
  private readonly config = inject(ConfigService);

  getHealth(): Observable<HealthStatus> {
    return this.http.get<HealthStatus>(`${this.config.apiUrl}/health`);
  }
}
