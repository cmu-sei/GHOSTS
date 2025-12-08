import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

export interface AppConfig {
  apiUrl: string;
  n8nApiUrl: string;
}

@Injectable({
  providedIn: 'root'
})
export class ConfigService {
  private readonly http = inject(HttpClient);
  private config?: AppConfig;

  async loadConfig(): Promise<AppConfig> {
    if (this.config) {
      return this.config;
    }

    try {
      this.config = await firstValueFrom(
        this.http.get<AppConfig>('/assets/config.json')
      );
    } catch (error) {
      console.warn('Failed to load config.json, using defaults', error);
      // Fallback to localhost for local development
      this.config = {
        apiUrl: 'http://localhost:5000/api',
        n8nApiUrl: 'http://localhost:5678'
      };
    }

    return this.config;
  }

  get apiUrl(): string {
    return this.config?.apiUrl || 'http://localhost:5000/api';
  }

  get n8nApiUrl(): string {
    return this.config?.n8nApiUrl || 'http://localhost:5678';
  }
}
