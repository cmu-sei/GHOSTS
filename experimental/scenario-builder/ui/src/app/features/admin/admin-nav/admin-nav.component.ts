import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

/** Admin's sections, in the top bar of every Admin page, styled like a scenario's page links. */
@Component({
  selector: 'app-admin-nav',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <nav class="pages" aria-label="Admin">
      <a routerLink="/admin/scenario-types" routerLinkActive="active" ariaCurrentWhenActive="page">Scenario Types</a>
      <a routerLink="/admin/libraries" routerLinkActive="active" ariaCurrentWhenActive="page">Template Libraries</a>
    </nav>`,
  styles: [`
    :host { display: flex; height: 100%; }
    .pages { display: flex; gap: 4px; height: 100%; }
    a {
      display: flex; align-items: center; padding: 0 12px;
      color: var(--sb-muted); font-weight: 500; text-decoration: none; border-bottom: 2px solid transparent;
      &:hover { color: var(--sb-ink); }
      &.active { color: var(--sb-ink); border-bottom-color: var(--sb-person); }
    }
  `]
})
export class AdminNavComponent {}
