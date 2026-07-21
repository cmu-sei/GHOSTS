import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ConfigService } from './config.service';
import { PopulationProfile } from '../models/population.model';

@Injectable({ providedIn: 'root' })
export class PopulationService {
  private readonly http = inject(HttpClient);
  private readonly config = inject(ConfigService);

  private get baseUrl(): string {
    return `${this.config.apiUrl}/api/v1/populations`;
  }

  getAll(): Observable<PopulationProfile[]> {
    return this.http.get<PopulationProfile[]>(this.baseUrl);
  }

  getByCountry(country: string): Observable<PopulationProfile> {
    return this.http.get<PopulationProfile>(`${this.baseUrl}/${country}`);
  }

  create(profile: PopulationProfile): Observable<PopulationProfile> {
    return this.http.post<PopulationProfile>(this.baseUrl, profile);
  }
}
