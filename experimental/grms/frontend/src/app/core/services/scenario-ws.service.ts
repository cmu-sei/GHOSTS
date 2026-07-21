import { Injectable, inject } from '@angular/core';
import { BehaviorSubject, Observable, Subject } from 'rxjs';
import { ConfigService } from './config.service';
import { GeopoliticalEvent } from '../models/event.model';
import { LeaderResponse, PopulationResponse } from '../models/response.model';

export type WsConnectionState = 'disconnected' | 'connecting' | 'connected' | 'error';

@Injectable({ providedIn: 'root' })
export class ScenarioWsService {
  private readonly config = inject(ConfigService);
  private ws: WebSocket | null = null;

  private readonly _state = new BehaviorSubject<WsConnectionState>('disconnected');
  private readonly _leaderResponse = new Subject<LeaderResponse>();
  private readonly _populationResponse = new Subject<PopulationResponse>();
  private readonly _complete = new Subject<{ event_id: string }>();

  readonly state$ = this._state.asObservable();
  readonly leaderResponse$ = this._leaderResponse.asObservable();
  readonly populationResponse$ = this._populationResponse.asObservable();
  readonly complete$ = this._complete.asObservable();

  connect(scenarioId: string): void {
    this.disconnect();
    this._state.next('connecting');

    const url = `${this.config.wsUrl}/ws/scenario/${scenarioId}`;
    this.ws = new WebSocket(url);

    this.ws.onopen = () => this._state.next('connected');

    this.ws.onmessage = (event) => {
      const data = JSON.parse(event.data);
      switch (data.type) {
        case 'leader_response':
          this._leaderResponse.next(data.payload);
          break;
        case 'population_response':
          this._populationResponse.next(data.payload);
          break;
        case 'complete':
          this._complete.next(data.payload);
          break;
      }
    };

    this.ws.onerror = () => this._state.next('error');
    this.ws.onclose = () => this._state.next('disconnected');
  }

  disconnect(): void {
    if (this.ws) {
      this.ws.close();
      this.ws = null;
    }
    this._state.next('disconnected');
  }

  sendEvent(event: GeopoliticalEvent, leaderId?: string, country?: string): void {
    if (this.ws?.readyState === WebSocket.OPEN) {
      this.ws.send(JSON.stringify({
        event,
        leader_id: leaderId,
        country,
      }));
    }
  }
}
