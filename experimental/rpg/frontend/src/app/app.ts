import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  signal,
  viewChild,
  effect,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LowerCasePipe } from '@angular/common';
import { RpgApiService } from './rpg-api.service';
import { FixtureSummary, Frame, GameResponse, Hud } from './rpg.models';

// One line in the terminal scrollback.
interface Line {
  kind: 'beat' | 'player' | 'notice' | 'ruling' | 'system' | 'header';
  speaker?: string; // for beats: Judge / Signals / Control
  text: string;
}

@Component({
  selector: 'app-root',
  imports: [FormsModule, LowerCasePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  private readonly api = inject(RpgApiService);

  // ── state ──
  readonly lines = signal<Line[]>([]);
  readonly frame = signal<Frame | null>(null);
  readonly gameId = signal<string | null>(null);
  readonly action = signal('');
  readonly rationale = signal('');
  readonly busy = signal(false);
  readonly started = signal(false);
  // Seconds elapsed on the current adjudication, so a multi-minute LLM turn
  // reads as "working" rather than frozen.
  readonly elapsed = signal(0);
  private timer: ReturnType<typeof setInterval> | null = null;
  readonly fixtures = signal<FixtureSummary[]>([]);
  readonly selectedFixture = signal<string | null>(null);
  readonly catalogLoading = signal(true);
  readonly catalogError = signal(false);

  private readonly scroller = viewChild<ElementRef<HTMLDivElement>>('scroller');

  constructor() {
    this.loadCatalog();

    // Auto-scroll the transcript to the bottom whenever lines change.
    effect(() => {
      this.lines();
      queueMicrotask(() => {
        const el = this.scroller()?.nativeElement;
        if (el) el.scrollTop = el.scrollHeight;
      });
    });
  }

  // ── derived state used by the template ──
  readonly hud = computed<Hud | null>(() => this.frame()?.hud ?? null);
  readonly awaiting = computed<boolean>(
    () => !!this.frame()?.awaiting_player && !this.busy(),
  );
  readonly complete = computed<boolean>(() => !!this.frame()?.is_complete);
  // OPFOR progress as a percentage of its win threshold, for the pressure bar.
  readonly opforPercent = computed<number>(() => {
    const h = this.hud();
    if (!h || !h.opforThreshold) return 0;
    return Math.max(0, Math.min(100, (h.opforProgress / h.opforThreshold) * 100));
  });
  readonly clockPercent = computed<number>(() => {
    const h = this.hud();
    if (!h || !h.windowMinutes) return 0;
    return Math.max(0, Math.min(100, (h.minutesLeft / h.windowMinutes) * 100));
  });

  // ── lifecycle ──
  loadCatalog(): void {
    this.catalogLoading.set(true);
    this.catalogError.set(false);
    this.api.listFixtures().subscribe({
      next: ({ fixtures }) => {
        this.fixtures.set(fixtures);
        this.catalogLoading.set(false);
      },
      error: (e) => {
        this.catalogLoading.set(false);
        this.catalogError.set(true);
        console.error(e);
      },
    });
  }

  selectScenario(fixture: string): void {
    if (!this.busy()) this.selectedFixture.set(fixture);
  }

  begin(): void {
    const fixture = this.selectedFixture();
    if (!fixture) return;
    this.startBusy();
    this.started.set(true);
    this.lines.set([{ kind: 'system', text: 'Connecting to the exercise…' }]);
    this.api.newGame(fixture).subscribe({
      next: (r) => this.absorb(r, true),
      error: (e) => this.fail(e),
    });
  }

  submit(): void {
    const action = this.action().trim();
    if (!action || !this.awaiting()) return;
    const rationale = this.rationale().trim();
    this.pushLine({
      kind: 'player',
      text: rationale ? `${action} — because ${rationale}` : action,
    });
    this.action.set('');
    this.rationale.set('');
    this.startBusy();
    this.api.act(this.gameId()!, action, rationale).subscribe({
      next: (r) => this.absorb(r, false),
      error: (e) => this.fail(e),
    });
  }

  restart(): void {
    this.resetGame();
    this.begin();
  }

  chooseScenario(): void {
    this.resetGame();
    this.selectedFixture.set(null);
  }

  private resetGame(): void {
    this.frame.set(null);
    this.gameId.set(null);
    this.lines.set([]);
    this.action.set('');
    this.rationale.set('');
    this.started.set(false);
  }

  // ── apply a server frame to the scrollback ──
  private absorb(r: GameResponse, first: boolean): void {
    this.gameId.set(r.gameId);
    const f = r.frame;
    if (first) this.lines.set([]); // clear the "connecting…" line

    for (const b of f.beats) {
      this.pushLine({ kind: 'beat', speaker: b.speaker, text: b.text });
    }
    // The umpire's ruling on the player's last move: band → tier + critique.
    if (f.band) {
      const critique = f.critique ? ` — ${f.critique}` : '';
      this.pushLine({ kind: 'ruling', text: `Umpire odds: ${f.band} → ${f.tier}${critique}` });
    }
    for (const n of f.notices) {
      this.pushLine({ kind: 'notice', text: n });
    }
    if (f.is_complete && f.aar) {
      this.pushLine({ kind: 'header', text: `EXERCISE COMPLETE — ${f.aar.outcome}` });
    }
    this.frame.set(f);
    this.stopBusy();
  }

  private pushLine(line: Line): void {
    this.lines.update((ls) => [...ls, line]);
  }

  // Begin an adjudication: mark busy and start counting elapsed seconds.
  private startBusy(): void {
    this.busy.set(true);
    this.elapsed.set(0);
    this.timer = setInterval(() => this.elapsed.update((s) => s + 1), 1000);
  }

  private stopBusy(): void {
    if (this.timer) {
      clearInterval(this.timer);
      this.timer = null;
    }
    this.busy.set(false);
  }

  private fail(e: unknown): void {
    this.pushLine({
      kind: 'notice',
      text: 'Connection error — is the RPG API running on :8095?',
    });
    this.stopBusy();
    console.error(e);
  }

  // CSS class fragment for a beat based on its speaker.
  speakerClass(speaker?: string): string {
    switch (speaker) {
      case 'Signals':
        return 'amber';
      case 'Judge':
        return 'blue';
      case 'Control':
        return 'white';
      default:
        return 'white';
    }
  }
}
