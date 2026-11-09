import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { Gate } from '../../../core/models/builder.model';

/** What stands between the open stage and the next one, and the owner's next step when there is one. */
@Component({
  selector: 'app-gate-panel',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './gate-panel.component.html',
  styleUrls: ['./gate-panel.component.scss']
})
export class GatePanelComponent {
  readonly gate = input.required<Gate>();
  readonly lastStage = input(false);
  readonly advance = output<void>();

  /** Open questions are counted, not listed: each one is already a row below. */
  protected readonly openCount = computed(() => this.gate().findings.filter(f => f.code === 'QUESTION_OPEN').length);

  /** What no row shows, such as a draft that isn't written. */
  protected readonly otherFindings = computed(() => this.gate().findings.filter(f => f.code !== 'QUESTION_OPEN'));
}
