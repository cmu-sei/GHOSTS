import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AnswerType, ScenarioType, ScenarioTypeDetail, ScenarioTypeQuestion, ScenarioTypeStage } from '../../../core/models/builder.model';
import { AdminApiService } from '../../../core/services/admin-api.service';
import { errorMessage } from '../../../core/services/builder-api.service';
import { TopBarComponent } from '../../../shared/components/top-bar/top-bar.component';
import { AdminNavComponent } from '../admin-nav/admin-nav.component';

/**
 * One scenario type: its draft as an outline of stages and questions (or as JSON, for power users), the model that can draft
 * it, and its published versions. Edits stay here until Save draft; scenarios only ever get a published version.
 * Opened with ?draft=<model>, it drafts the whole scenario type with that model as soon as it loads.
 */
@Component({
  selector: 'app-scenario-type-page',
  standalone: true,
  imports: [DatePipe, TopBarComponent, AdminNavComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './scenario-type-page.component.html',
  styleUrls: ['./scenario-type-page.component.scss']
})
export class ScenarioTypePageComponent implements OnInit {
  private readonly admin = inject(AdminApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly id = this.route.snapshot.paramMap.get('id')!;
  protected readonly detail = signal<ScenarioTypeDetail | null>(null);
  /** The draft as edited here, until it's saved. */
  protected readonly draft = signal<ScenarioType | null>(null);
  protected readonly dirty = signal(false);
  protected readonly tab = signal<'outline' | 'json'>('outline');
  protected readonly json = signal('');
  protected readonly models = signal<string[]>([]);
  protected readonly model = signal('');
  /** What the model is doing, while it drafts. */
  protected readonly drafting = signal<string | null>(null);
  /** The stage the model last failed to write, to try again from. */
  protected readonly failed = signal<number | null>(null);
  protected readonly note = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);

  protected readonly answerTypes: { type: AnswerType; label: string }[] = [
    { type: 'text', label: 'Text' },
    { type: 'choice', label: 'Choice' },
    { type: 'list', label: 'List' }
  ];

  ngOnInit(): void {
    const draftWith = this.route.snapshot.queryParamMap.get('draft');
    this.admin.models().subscribe(models => {
      this.models.set(models);
      this.model.set(draftWith ?? models[0] ?? '');
    });
    this.admin.scenarioType(this.id).subscribe({
      next: detail => {
        this.show(detail);
        if (draftWith) {
          this.router.navigate([], { queryParams: {}, replaceUrl: true });
          this.draftAll(draftWith, false);
        }
      },
      error: e => this.error.set(errorMessage(e))
    });
  }

  protected value(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }

  protected checked(event: Event): boolean {
    return (event.target as HTMLInputElement).checked;
  }

  protected exportUrl(): string {
    return this.admin.scenarioTypeExportUrl(this.id);
  }

  // Editing the outline: every change makes a new draft, saved with Save draft.

  protected edit(change: (scenarioType: ScenarioType) => ScenarioType): void {
    this.draft.update(scenarioType => scenarioType && change(scenarioType));
    this.dirty.set(true);
  }

  protected setSetting(patch: Partial<ScenarioType>): void {
    this.edit(k => ({ ...k, ...patch }));
  }

  protected setStage(i: number, patch: Partial<ScenarioTypeStage>): void {
    this.edit(k => ({ ...k, stages: k.stages.map((s, n) => (n === i ? { ...s, ...patch } : s)) }));
  }

  protected addStage(): void {
    this.edit(k => ({ ...k, stages: [...k.stages, { number: k.stages.length, title: 'New Stage', purpose: '', questions: [], gate: {} }] }));
  }

  /** A draft due after a removed stage moves to the stage before it. */
  protected removeStage(i: number): void {
    const stage = this.draft()!.stages[i];
    if (stage.questions.length && !confirm(`Remove "${stage.title}" and its ${stage.questions.length} questions from the draft?`)) {
      return;
    }
    this.edit(k => {
      const stages = k.stages.filter((_, n) => n !== i).map((s, n) => ({ ...s, number: n }));
      const drafts = stages.length
        ? k.drafts.map(d => ({ ...d, dueAfterStage: Math.max(0, Math.min(d.dueAfterStage >= i ? d.dueAfterStage - 1 : d.dueAfterStage, stages.length - 1)) }))
        : [];
      return { ...k, stages, drafts };
    });
  }

  protected setQuestion(i: number, j: number, patch: Partial<ScenarioTypeQuestion>): void {
    this.setStage(i, { questions: this.draft()!.stages[i].questions.map((q, n) => (n === j ? { ...q, ...patch } : q)) });
  }

  protected setOptions(i: number, j: number, text: string): void {
    this.setQuestion(i, j, { options: text.split(',').map(o => o.trim()).filter(o => o) });
  }

  /** The builder fills in a new question's id, label and place in the document when it's saved. */
  protected addQuestion(i: number): void {
    this.setStage(i, { questions: [...this.draft()!.stages[i].questions, { id: '', text: '', why: '', answer: 'text', path: '' }] });
  }

  protected removeQuestion(i: number, j: number): void {
    this.setStage(i, { questions: this.draft()!.stages[i].questions.filter((_, n) => n !== j) });
  }

  protected showJson(): void {
    this.json.set(JSON.stringify(this.draft(), null, 2));
    this.tab.set('json');
  }

  // Saving, publishing and the rest.

  protected save(): void {
    this.store(this.draft()!, 'Saved the draft.');
  }

  protected saveJson(): void {
    let scenarioType: ScenarioType;
    try {
      scenarioType = JSON.parse(this.json());
    } catch (e) {
      this.error.set(`That isn't JSON: ${(e as Error).message}`);
      return;
    }
    this.store(scenarioType, 'Saved the draft from its JSON.');
  }

  protected publish(): void {
    this.admin.publishScenarioType(this.id).subscribe({
      next: version => {
        this.note.set(`Published version ${version.version}. New scenarios get it; scenarios already made stay on theirs.`);
        this.reload();
      },
      error: e => this.error.set(errorMessage(e))
    });
  }

  protected retire(retired: boolean): void {
    this.admin.retireScenarioType(this.id, retired).subscribe({
      next: detail => {
        this.detail.set(detail);
        this.note.set(retired ? 'Retired: new scenarios can’t use it. Scenarios on it carry on.' : 'Offered again for new scenarios.');
      },
      error: e => this.error.set(errorMessage(e))
    });
  }

  protected remove(): void {
    if (!confirm(`Delete "${this.draft()!.name}"? It has never been published, so no scenario is on it.`)) {
      return;
    }
    this.admin.deleteScenarioType(this.id).subscribe({
      next: () => this.router.navigate(['/admin/scenario-types']),
      error: e => this.error.set(errorMessage(e))
    });
  }

  // Drafting with the model. Each step saves the draft on the server, so the outline fills in as it goes.

  /** Plans every stage, then writes each one's questions in turn. Asked first when it would replace stages. */
  protected async draftAll(model: string, ask = true): Promise<void> {
    if (ask && this.draft()!.stages.length && !confirm('Replace every stage and question in the draft with what the model writes?')) {
      return;
    }
    await this.run(async () => {
      this.drafting.set(`${model} is planning the stages…`);
      this.show(await firstValueFrom(this.admin.draftStages(this.id, model)));
      await this.writeFrom(0, model);
    });
  }

  /** Writes one stage again, replacing its questions. */
  protected async redraft(i: number): Promise<void> {
    const stage = this.draft()!.stages[i];
    if (stage.questions.length && !confirm(`Replace the questions of "${stage.title}" with what the model writes?`)) {
      return;
    }
    await this.run(() => this.write(i, this.model()));
  }

  /** After a failure: the stage that failed, then every later stage still without questions. */
  protected async resume(): Promise<void> {
    const from = this.failed()!;
    await this.run(() => this.writeFrom(from, this.model()));
  }

  private async writeFrom(first: number, model: string): Promise<void> {
    const stages = this.detail()!.scenarioType.stages;
    for (let n = first; n < stages.length; n++) {
      if (n === first || !this.detail()!.scenarioType.stages[n].questions.length) {
        await this.write(n, model);
      }
    }
    this.note.set('Drafted. Read it through, change what you like, then publish.');
  }

  private async write(n: number, model: string): Promise<void> {
    const stages = this.detail()!.scenarioType.stages;
    this.drafting.set(`${model} is writing stage ${n + 1} of ${stages.length}: ${stages[n].title}…`);
    try {
      this.show(await firstValueFrom(this.admin.draftStage(this.id, n, model)));
    } catch (e) {
      this.failed.set(n);
      throw e;
    }
  }

  private async run(work: () => Promise<void>): Promise<void> {
    this.error.set(null);
    this.note.set(null);
    this.failed.set(null);
    try {
      await work();
    } catch (e) {
      this.error.set(errorMessage(e));
    } finally {
      this.drafting.set(null);
    }
  }

  private store(scenarioType: ScenarioType, saved: string): void {
    this.admin.saveScenarioType(this.id, scenarioType).subscribe({
      next: detail => {
        this.show(detail);
        this.error.set(null);
        this.note.set(saved);
      },
      error: e => this.error.set(errorMessage(e))
    });
  }

  private reload(): void {
    this.admin.scenarioType(this.id).subscribe({ next: detail => this.show(detail), error: e => this.error.set(errorMessage(e)) });
  }

  private show(detail: ScenarioTypeDetail): void {
    this.detail.set(detail);
    this.draft.set(structuredClone(detail.scenarioType));
    this.json.set(JSON.stringify(detail.scenarioType, null, 2));
    this.dirty.set(false);
  }
}
