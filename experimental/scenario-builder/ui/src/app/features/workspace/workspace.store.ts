import { Injectable, computed, inject, signal } from '@angular/core';
import { forkJoin, switchMap } from 'rxjs';
import { BuilderApiService, errorMessage } from '../../core/services/builder-api.service';
import { AnsweredView, ScenarioType, Progress } from '../../core/models/builder.model';

/**
 * One scenario's state for its workspace pages and agent panel, provided by the scenario shell.
 * Every write is followed by refresh(), so each page shows what the API now holds.
 */
@Injectable()
export class WorkspaceStore {
  private readonly api = inject(BuilderApiService);

  readonly scenarioId = signal(0);
  readonly name = signal<string | null>(null);
  /** The version of the scenario format the pull document is in, read from the document itself. */
  readonly schemaVersion = signal<string | null>(null);
  readonly scenarioType = signal<ScenarioType | null>(null);
  readonly progress = signal<Progress | null>(null);
  readonly error = signal<string | null>(null);
  /** The stage the Questions page shows. It follows the open stage whenever a new one opens. */
  readonly selectedStage = signal(0);

  readonly answers = computed(() => new Map((this.progress()?.answered ?? []).map(a => [a.id, a] as const)));

  private openStage = -1;

  load(scenarioId: number): void {
    this.scenarioId.set(scenarioId);
    this.api.getProgress(scenarioId).pipe(
      switchMap(progress => forkJoin({ progress: [progress], scenarioType: this.api.getScenarioType(progress.scenarioTypeId, progress.scenarioTypeVersion), doc: this.api.pull(scenarioId) }))
    ).subscribe({
      next: ({ progress, scenarioType, doc }) => {
        this.scenarioType.set(scenarioType);
        this.name.set(typeof doc['name'] === 'string' ? doc['name'] : null);
        this.schemaVersion.set(typeof doc['schemaVersion'] === 'string' ? doc['schemaVersion'] : null);
        this.apply(progress);
      },
      error: e => this.error.set(errorMessage(e))
    });
  }

  refresh(): void {
    this.api.getProgress(this.scenarioId()).subscribe({
      next: progress => this.apply(progress),
      error: e => this.error.set(errorMessage(e))
    });
  }

  answer(questionId: string): AnsweredView | undefined {
    return this.answers().get(questionId);
  }

  /** Shows progress just read from the API. */
  apply(progress: Progress): void {
    if (progress.stage !== this.openStage) {
      this.openStage = progress.stage;
      this.selectedStage.set(progress.stage);
    }
    this.progress.set(progress);
    this.error.set(null);
  }
}
