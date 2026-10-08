import { Component, ElementRef, Input, OnInit, OnDestroy, SecurityContext, afterRenderEffect, inject, signal, computed, viewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { DomSanitizer } from '@angular/platform-browser';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ChangeDetectionStrategy } from '@angular/core';
import { Subscription, interval } from 'rxjs';
import MarkdownIt from 'markdown-it';
import { ScenarioAuthoringService } from '../../../core/services/scenario-authoring.service';
import { ScenarioHubService } from '../../../core/services/scenario-hub.service';
import {
  AuthoringSessionRecord, AuthoringSessionSummary, AuthoringTurnRecord, AuthoringChunk, AuthoringReadiness, AuthoringReadinessQuestion,
} from '../../../core/models/scenario-authoring.model';
import { BuilderModel } from '../../../core/models/scenario-builder.model';
import { ScenarioBuilderService } from '../../../core/services/scenario-builder.service';

/** One turn as the page renders it: the developer's message, and the agent's post once it ends. */
interface ConversationTurn extends AuthoringTurnRecord {
  html: string;
  chunkIds: number[];
  /** How many times the turn searched the scenario's sources (J8). */
  searches: number;
}

@Component({
  selector: 'app-builder-conversation',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatCardModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatIconModule,
    MatChipsModule,
    MatSelectModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    MatTooltipModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './builder-conversation.component.html',
  styleUrls: ['./builder-conversation.component.scss'],
})
export class BuilderConversationComponent implements OnInit, OnDestroy {
  @Input({ required: true }) scenarioId!: number;

  private readonly authoring = inject(ScenarioAuthoringService);
  private readonly builderService = inject(ScenarioBuilderService);
  private readonly hub = inject(ScenarioHubService);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly md = new MarkdownIt({ html: false, linkify: true, breaks: true });

