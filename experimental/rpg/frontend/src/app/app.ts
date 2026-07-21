import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnDestroy,
  computed,
  inject,
  signal,
  viewChild,
  effect,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LowerCasePipe } from '@angular/common';
import { RpgApiService } from './rpg-api.service';
import { Frame, GameResponse, Hud, Task, TaskAction } from './rpg.models';

// One line in the terminal scrollback.
interface Line {
  kind: 'dm' | 'player' | 'notice' | 'system' | 'header';
  cell?: string; // for dm beats: Red Team / Blue Team / etc.
  time?: string;
  text: string;
}

@Component({
  selector: 'app-root',
  imports: [FormsModule, LowerCasePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App implements OnDestroy {
  private readonly api = inject(RpgApiService);
  private readonly ticker = window.setInterval(() => this.now.set(Date.now()), 1000);

  // ── state ──
  readonly lines = signal<Line[]>([]);
  readonly frame = signal<Frame | null>(null);
  readonly gameId = signal<string | null>(null);
  readonly command = signal('');
  readonly busy = signal(false);
  readonly started = signal(false);
  // The in-game scenario clock (e.g. "T+40m") — non-linear; it jumps between steps.
  // Held separately so it stays on the last-known time while the computer plays.
  readonly gameTime = signal<string | null>(null);
  readonly now = signal(Date.now());
  readonly frameReceivedAt = signal(Date.now());

  private readonly scroller = viewChild<ElementRef<HTMLDivElement>>('scroller');

  constructor() {
    // Auto-scroll the transcript to the bottom whenever lines change.
    effect(() => {
      this.lines();
      queueMicrotask(() => {
        const el = this.scroller()?.nativeElement;
        if (el) el.scrollTop = el.scrollHeight;
      });
    });
  }

  // ── derived state used by the template (computed signals = callable in tpl) ──
  readonly hud = computed<Hud | null>(() => this.frame()?.hud ?? null);
  readonly tasks = computed<Task[]>(() => this.frame()?.tasks ?? []);
  readonly awaiting = computed<boolean>(
    () => !!this.frame()?.awaiting_player && !this.busy(),
  );
  readonly complete = computed<boolean>(() => !!this.frame()?.is_complete);
  // A queued ticket is waiting behind the ones currently on the board.
  readonly canTable = computed<boolean>(() => !!this.frame()?.can_table);
  readonly fuseSecondsLeft = computed<number | null>(() => {
    const h = this.hud();
    if (
      !h ||
      h.containmentContained ||
      h.containmentFuseMinutesLeft === null ||
      this.complete()
    ) {
      return null;
    }
    const elapsedSeconds = Math.floor((this.now() - this.frameReceivedAt()) / 1000);
    return Math.max(0, h.containmentFuseMinutesLeft - elapsedSeconds);
  });
  readonly fuseClock = computed<string>(() => {
    const seconds = this.fuseSecondsLeft();
    return seconds === null ? '--:--' : this.formatClock(seconds);
  });
  readonly fusePercent = computed<number>(() => {
    const h = this.hud();
    const seconds = this.fuseSecondsLeft();
    if (!h?.containmentFuseMinutes || seconds === null) return 0;
    return Math.max(0, Math.min(100, (seconds / h.containmentFuseMinutes) * 100));
  });
  readonly fusePressed = computed<boolean>(() => {
    const h = this.hud();
    const seconds = this.fuseSecondsLeft();
    if (!h?.containmentFuseMinutes || seconds === null) return false;
    return seconds <= Math.max(5, Math.floor(h.containmentFuseMinutes / 3));
  });

  // ── lifecycle ──
  begin(): void {
    this.busy.set(true);
    this.started.set(true);
    this.lines.set([{ kind: 'system', text: 'Connecting to the exercise…' }]);
    this.api.newGame('soc-morning').subscribe({
      next: (r) => this.absorb(r, true),
      error: (e) => this.fail(e),
    });
  }

  submit(): void {
    const input = this.command().trim();
    if (!input || !this.awaiting()) return;
    this.pushLine({ kind: 'player', text: input });
    this.command.set('');
    this.busy.set(true);
    this.api.act(this.gameId()!, input).subscribe({
      next: (r) => this.absorb(r, false),
      error: (e) => this.fail(e),
    });
  }

  // Click a task's action chip. When several tickets are open, address the chosen
  // one explicitly as 'task N: <action>' so the server resolves it unambiguously.
  choose(task: Task, action: TaskAction): void {
    if (!this.awaiting()) return;
    const many = this.tasks().length > 1;
    this.command.set(many ? `task ${task.step}: ${action.label}` : action.label);
    this.submit();
  }

  // Table the current ticket(s): pull the next queued one onto the board without
  // resolving anything. Lets the player stack tickets open when they want to.
  table(): void {
    if (!this.awaiting() || !this.canTable()) return;
    this.command.set('next ticket');
    this.submit();
  }

  restart(): void {
    this.frame.set(null);
    this.gameId.set(null);
    this.lines.set([]);
    this.gameTime.set(null);
    this.started.set(false);
    this.begin();
  }

  ngOnDestroy(): void {
    window.clearInterval(this.ticker);
  }

  // ── apply a server frame to the scrollback ──
  private absorb(r: GameResponse, first: boolean): void {
    this.gameId.set(r.gameId);
    const f = r.frame;
    if (first) this.lines.set([]); // clear the "connecting…" line
    this.frameReceivedAt.set(Date.now());
    this.now.set(Date.now());

    for (const b of f.beats) {
      this.pushLine({ kind: 'dm', cell: b.cell, time: b.time, text: b.text });
    }
    // Advance the in-game clock: the newest beat's time, else the open ticket's time.
    const latest = f.beats.length ? f.beats[f.beats.length - 1].time : f.hud?.time;
    if (latest) this.gameTime.set(latest);
    for (const n of f.notices) {
      this.pushLine({ kind: 'notice', text: n });
    }
    if (f.is_complete && f.aar) {
      this.pushLine({ kind: 'header', text: `EXERCISE COMPLETE — ${f.aar.outcome}` });
    }
    this.frame.set(f);
    this.busy.set(false);
  }

  private pushLine(line: Line): void {
    this.lines.update((ls) => [...ls, line]);
  }

  private fail(e: unknown): void {
    this.pushLine({
      kind: 'notice',
      text: 'Connection error — is the RPG API running on :8095?',
    });
    this.busy.set(false);
    console.error(e);
  }

  // CSS class fragment for a DM beat based on its cell.
  cellClass(cell?: string): string {
    switch (cell) {
      case 'Red Team':
        return 'red';
      case 'Blue Team':
        return 'blue';
      case 'Green Cell':
        return 'green';
      default:
        return 'white';
    }
  }

  private formatClock(seconds: number): string {
    const minutes = Math.floor(seconds / 60);
    const remainder = seconds % 60;
    return `${minutes.toString().padStart(2, '0')}:${remainder.toString().padStart(2, '0')}`;
  }
}
