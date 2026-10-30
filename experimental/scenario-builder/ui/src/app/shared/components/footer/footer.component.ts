import { ChangeDetectionStrategy, Component } from '@angular/core';

/** The standard footer, made small: the notice, on every page. */
@Component({
  selector: 'app-footer',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <footer class="footer">
      <span class="notice">Scenario Builder. CERT © 2017. All rights reserved.</span>
    </footer>`,
  styles: [`
    :host { display: block; flex: none; }
    .footer {
      display: flex; align-items: center;
      height: 24px; padding: 0 16px;
      border-top: 1px solid var(--sb-line); background: var(--sb-surface);
      font-size: 11px; color: var(--sb-faint);
    }
    .notice { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  `]
})
export class FooterComponent {}
