import { ChangeDetectionStrategy, Component, effect, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { BuilderApiService } from '../../../core/services/builder-api.service';
import { LogEntry } from '../../../core/models/builder.model';
import { labelFor } from '../../../shared/labels/question-label';
import { WorkspaceStore } from '../workspace.store';

/** The decision log: every answer, draft, proposal and approval, and who made it. */
@Component({
  selector: 'app-log-page',
  standalone: true,
  imports: [DatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './log-page.component.html',
  styleUrls: ['./log-page.component.scss']
})
export class LogPageComponent {
  private readonly store = inject(WorkspaceStore);
  private readonly api = inject(BuilderApiService);

  protected readonly entries = signal<LogEntry[]>([]);

  protected label(target: string): string {
    return labelFor(target, this.store.kind());
  }

  protected show(value: unknown): string {
    return Array.isArray(value) ? value.join('; ') : String(value);
  }

  constructor() {
    effect(() => {
      this.store.progress();
      const id = this.store.scenarioId();
      if (id) {
        this.api.getLog(id).subscribe(entries => this.entries.set([...entries].reverse()));
      }
    });
  }
}
