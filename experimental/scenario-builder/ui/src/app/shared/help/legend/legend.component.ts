import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { HelpService } from '../help.service';

/** The ledger's marks in words: stated, proposed, open. Each mark has its word, so colour is never the only cue. */
@Component({
  selector: 'app-legend',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span class="key"><span class="mark stated" aria-hidden="true"></span>Stated</span>
    <span class="key"><span class="mark proposed" aria-hidden="true"></span>Proposed</span>
    <span class="key"><span class="mark open" aria-hidden="true"></span>Open</span>
    <button type="button" class="what" (click)="help.show('how', 'provenance')">What these mean</button>`,
  styles: [`
    :host { display: inline-flex; align-items: center; gap: 14px; font-size: 11.5px; color: var(--sb-muted); }
    .key { display: inline-flex; align-items: center; gap: 6px; }
    .mark { width: 8px; height: 8px; border-radius: 50%; box-sizing: border-box; }
    .stated { background: var(--sb-person); }
    .proposed { background: var(--sb-agent); }
    .open { border: 1.5px solid var(--sb-faint); }
    .what {
      padding: 0; border: 0; background: none; cursor: pointer; font: inherit; color: var(--sb-muted);
      text-decoration: underline dotted; text-underline-offset: 3px; text-decoration-color: var(--sb-faint);
      &:hover { color: var(--sb-person); text-decoration-color: var(--sb-person); }
    }
  `]
})
export class LegendComponent {
  protected readonly help = inject(HelpService);
}
