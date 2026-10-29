import { Injectable, inject } from '@angular/core';
import { Subject } from 'rxjs';
import * as signalR from '@microsoft/signalr';
import { ConfigService } from './config.service';
import { TerrainEvent } from '../models/execution.model';

export interface ExecutionHubMessage {
  executionId: number;
  updateType: string;
  data: any;
  timestamp: string;
}

@Injectable({
  providedIn: 'root',
})
export class ExecutionHubService {
  private readonly configService = inject(ConfigService);
  private connection: signalR.HubConnection | null = null;

  readonly updates$ = new Subject<ExecutionHubMessage>();

  /** GraySpace's /api/v1/events, relayed by GHOSTS. */
  readonly terrainEvents$ = new Subject<TerrainEvent>();

  connect(executionId: number): void {
    if (this.connection) {
      this.disconnect();
    }

    const baseUrl = this.configService.apiUrl.replace('/api', '');
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(`${baseUrl}/api/hubs/execution?executionId=${executionId}`)
      .withAutomaticReconnect()
      .build();

    this.connection.on('ExecutionUpdate', (message: ExecutionHubMessage) => {
      this.updates$.next(message);
    });
    this.connection.on('TerrainEvent', (event: TerrainEvent) => {
      this.terrainEvents$.next(event);
    });

    this.connection
      .start()
      .catch((err) => console.error('ExecutionHub connection failed:', err));
  }

  disconnect(): void {
    if (this.connection) {
      this.connection.stop();
      this.connection = null;
    }
  }
}
