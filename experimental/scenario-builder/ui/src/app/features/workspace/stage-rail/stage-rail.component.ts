import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { Kind, Progress } from '../../../core/models/builder.model';

type StageState = 'done' | 'open' | 'locked' | 'attention';

interface StageView {
  number: number;
  title: string;
  answered: number;
  total: number;
  state: StageState;
  note: string;
}

/** The gated sequence, always in view: each stage, how far along it is, and what it waits for. */
@Component({
  selector: 'app-stage-rail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './stage-rail.component.html',
  styleUrls: ['./stage-rail.component.scss']
})
export class StageRailComponent {
  readonly kind = input.required<Kind>();
  readonly progress = input.required<Progress>();
  readonly selected = input(0);
  readonly select = output<number>();

  protected readonly stages = computed<StageView[]>(() => {
    const progress = this.progress();
    const stale = new Set(progress.gate.findings.filter(f => f.code === 'APPROVAL_STALE').map(f => f.stage));
    return this.kind().stages.map(stage => {
      const answered = progress.answered.filter(a => a.stage === stage.number).length;
      const total = stage.questions.length;
      const state: StageState = stale.has(stage.number) && stage.number < progress.stage
        ? 'attention'
        : stage.number < progress.stage ? 'done' : stage.number === progress.stage ? 'open' : 'locked';
      return { number: stage.number, title: stage.title, answered, total, state, note: this.note(state, stage.number, answered, total) };
    });
  });

  private note(state: StageState, number: number, answered: number, total: number): string {
    switch (state) {
      case 'done':
        return 'Closed';
      case 'attention':
        return 'Changed since approval';
      case 'locked':
        return `Opens after stage ${number - 1}`;
      default: {
        const gate = this.progress().gate;
        if (gate.passes) {
          return this.progress().lastStage ? 'Complete' : 'Ready to close';
        }
        return answered < total ? `${total - answered} open` : 'Waiting for approval';
      }
    }
  }
}
