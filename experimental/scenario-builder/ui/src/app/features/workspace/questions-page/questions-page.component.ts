import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { BuilderApiService, errorMessage } from '../../../core/services/builder-api.service';
import { AnswerValue } from '../../../core/models/builder.model';
import { GatePanelComponent } from '../gate-panel/gate-panel.component';
import { WorkspaceStore } from '../workspace.store';
import { QuestionRowComponent } from './question-row/question-row.component';

/** The spine of the workspace: the selected stage's questions as a ledger, with its gate on top. */
@Component({
  selector: 'app-questions-page',
  standalone: true,
  imports: [MatSnackBarModule, QuestionRowComponent, GatePanelComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './questions-page.component.html',
  styleUrls: ['./questions-page.component.scss']
})
export class QuestionsPageComponent {
  protected readonly store = inject(WorkspaceStore);
  private readonly api = inject(BuilderApiService);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly stage = computed(() => this.store.kind()?.stages[this.store.selectedStage()] ?? null);
  protected readonly answeredCount = computed(() => {
    const stage = this.stage();
    return stage ? stage.questions.filter(q => this.store.answers().has(q.id)).length : 0;
  });
  /** A closed stage changed since its approval: this page offers the renewal too. */
  protected readonly staleHere = computed(() => {
    const stage = this.stage();
    return (this.store.progress()?.gate.findings ?? []).find(f => f.code === 'APPROVAL_STALE' && f.stage === stage?.number && f.stage !== this.store.progress()?.stage);
  });

  protected save(questionId: string, value: AnswerValue): void {
    this.api.answer(this.store.scenarioId(), questionId, value).subscribe({
      next: () => this.store.refresh(),
      error: e => this.snackBar.open(errorMessage(e), 'Close')
    });
  }

  protected approve(stage: number): void {
    this.api.approve(this.store.scenarioId(), stage).subscribe({
      next: () => {
        this.store.refresh();
        this.store.approvals.next(stage);
      },
      error: e => this.snackBar.open(errorMessage(e), 'Close')
    });
  }

  protected advance(): void {
    this.api.advance(this.store.scenarioId()).subscribe({
      next: () => this.store.refresh(),
      error: e => this.snackBar.open(errorMessage(e), 'Close')
    });
  }
}
