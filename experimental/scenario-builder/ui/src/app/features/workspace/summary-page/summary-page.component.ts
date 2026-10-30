import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { BuilderApiService, errorMessage } from '../../../core/services/builder-api.service';
import { GatePanelComponent } from '../gate-panel/gate-panel.component';
import { WorkspaceStore } from '../workspace.store';

const FIELD = 'executiveSummary';

/** The executive summary: drafted by the agent or written here, then approved by the owner at stage 2's gate. */
@Component({
  selector: 'app-summary-page',
  standalone: true,
  imports: [MatSnackBarModule, GatePanelComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './summary-page.component.html',
  styleUrls: ['./summary-page.component.scss']
})
export class SummaryPageComponent {
  protected readonly store = inject(WorkspaceStore);
  private readonly api = inject(BuilderApiService);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly saved = signal('');
  protected readonly text = signal('');
  protected readonly dueAfter = computed(() => this.store.kind()?.drafts.find(d => d.field === FIELD)?.dueAfterStage ?? 2);
  protected readonly isGateStage = computed(() => this.store.progress()?.stage === this.dueAfter());

  constructor() {
    effect(() => {
      // Reload whenever the scenario changes underneath, such as the agent drafting it.
      this.store.progress();
      this.load();
    });
  }

  protected save(): void {
    this.api.draft(this.store.scenarioId(), FIELD, this.text()).subscribe({
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

  protected typed(event: Event): void {
    this.text.set((event.target as HTMLTextAreaElement).value);
  }

  private load(): void {
    const id = this.store.scenarioId();
    if (!id) {
      return;
    }
    this.api.pull(id).subscribe(doc => {
      const summary = ((doc['scenario'] as Record<string, string> | undefined)?.[FIELD]) ?? '';
      // Keep unsaved typing; take the stored text when nothing is being edited.
      if (this.text() === this.saved()) {
        this.text.set(summary);
      }
      this.saved.set(summary);
    });
  }
}
