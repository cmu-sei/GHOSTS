import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { KindUpdatePlan } from '../../../core/models/builder.model';
import { BuilderApiService, errorMessage } from '../../../core/services/builder-api.service';
import { MeService } from '../../../core/services/me.service';
import { spaced } from '../../../shared/labels/question-label';
import { WorkspaceStore } from '../workspace.store';

/**
 * Above a scenario's pages when a newer version of its scenario type is published: the owner reviews what an update
 * carries over, drops and adds, then applies it. The page reloads after, so the pages and the agent read the new version.
 */
@Component({
  selector: 'app-kind-update',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './kind-update.component.html',
  styleUrls: ['./kind-update.component.scss']
})
export class KindUpdateComponent {
  protected readonly store = inject(WorkspaceStore);
  private readonly api = inject(BuilderApiService);
  private readonly me = inject(MeService).me;

  protected readonly plan = signal<KindUpdatePlan | null>(null);
  protected readonly open = signal(false);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly behind = computed(() => {
    const plan = this.plan();
    return plan !== null && plan.from !== plan.to;
  });
  protected readonly owner = computed(() => this.me()?.user === this.plan()?.owner);
  protected readonly moved = computed(() => this.plan()?.carried.filter(c => c.fromStage !== c.toStage).length ?? 0);

  protected readonly spaced = spaced;

  constructor() {
    effect(() => {
      const id = this.store.scenarioId();
      if (id && this.store.progress()?.kindVersion) {
        this.api.getKindUpdate(id).subscribe({ next: plan => this.plan.set(plan), error: () => this.plan.set(null) });
      }
    });
  }

  protected apply(): void {
    this.busy.set(true);
    this.error.set(null);
    this.api.applyKindUpdate(this.store.scenarioId(), this.plan()!.to).subscribe({
      next: () => this.reloadPage(),
      error: e => {
        this.busy.set(false);
        this.error.set(errorMessage(e));
      }
    });
  }

  /** A full reload, so the agent in this tab starts again on the new version. */
  protected reloadPage(): void {
    location.reload();
  }
}
