import { Injectable, inject } from '@angular/core';
import { Subject } from 'rxjs';
import * as signalR from '@microsoft/signalr';
import { ConfigService } from './config.service';
import { CreateScenario } from '../models/scenario.model';

export interface ScenarioHubMessage {
  scenarioId: number;
  timestamp: string;
  error?: string;
}

@Injectable({
  providedIn: 'root',
})
export class ScenarioHubService {
  private readonly configService = inject(ConfigService);
  private connection: signalR.HubConnection | null = null;
  private connectedScenarioId: number | null = null;

  readonly saved$ = new Subject<ScenarioHubMessage>();
  readonly error$ = new Subject<ScenarioHubMessage>();
  readonly externalUpdate$ = new Subject<ScenarioHubMessage>();

  connect(scenarioId: number): void {
    if (this.connection && this.connectedScenarioId === scenarioId) {
      return;
    }

    this.disconnect();

    const baseUrl = this.configService.apiUrl.replace('/api', '');
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(`${baseUrl}/api/hubs/scenarioBuilder?scenarioId=${scenarioId}`)
      .withAutomaticReconnect()
      .build();

    this.connection.on('ScenarioSaved', (message: ScenarioHubMessage) => {
      this.saved$.next(message);
    });

    this.connection.on('ScenarioSaveError', (message: ScenarioHubMessage) => {
      this.error$.next(message);
    });

    this.connection.on('ScenarioUpdated', (message: ScenarioHubMessage) => {
      this.externalUpdate$.next(message);
    });

    this.connection
      .start()
      .then(() => {
        this.connectedScenarioId = scenarioId;
      })
      .catch((err) => console.error('ScenarioHub connection failed:', err));
  }

  updateScenario(scenarioId: number, scenario: CreateScenario): void {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      console.warn('ScenarioHub not connected, cannot send update');
      return;
    }

    this.connection.invoke('UpdateScenario', scenarioId, scenario)
      .catch((err) => console.error('ScenarioHub invoke failed:', err));
  }

  disconnect(): void {
    if (this.connection) {
      this.connection.stop();
      this.connection = null;
      this.connectedScenarioId = null;
    }
  }
}
