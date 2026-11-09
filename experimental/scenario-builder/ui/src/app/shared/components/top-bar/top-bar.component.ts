import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { MeService } from '../../../core/services/me.service';
import { HelpService } from '../../help/help.service';

/** The thin bar across the top: the product, where you are, whatever the page puts on the right, and the Guide. */
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
      <button type="button" class="guide" data-tour="guide" title="Guide (?)" [attr.aria-expanded]="help.open()" (click)="help.toggle()">
        <span class="glyph" aria-hidden="true">?</span>Guide
      </button>
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
    .guide {
      display: flex; align-items: center; gap: 6px; height: 28px; padding: 0 10px 0 5px;
      border: 1px solid var(--sb-line); border-radius: 999px; background: var(--sb-surface);
      font: 500 var(--sb-small) var(--sb-font); color: var(--sb-muted); cursor: pointer;
      transition: border-color 120ms, color 120ms, background 120ms;
      &:hover, &[aria-expanded='true'] { border-color: var(--sb-person); color: var(--sb-person); background: var(--sb-person-soft); }
      &:hover .glyph, &[aria-expanded='true'] .glyph { background: var(--sb-person); color: #ffffff; }
    }
    .glyph {
      display: grid; place-items: center; width: 18px; height: 18px; border-radius: 50%;
      background: var(--sb-canvas); font: 600 11px/1 var(--sb-serif); color: var(--sb-ink);
      transition: background 120ms, color 120ms;
    }
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
  protected readonly help = inject(HelpService);
}
