import { ChangeDetectionStrategy, Component, effect, inject, input, signal } from '@angular/core';
import { forkJoin } from 'rxjs';
import { BuilderApiService } from '../../../core/services/builder-api.service';
import { ScenarioType, ScenarioTypeSummary } from '../../../core/models/builder.model';
import { WELCOME_STEPS } from '../../../shared/help/help-content';
import { HelpService } from '../../../shared/help/help.service';

/** The first thing a new designer reads: what the builder does, in four steps, and which scenario type to pick. */
@Component({
  selector: 'app-welcome',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './welcome.component.html',
  styleUrls: ['./welcome.component.scss']
})
export class WelcomeComponent {
  private readonly api = inject(BuilderApiService);
  protected readonly help = inject(HelpService);

  readonly scenarioTypes = input<ScenarioTypeSummary[]>([]);
  protected readonly steps = WELCOME_STEPS;
  protected readonly types = signal<ScenarioType[]>([]);

  constructor() {
    // Each type in full, for its stage count, topology and description.
    effect(() => {
      const summaries = this.scenarioTypes();
      if (!summaries.length) {
        return;
      }
      forkJoin(summaries.map(s => this.api.getScenarioType(s.id, s.version))).subscribe({
        next: types => this.types.set(types),
        error: () => this.types.set([])
      });
    });
  }

  protected questions(type: ScenarioType): number {
    return type.stages.reduce((n, s) => n + s.questions.length, 0);
  }
}
