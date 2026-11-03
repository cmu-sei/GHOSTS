import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { Finding, Gate } from '../../../core/models/builder.model';

/**
 * What stands between the open stage and the next one, and the owner's next step when there is one.
 * A closed stage whose approval went stale is offered for approval again, whatever else is open.
 */
@Component({
  selector: 'app-gate-panel',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './gate-panel.component.html',
  styleUrls: ['./gate-panel.component.scss']
})
export class GatePanelComponent {
  readonly gate = input.required<Gate>();
  /** The open stage. */
  readonly stage = input(0);
  readonly lastStage = input(false);
  /** The stage to approve: the open one, or a closed one approved again. */
  readonly approve = output<number>();
  readonly advance = output<void>();

  protected readonly staleClosed = computed<Finding[]>(() =>
    this.gate().findings.filter(f => f.code === 'APPROVAL_STALE' && f.stage !== this.stage()));

  protected readonly openFindings = computed<Finding[]>(() =>
    this.gate().findings.filter(f => !this.staleClosed().includes(f)));

  /** The open stage's approval is offered only once nothing else in it is in the way, as the API reports it. */
  protected readonly waitsForApproval = computed(() => {
    const findings = this.openFindings();
    return findings.length > 0 && findings.every(f => f.code.startsWith('APPROVAL_'));
  });

  /** Open questions are counted, not listed: each one is already a row below. */
  protected readonly openCount = computed(() => this.openFindings().filter(f => f.code === 'QUESTION_OPEN').length);

  /** What no row shows, such as a draft that isn't written. */
  protected readonly otherFindings = computed(() => this.openFindings().filter(f => f.code !== 'QUESTION_OPEN'));
}