  protected readonly loading = signal(true);
  protected readonly sessions = signal<AuthoringSessionSummary[]>([]);
  protected readonly session = signal<AuthoringSessionRecord | null>(null);
  /** The scenario's Builder model and effort, picked on the Sources step; a new session starts on them and keeps them (C5, H3). */
  protected readonly builderModel = signal<BuilderModel | null>(null);
  protected readonly modelName = computed(() => {
    const choice = this.builderModel();
    return choice?.models.find((m) => m.id === choice.model)?.name ?? choice?.model ?? '';
  });
  protected readonly turns = signal<ConversationTurn[]>([]);
  protected readonly message = signal('');
  protected readonly busy = signal(false);
  protected readonly progressLine = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);
  /** The message just sent, shown at once; the server's turn record replaces it on the next read. */
  protected readonly pending = signal<string | null>(null);
  protected readonly openDocument = signal<{ hash: string; text: string; findings: string } | null>(null);
  protected readonly openPlan = signal<{ hash: string; html: string } | null>(null);
  protected readonly openDetails = signal<Set<number>>(new Set());
  protected readonly openChunks = signal<Map<number, string>>(new Map());

  /** The dashboard for this scenario before any session: every question open, the sources uncited. */
  private readonly blankReadiness = signal<AuthoringReadiness | null>(null);
  /** The readiness dashboard beside the thread: the session's own once one exists, else the blank one. */
  protected readonly dash = computed(() => this.session()?.readiness ?? this.blankReadiness());

  protected readonly canImport = computed(() => this.session()?.canImport ?? false);
  protected readonly importedBefore = computed(() => this.session()?.importedBefore ?? false);

  // The posts list scrolls within itself; it follows the bottom when a post is added or a reply lands,
  // not on every poll, so reading back through the thread is not interrupted.
  private readonly postsEl = viewChild<ElementRef<HTMLElement>>('posts');
  private readonly composerEl = viewChild<ElementRef<HTMLTextAreaElement>>('composer');
  private readonly postsKey = computed(() => {
    const turns = this.turns();
    return `${turns.length}:${turns[turns.length - 1]?.result ? 1 : 0}:${this.pending() ? 1 : 0}`;
  });
  private readonly followBottom = afterRenderEffect(() => {
    this.postsKey();
    const el = this.postsEl()?.nativeElement;
    if (el) el.scrollTop = el.scrollHeight;
  });
  protected readonly latestHash = computed(() => this.session()?.latestDocument?.hash ?? null);

  private progressSub?: Subscription;
  private pollSub?: Subscription;
  private elapsedSub?: Subscription;
  private turnStartedAt = 0;
  protected readonly elapsedSeconds = signal(0);

  ngOnInit(): void {
    this.hub.connect(this.scenarioId);
    this.progressSub = this.hub.authoringProgress$.subscribe((event) => {
      if (event.sessionId !== this.session()?.id) return;
      if (event.kind === 'turn-ended') {
        this.refresh(event.sessionId);
      } else {
        this.progressLine.set(this.describeProgress(event));
        // A validate call has kept its document already (C2), so the dashboard can show it before the turn ends.
        if (event.kind === 'tool-call' && event.detail?.['hash']) this.refresh(event.sessionId);
      }
    });
    this.builderService.getModel(this.scenarioId).subscribe({
      next: (choice) => this.builderModel.set(choice),
      error: () => this.snackBar.open('Could not read the scenario\'s model; the conversation will use the default.', 'Close', { duration: 5000 }),
    });
    this.authoring.getReadiness(this.scenarioId).subscribe({
      next: (readiness) => this.blankReadiness.set(readiness),
      error: () => { /* the panel keeps its empty state until a session supplies one */ },
    });
    this.loadSessions();
  }

  ngOnDestroy(): void {
    this.progressSub?.unsubscribe();
    this.pollSub?.unsubscribe();
    this.elapsedSub?.unsubscribe();
  }

  private loadSessions(): void {
    this.loading.set(true);
    this.authoring.getSessions(this.scenarioId).subscribe({
      next: (sessions) => {
        this.sessions.set(sessions);
        if (sessions.length > 0) {
          this.openSession(sessions[0].id);
        } else {
          this.loading.set(false);
        }
      },
      error: () => {
        this.error.set('Could not load this scenario\'s authoring sessions.');
        this.loading.set(false);
      },
    });
  }

  protected openSession(id: string): void {
    this.loading.set(true);
    this.openDocument.set(null);
    this.authoring.getSession(id).subscribe({
      next: (record) => this.applySession(record),
      error: () => {
        this.error.set(`Could not load session ${id}.`);
        this.loading.set(false);
      },
    });
  }

  private applySession(record: AuthoringSessionRecord): void {
    this.session.set(record);
    this.turns.set(record.turns.map((t) => this.render(t)));
    this.loading.set(false);
    this.busy.set(record.running);
    if (!record.running || record.turns.some((t) => t.endedAt === null)) this.pending.set(null);
    if (record.running) {
      this.startPolling(record.id);
      // Resumed mid-turn (a reload, or opening another session's tab): the timer has no real start
      // time to count from, so it counts from now rather than showing nothing.
      if (!this.elapsedSub) {
        this.turnStartedAt = Date.now();
        this.startElapsedTimer();
      }
    } else {
      this.stopPolling();
    }
  }

  protected startNewSession(): void {
    this.session.set(null);
    this.turns.set([]);
    this.error.set(null);
    this.openDocument.set(null);
    this.openPlan.set(null);
    this.composerEl()?.nativeElement.focus();
  }

  /** B4: the plan is the server's rendering of the document, not the model's prose; opening it shows the document (A3). */
  protected togglePlan(hash: string | null): void {
    const id = this.session()?.id;
    if (!id || !hash) return;
    if (this.openPlan()?.hash === hash) {
      this.openPlan.set(null);
      return;
    }
    this.authoring.getPlan(id, hash).subscribe({
      next: (markdown) => {
        this.openPlan.set({ hash, html: this.sanitizer.sanitize(SecurityContext.HTML, this.md.render(markdown)) ?? '' });
        this.refresh(id);
      },
      error: () => this.snackBar.open('Could not render the plan.', 'Close', { duration: 3000 }),
    });
  }

  /** The first message makes the session, then runs as its first turn. */
  private startInterview(text: string): void {
    this.error.set(null);
    this.busy.set(true);
    this.pending.set(text);
    this.authoring.startSession(this.scenarioId).subscribe({
      next: (session) => {
        this.sessions.update((list) => [{ id: session.id, model: session.model, turns: 0, importedScenarioId: null, createdAt: new Date().toISOString(), updatedAt: new Date().toISOString() }, ...list]);
        // The turn first, so the session read that follows sees it running and keeps sending locked. A
        // refresh, not openSession, so the page does not reload and the composer keeps the cursor.
        this.sendTurn(session.id, text, () => this.refresh(session.id));
      },
      error: () => {
        this.busy.set(false);
        this.pending.set(null);
        if (!this.message().trim()) this.message.set(text);
        this.error.set('Could not start a session.');
      },
    });
  }

  /** Enter sends; Shift+Enter starts a new line, as the prototype's composer did. */
  protected onComposerKeydown(event: Event): void {
    const keyboard = event as KeyboardEvent;
    if (keyboard.shiftKey) return;
    keyboard.preventDefault();
    this.send();
  }

  /** The composer stays usable while a turn runs, so the cursor stays in it; only sending waits. */
  protected send(): void {
    const text = this.message().trim();
    if (!text || this.busy()) return;
    this.message.set('');
    const id = this.session()?.id;
    if (id) this.sendTurn(id, text);
    else this.startInterview(text);
  }

  private sendTurn(id: string, text: string, done?: () => void): void {
    this.error.set(null);
    this.progressLine.set('Working…');
    this.busy.set(true);
    this.pending.set(text);
    this.turnStartedAt = Date.now();
    this.startElapsedTimer();
    this.authoring.runTurn(id, text).subscribe({
      next: () => {
        done?.();
        this.startPolling(id);
      },
      error: (err) => {
        this.busy.set(false);
        this.pending.set(null);
        if (!this.message().trim()) this.message.set(text);
        this.stopElapsedTimer();
        this.error.set(err?.error?.error ?? 'The turn could not be started.');
        done?.();
      },
    });
  }

  /** A fallback for when the hub is not connected: poll until the turn ends. */
  private startPolling(id: string): void {
    this.pollSub?.unsubscribe();
    this.pollSub = interval(4000).subscribe(() => this.refresh(id));
  }

  private stopPolling(): void {
    this.pollSub?.unsubscribe();
    this.pollSub = undefined;
    this.stopElapsedTimer();
  }

  private startElapsedTimer(): void {
    this.elapsedSub?.unsubscribe();
    this.elapsedSub = interval(1000).subscribe(() => this.elapsedSeconds.set(Math.floor((Date.now() - this.turnStartedAt) / 1000)));
  }

  private stopElapsedTimer(): void {
    this.elapsedSub?.unsubscribe();
    this.elapsedSub = undefined;
    this.elapsedSeconds.set(0);
    this.progressLine.set(null);
  }

  private refresh(id: string): void {
    this.authoring.getSession(id).subscribe({
      next: (record) => {
        const wasBusy = this.busy();
        this.applySession(record);
        if (wasBusy && !record.running) this.stopElapsedTimer();
      },
      error: () => {
        /* a transient failure while polling; the next tick tries again */
      },
    });
  }

  private describeProgress(event: { kind: string; detail: Record<string, unknown> | null }): string {
    if (event.kind === 'model-reply') return 'The agent is thinking…';
    if (event.kind === 'tool-call') {
      const name = String(event.detail?.['name'] ?? 'a tool');
      return `Running ${name}…`;
    }
    return 'Working…';
  }

  private render(turn: AuthoringTurnRecord): ConversationTurn {
    const reply = turn.result?.reply ?? '';
    const html = reply ? this.sanitizer.sanitize(SecurityContext.HTML, this.md.render(reply)) ?? '' : '';
    const chunkIds = [...new Set([...reply.matchAll(/\[chunk (\d+)\]/gi)].map((m) => Number(m[1])))];
    const searches = turn.result?.toolCalls.filter((c) => c.name === 'scenario_source_search').length ?? 0;
    return { ...turn, html, chunkIds, searches };
  }

  protected questionsFor(group: string): AuthoringReadinessQuestion[] {
    return this.dash()?.questions.filter((q) => q.group === group) ?? [];
  }

  protected statusIcon(status: AuthoringReadinessQuestion['status']): string {
    switch (status) {
      case 'stated': return 'check_circle';
      case 'proposed': return 'tips_and_updates';
      case 'drafted': return 'edit_note';
      case 'open': return 'help_outline';
    }
  }

  /** 14200 reads as 14.2k, 257256 as 257k: a glance, not a ledger. */
  protected compact(n: number): string {
    if (n < 1000) return `${n}`;
    if (n < 10000) return `${(n / 1000).toFixed(1)}k`;
    if (n < 1000000) return `${Math.round(n / 1000)}k`;
    return `${(n / 1000000).toFixed(1)}M`;
  }

  protected toggleDetails(turnNumber: number): void {
    this.openDetails.update((set) => {
      const next = new Set(set);
      if (next.has(turnNumber)) next.delete(turnNumber);
      else next.add(turnNumber);
      return next;
    });
  }

  protected isDetailsOpen(turnNumber: number): boolean {
    return this.openDetails().has(turnNumber);
  }

  protected toggleDocument(hash: string | null): void {
    const id = this.session()?.id;
    if (!id || !hash) return;
    if (this.openDocument()?.hash === hash) {
      this.openDocument.set(null);
      return;
    }
    this.authoring.getDocument(id, hash).subscribe({
      next: (doc) => {
        this.openDocument.set({
          hash: doc.hash,
          text: JSON.stringify(JSON.parse(doc.document), null, 2),
          findings: doc.findings.map((f) => `${f.severity.toUpperCase()} ${f.code} ${f.path}: ${f.message}`).join('\n'),
        });
        // Opening it is what shows it (A3); re-read the session so canImport reflects that.
        this.refresh(id);
      },
      error: () => this.snackBar.open('Could not load that document.', 'Close', { duration: 3000 }),
    });
  }

  protected toggleChunk(chunkId: number): void {
    const id = this.session()?.id;
    if (!id) return;
    if (this.openChunks().has(chunkId)) {
      this.openChunks.update((map) => { const next = new Map(map); next.delete(chunkId); return next; });
      return;
    }
    this.authoring.getChunk(id, chunkId).subscribe({
      next: (chunk: AuthoringChunk) => this.openChunks.update((map) => new Map(map).set(chunkId, chunk.text)),
      error: () => this.snackBar.open(`Could not load chunk ${chunkId}.`, 'Close', { duration: 3000 }),
    });
  }

  protected isChunkOpen(chunkId: number): boolean {
    return this.openChunks().has(chunkId);
  }

  protected chunkText(chunkId: number): string | undefined {
    return this.openChunks().get(chunkId);
  }

  protected import(): void {
    const id = this.session()?.id;
    const hash = this.latestHash();
    if (!id || !hash) return;
    this.doImport(id, hash, false, false);
  }

  private doImport(id: string, hash: string, again: boolean, replace: boolean): void {
    this.authoring.import(id, hash, again, replace).subscribe({
      next: (result) => {
        this.snackBar.open(`Imported as scenario ${result.scenarioId}.`, 'Open', { duration: 6000 })
          .onAction().subscribe(() => this.router.navigate(['/scenarios', result.scenarioId]));
        this.refresh(id);
      },
      error: (err) => {
        const body = err?.error as { needsConfirmation?: boolean; confirm?: string; reason?: string } | undefined;
        if (body?.needsConfirmation && body.confirm && window.confirm(`${body.reason}\n\nContinue?`)) {
          this.doImport(id, hash, body.confirm === 'again', body.confirm === 'replace');
          return;
        }
        this.snackBar.open(body?.reason ?? 'The import was refused.', 'Close', { duration: 6000 });
      },
    });
  }
}
