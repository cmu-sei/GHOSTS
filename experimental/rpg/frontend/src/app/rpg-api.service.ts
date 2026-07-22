import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { FixtureList, FixtureSummary, GameResponse } from './rpg.models';

// Backend base URL. Dev: FastAPI on 8095 (CORS allow-all). Override at build via env if needed.
const API = 'http://localhost:8095';

@Injectable({ providedIn: 'root' })
export class RpgApiService {
  private readonly http = inject(HttpClient);

  listFixtures(): Observable<FixtureList> {
    return this.http.get<{ fixtures: Array<FixtureSummary | string> }>(`${API}/api/fixtures`).pipe(
      map(({ fixtures }) => ({
        fixtures: fixtures.map((fixture) => this.normalizeFixture(fixture)),
      })),
    );
  }

  newGame(fixture: string): Observable<GameResponse> {
    return this.http.post<GameResponse>(`${API}/api/games`, { fixture });
  }

  act(gameId: string, input: string): Observable<GameResponse> {
    return this.http.post<GameResponse>(`${API}/api/games/${gameId}/act`, { input });
  }

  private normalizeFixture(fixture: FixtureSummary | string): FixtureSummary {
    if (typeof fixture !== 'string') return fixture;
    return {
      fixture,
      sortOrder: 0,
      name: this.fixtureName(fixture),
      description: 'Bundled exercise fixture.',
      era: 'LOCAL',
      theater: 'OFFLINE',
      estimatedMinutes: 0,
      events: 0,
      objectives: 0,
    };
  }

  private fixtureName(fixture: string): string {
    return fixture
      .split('-')
      .filter(Boolean)
      .map((word) => word[0]?.toUpperCase() + word.slice(1))
      .join(' ');
  }
}
