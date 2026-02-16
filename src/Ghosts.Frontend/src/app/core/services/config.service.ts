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

    this.config = await firstValueFrom(
      this.http.get<AppConfig>('/assets/config.json')
    );

    return this.config;
  }

  get apiUrl(): string {
    if (!this.config?.apiUrl) {
      throw new Error('Config not loaded. Call loadConfig() first.');
    }
    return this.config.apiUrl;
  }

  get n8nApiUrl(): string {
    if (!this.config?.n8nApiUrl) {
      throw new Error('Config not loaded. Call loadConfig() first.');
    }
    return this.config.n8nApiUrl;
  }

  getConfig(): AppConfig {
    if (!this.config) {
      throw new Error('Config not loaded. Call loadConfig() first.');
    }
    return this.config;
  }
}
