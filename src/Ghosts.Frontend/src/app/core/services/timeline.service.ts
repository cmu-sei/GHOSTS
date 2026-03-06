import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Timeline, PostTimelineRequest } from '../models';
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

  getTimelineStatus(machineId: string, timelineId: string): Observable<Timeline> {
    return this.http.get<Timeline>(`${this.apiUrl}/${machineId}/${timelineId}`);
  }

  stopTimeline(machineId: string, timelineId: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/${machineId}/${timelineId}/stop`, {});
  }
}
