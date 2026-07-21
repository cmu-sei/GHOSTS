import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { FixtureList, GameResponse } from './rpg.models';

// Backend base URL. Dev: FastAPI on 8095 (CORS allow-all). Override at build via env if needed.
const API = 'http://localhost:8095';

@Injectable({ providedIn: 'root' })
export class RpgApiService {
  private readonly http = inject(HttpClient);

  listFixtures(): Observable<FixtureList> {
    return this.http.get<FixtureList>(`${API}/api/fixtures`);
  }

  newGame(fixture: string): Observable<GameResponse> {
    return this.http.post<GameResponse>(`${API}/api/games`, { fixture });
  }

  act(gameId: string, input: string): Observable<GameResponse> {
    return this.http.post<GameResponse>(`${API}/api/games/${gameId}/act`, { input });
  }
}
