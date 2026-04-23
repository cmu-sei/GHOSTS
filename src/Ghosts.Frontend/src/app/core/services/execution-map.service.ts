import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ConfigService } from './config.service';
import {
  GeoJsonFeatureCollection,
  MapLayerInfo,
  MapTimelineInfo,
  MapSearchResult,
  MapEntityDetail,
} from '../models/execution-map.model';

@Injectable({
  providedIn: 'root',
})
export class ExecutionMapService {
  private readonly http = inject(HttpClient);
  private readonly configService = inject(ConfigService);

  private apiUrl(executionId: number): string {
    return `${this.configService.apiUrl}/executionmap/${executionId}`;
  }

  getLayers(executionId: number): Observable<MapLayerInfo[]> {
    return this.http.get<MapLayerInfo[]>(`${this.apiUrl(executionId)}/layers`);
  }

  getAllFeatures(
    executionId: number,
    timeFrom?: string,
    timeTo?: string
  ): Observable<GeoJsonFeatureCollection> {
    let params = new HttpParams();
    if (timeFrom) params = params.set('timeFrom', timeFrom);
    if (timeTo) params = params.set('timeTo', timeTo);
    return this.http.get<GeoJsonFeatureCollection>(
      `${this.apiUrl(executionId)}/features`,
      { params }
    );
  }

  getFeaturesByType(
    executionId: number,
    featureType: string,
    options?: {
      timeFrom?: string;
      timeTo?: string;
      status?: string;
      team?: string;
    }
  ): Observable<GeoJsonFeatureCollection> {
    let params = new HttpParams();
    if (options?.timeFrom) params = params.set('timeFrom', options.timeFrom);
    if (options?.timeTo) params = params.set('timeTo', options.timeTo);
    if (options?.status) params = params.set('status', options.status);
    if (options?.team) params = params.set('team', options.team);
    return this.http.get<GeoJsonFeatureCollection>(
      `${this.apiUrl(executionId)}/features/${featureType}`,
      { params }
    );
  }

  getConnections(
    executionId: number
  ): Observable<GeoJsonFeatureCollection> {
    return this.http.get<GeoJsonFeatureCollection>(
      `${this.apiUrl(executionId)}/connections`
    );
  }

  getTimeline(
    executionId: number,
    buckets = 30
  ): Observable<MapTimelineInfo> {
    const params = new HttpParams().set('buckets', buckets.toString());
    return this.http.get<MapTimelineInfo>(
      `${this.apiUrl(executionId)}/timeline`,
      { params }
    );
  }

  search(
    executionId: number,
    query: string
  ): Observable<MapSearchResult[]> {
    const params = new HttpParams().set('q', query);
    return this.http.get<MapSearchResult[]>(
      `${this.apiUrl(executionId)}/search`,
      { params }
    );
  }

  getEntityDetail(
    executionId: number,
    featureType: string,
    entityId: string
  ): Observable<MapEntityDetail> {
    return this.http.get<MapEntityDetail>(
      `${this.apiUrl(executionId)}/entity/${featureType}/${entityId}`
    );
  }
}
