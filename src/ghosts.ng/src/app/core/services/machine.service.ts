import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Machine, CreateMachineRequest, UpdateMachineRequest } from '../models';
import { ConfigService } from './config.service';

@Injectable({
  providedIn: 'root'
})
export class MachineService {
  private readonly http = inject(HttpClient);
  private readonly configService = inject(ConfigService);

  private get apiUrl(): string {
    return `${this.configService.apiUrl}/machines`;
  }

  getMachines(): Observable<Machine[]> {
    return this.http.get<Machine[]>(this.apiUrl);
  }

  getMachine(id: string): Observable<Machine> {
    return this.http.get<Machine>(`${this.apiUrl}/${id}`);
  }

  createMachine(request: CreateMachineRequest): Observable<Machine> {
    return this.http.post<Machine>(this.apiUrl, request);
  }

  updateMachine(id: string, request: UpdateMachineRequest): Observable<Machine> {
    return this.http.put<Machine>(`${this.apiUrl}/${id}`, request);
  }

  deleteMachine(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  getMachineActivity(id: string, skip?: number, take?: number): Observable<any[]> {
    let url = `${this.apiUrl}/${id}/activity`;
    const params: string[] = [];
    if (skip !== undefined) params.push(`skip=${skip}`);
    if (take !== undefined) params.push(`take=${take}`);
    if (params.length > 0) url += `?${params.join('&')}`;
    return this.http.get<any[]>(url);
  }

  getMachineHealth(id: string): Observable<any> {
    return this.http.get<any>(`${this.apiUrl}/${id}/health`);
  }
}
