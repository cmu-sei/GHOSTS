import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { MatMenuModule } from '@angular/material/menu';
import { ChangeDetectionStrategy } from '@angular/core';
import { FooterComponent } from '../footer/footer.component';
import { ThemeService } from '../../../core/services/theme.service';

interface NavItem {
  label: string;
  path?: string;
  icon: string;
  children?: NavItem[];
  target?: string;
  rightAlign?: boolean;
}

@Component({
  selector: 'app-navigation',
  imports: [
    RouterLink,
    RouterLinkActive,
    MatMenuModule,
    FooterComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="bar">
      <a [routerLink]="'/'" class="brand">
        <span class="mark" aria-hidden="true"></span>
        GHOSTS
      </a>

      <nav class="tabs">
        @for (item of navItems(); track item.path || item.label) {
          @if (item.rightAlign) {
            <span class="spacer"></span>
          }
          @if (item.children && item.children.length > 0) {
            <div class="tab-group" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: false }">
              <a class="tab" [routerLink]="item.path" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: false }">
                <i class="fas {{item.icon}}"></i>
                <span>{{item.label}}</span>
              </a>
              <button type="button" class="tab caret" [matMenuTriggerFor]="menu" [attr.aria-label]="item.label + ' menu'">
                <i class="fas fa-chevron-down"></i>
              </button>
              <mat-menu #menu="matMenu">
                @for (child of item.children; track child.path) {
                  <a mat-menu-item [routerLink]="child.path" [target]="child.target || '_self'">
                    <i class="fas {{child.icon}}"></i>
                    <span>{{child.label}}</span>
                  </a>
                }
              </mat-menu>
            </div>
          } @else {
            <a class="tab" [routerLink]="item.path" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: false }">
              <i class="fas {{item.icon}}"></i>
              <span>{{item.label}}</span>
            </a>
          }
        }
      </nav>

      <button type="button" class="theme" [attr.aria-label]="themeLabel()" [title]="themeLabel()" (click)="theme.toggle()">
        @if (theme.theme() === 'dark') {
          <svg viewBox="0 0 16 16" aria-hidden="true"><circle cx="8" cy="8" r="3" /><path d="M8 1.5v1.6M8 12.9v1.6M1.5 8h1.6M12.9 8h1.6M3.4 3.4l1.1 1.1M11.5 11.5l1.1 1.1M3.4 12.6l1.1-1.1M11.5 4.5l1.1-1.1" /></svg>
        } @else {
          <svg viewBox="0 0 16 16" aria-hidden="true"><path d="M13.5 9.6A5.6 5.6 0 0 1 6.4 2.5a5.6 5.6 0 1 0 7.1 7.1Z" /></svg>
        }
      </button>
    </header>

    <div class="main-content">
      <div class="content-wrapper">
        <ng-content></ng-content>
      </div>
      <app-footer></app-footer>
    </div>
  `,
  styles: [`
    :host {
      display: flex;
      flex-direction: column;
      min-height: 100vh;
    }

    .bar {
      display: flex;
      align-items: center;
      gap: 16px;
      height: 48px;
      padding: 0 16px;
      background: var(--gh-surface);
      border-bottom: 1px solid var(--gh-line);
    }

    .brand {
      display: flex;
      align-items: center;
      gap: 8px;
      font-weight: 600;
      color: var(--gh-ink) !important;
      text-decoration: none;
      white-space: nowrap;
    }

    .mark {
      width: 14px;
      height: 14px;
      border-radius: 4px;
      background: linear-gradient(135deg, var(--gh-brand-light) 0 50%, var(--gh-brand-dark) 50% 100%);
    }

    .tabs {
      display: flex;
      align-items: stretch;
      gap: 2px;
      flex: 1;
      height: 100%;
      overflow-x: auto;
      scrollbar-width: none; /* Firefox */
    }

    .tabs::-webkit-scrollbar {
      display: none; /* Chrome, Safari, Edge */
    }

    .spacer {
      flex: 1;
    }

    .tab-group {
      display: flex;
      align-items: stretch;
    }

    .tab {
      display: flex;
      align-items: center;
      gap: 6px;
      padding: 0 10px;
      border: 0;
      border-bottom: 2px solid transparent;
      background: none;
      font: 500 var(--gh-text) var(--gh-font);
      color: var(--gh-muted) !important;
      text-decoration: none;
      white-space: nowrap;
      cursor: pointer;
      transition: color 120ms, border-color 120ms;
    }

    .tab i {
      font-size: 12px;
    }

    .tab:hover {
      color: var(--gh-ink) !important;
    }

    .tab.active,
    .tab-group.active .caret {
      color: var(--gh-ink) !important;
      border-bottom-color: var(--gh-accent);
    }

    .tab-group .tab:first-child {
      padding-right: 4px;
    }

    .caret {
      padding: 0 8px 0 4px;
    }

    .caret i {
      font-size: 9px;
    }

    ::ng-deep .mat-mdc-menu-item i {
      margin-right: 12px;
      color: var(--gh-muted);
    }

    .theme {
      display: grid;
      place-items: center;
      flex: none;
      width: 28px;
      height: 28px;
      padding: 0;
      border: 1px solid var(--gh-line);
      border-radius: 50%;
      background: var(--gh-surface);
      color: var(--gh-muted);
      cursor: pointer;
      transition: border-color 120ms, color 120ms, background 120ms;
    }

    .theme:hover {
      border-color: var(--gh-accent);
      color: var(--gh-accent);
      background: var(--gh-accent-soft);
    }

    .theme svg {
      width: 14px;
      height: 14px;
      fill: none;
      stroke: currentColor;
      stroke-width: 1.5;
      stroke-linecap: round;
      stroke-linejoin: round;
    }

    .main-content {
      display: flex;
      flex-direction: column;
      flex: 1;
    }

    .content-wrapper {
      flex: 1;
      padding: 20px;
    }

    @media (max-width: 1024px) {
      .tab span {
        display: none;
      }

      .tab i {
        font-size: 15px;
      }
    }
  `]
})
export class NavigationComponent {
  protected readonly theme = inject(ThemeService);
  protected readonly themeLabel = computed(() => (this.theme.theme() === 'dark' ? 'Switch to light mode' : 'Switch to dark mode'));

  protected readonly navItems = signal<NavItem[]>([
    { label: 'Objectives', path: '/objectives', icon: 'fa-bullseye' },
    { label: 'Scenarios', path: '/scenarios', icon: 'fa-file-alt' },
    { label: 'Executions', path: '/executions', icon: 'fa-play' },
    { label: 'Assessment', path: '/assessment', icon: 'fa-clipboard-check' },
    {
      label: 'NPCs',
      path: '/npcs',
      icon: 'fa-user',
      rightAlign: true,
      children: [
        { label: 'Activities Overview', path: '/activities', icon: 'fa-list' },
        { label: 'Live Network', path: '/activities/dynamic', icon: 'fa-project-diagram', target: '_blank' },
        { label: 'Social', path: '/social', icon: 'fa-users' }
      ]
    },
    {
      label: 'Timelines',
      path: '/timelines',
      icon: 'fa-stream',
      children: [
        { label: 'Machines', path: '/machines', icon: 'fa-desktop' },
        { label: 'Machine Groups', path: '/machine-groups', icon: 'fa-users-cog' }
      ]
    },
    { label: 'Workflows', path: '/n8n-workflows', icon: 'fa-project-diagram' }
  ]);
}
