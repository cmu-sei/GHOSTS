import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

export interface AppConfig {
  apiUrl: string;
  wsUrl: string;
}

@Injectable({ providedIn: 'root' })
export class ConfigService {
  private readonly http = inject(HttpClient);
  private config?: AppConfig;
  private loaded = false;

  async loadConfig(): Promise<AppConfig> {
    if (this.config) {
      return this.config;
    }
    this.config = await firstValueFrom(
      this.http.get<AppConfig>('/assets/config.json')
    );
    this.loaded = true;
    return this.config;
  }

  get apiUrl(): string {
    if (!this.loaded) {
      throw new Error('Config not loaded. Call loadConfig() first.');
    }
    return this.config!.apiUrl;
  }

  get wsUrl(): string {
    if (!this.loaded) {
      throw new Error('Config not loaded. Call loadConfig() first.');
    }
    return this.config!.wsUrl;
  }
}
