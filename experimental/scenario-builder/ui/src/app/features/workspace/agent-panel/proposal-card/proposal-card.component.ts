import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { ProposalView } from '../../../../core/models/builder.model';

/** A change the agent proposed to a stated answer. Only the owner applies or discards it. */
@Component({
  selector: 'app-proposal-card',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="card" [attr.data-status]="proposal().status">
      <div class="title">The agent proposes a change to {{ proposal().questionId }}</div>
      <div class="change">
        <span class="before">{{ show(proposal().before) }}</span>
        <span class="after">{{ show(proposal().after) }}</span>
      </div>
      <div class="reason">{{ proposal().reason }}</div>
      @if (proposal().status === 'pending') {
        @if (isOwner()) {
          <div class="actions">
            <button class="sb-button ghost" type="button" (click)="discard.emit()">Discard</button>
            <button class="sb-button primary" type="button" (click)="apply.emit()">Apply</button>
          </div>
        } @else {
          <div class="reason">Waiting for the owner.</div>
        }
      } @else {
        <div class="status">{{ proposal().status === 'applied' ? 'Applied' : 'Discarded' }} ({{ proposal().status }})</div>
      }
    </div>`,
  styles: [`
    .card { margin: 10px 0; padding: 12px; border: 1px solid var(--sb-agent); border-radius: var(--sb-radius); background: var(--sb-agent-soft); }
    .card[data-status='applied'], .card[data-status='discarded'] { border-color: var(--sb-line); background: var(--sb-surface); }
    .title { font-weight: 500; color: #4b2d8f; }
    .change { display: flex; flex-direction: column; gap: 2px; margin: 8px 0; overflow-wrap: anywhere; }
    .before { color: var(--sb-muted); text-decoration: line-through; }
    .after { color: var(--sb-ink); font-weight: 500; }
    .reason, .status { font-size: var(--sb-small); color: var(--sb-muted); }
    .actions { display: flex; justify-content: flex-end; gap: 6px; margin-top: 8px; }
  `]
})
export class ProposalCardComponent {
  readonly proposal = input.required<ProposalView>();
  readonly isOwner = input(true);
  readonly apply = output<void>();
  readonly discard = output<void>();

  protected show(value: unknown): string {
    return Array.isArray(value) ? value.join('; ') : value === null || value === undefined ? '—' : String(value);
  }
}
