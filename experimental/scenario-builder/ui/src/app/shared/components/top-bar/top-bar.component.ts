import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { MeService } from '../../../core/services/me.service';

/** The thin bar across the top: the product, where you are, and whatever the page puts on the right. */
@Component({
  selector: 'app-top-bar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="bar">
      <a class="brand" routerLink="/">
        <span class="mark" aria-hidden="true"></span>
        Scenario Builder
      </a>
      @if (crumb()) {
        <span class="sep" aria-hidden="true">/</span>
        <span class="crumb">{{ crumb() }}</span>
      }
      <div class="end"><ng-content /></div>
      @if (me()?.admin) {
        <a class="admin" routerLink="/admin" routerLinkActive="active">Admin</a>
      }
    </header>`,
  styles: [`
    :host { display: block; flex: none; }
    .bar {
      display: flex; align-items: center; gap: 10px; height: 48px; padding: 0 16px;
      background: var(--sb-surface); border-bottom: 1px solid var(--sb-line);
    }
    .brand { display: flex; align-items: center; gap: 8px; font-weight: 600; color: var(--sb-ink); text-decoration: none; }
    .mark {
      width: 14px; height: 14px; border-radius: 4px;
      background: linear-gradient(135deg, var(--sb-person) 0 50%, var(--sb-agent) 50% 100%);
    }
    .sep { color: var(--sb-faint); }
    .crumb { color: var(--sb-muted); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; max-width: 40vw; }
    .end { margin-left: auto; display: flex; align-items: center; height: 100%; }
    .admin {
      padding: 4px 8px; border-radius: var(--sb-pill); font-size: var(--sb-small); font-weight: 500;
      color: var(--sb-muted); text-decoration: none;
      &:hover, &.active { color: var(--sb-ink); background: var(--sb-canvas); }
    }
  `]
})
export class TopBarComponent {
  readonly crumb = input<string | null>(null);
  protected readonly me = inject(MeService).me;
}
