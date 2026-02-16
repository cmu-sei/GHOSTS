import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  JobInfo,
  AnimationStartRequest,
  AnimationStopRequest,
  AnimationJobTypes
} from '../models';
import { ConfigService } from './config.service';

@Injectable({
  providedIn: 'root'
})
export class AnimationService {
  private readonly http = inject(HttpClient);
  private readonly configService = inject(ConfigService);

  private get apiUrl(): string {
    return `${this.configService.apiUrl}/animations`;
  }

  getRunningJobs(): Observable<JobInfo[]> {
    return this.http.get<JobInfo[]>(`${this.apiUrl}/jobs`);
  }

  startAnimation(request: AnimationStartRequest): Observable<any> {
    const formData = new FormData();
    formData.append('jobId', request.jobId);
    formData.append('jobConfiguration', request.jobConfiguration);
    return this.http.post(`${this.apiUrl}/start`, formData);
  }

  stopAnimation(request: AnimationStopRequest): Observable<any> {
    return this.http.post(`${this.apiUrl}/stop?jobId=${encodeURIComponent(request.jobId)}`, null);
  }

  getAnimationOutput(jobType: AnimationJobTypes): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/output/${jobType}`, {
      responseType: 'blob'
    });
  }
}
