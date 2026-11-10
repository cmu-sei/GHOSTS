import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Timeline, PostTimelineRequest, SendCommandsRequest, SendCommandsResponse, MachineTimeline } from '../models';
import { ConfigService } from './config.service';

@Injectable({
  providedIn: 'root'
})
export class TimelineService {
  private readonly http = inject(HttpClient);
  private readonly configService = inject(ConfigService);

  private get apiUrl(): string {
    return `${this.configService.apiUrl}/timelines`;
  }

  postTimeline(request: PostTimelineRequest): Observable<void> {
    return this.http.post<void>(this.apiUrl, request);
  }

  /** The timelines a machine's client has reported back, one row per report */
  getMachineTimelines(machineId: string): Observable<MachineTimeline[]> {
    return this.http.get<MachineTimeline[]>(`${this.apiUrl}/${machineId}`);
  }

  getTimelineStatus(machineId: string, timelineId: string): Observable<Timeline> {
    return this.http.get<Timeline>(`${this.apiUrl}/${machineId}/${timelineId}`);
  }

  stopTimeline(machineId: string, timelineId: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/${machineId}/${timelineId}/stop`, {});
  }

  sendCommands(machineId: string, request: SendCommandsRequest): Observable<SendCommandsResponse> {
    return this.http.post<SendCommandsResponse>(`${this.apiUrl}/${machineId}/commands`, request);
  }
}
