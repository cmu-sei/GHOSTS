import { Component, signal } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { MatMenuModule } from '@angular/material/menu';
import { ChangeDetectionStrategy } from '@angular/core';
import { FooterComponent } from '../footer/footer.component';

interface NavItem {
  label: string;
  path?: string;
  icon: string;
  children?: NavItem[];
  target?: string;
}

@Component({
  selector: 'app-navigation',
  imports: [
    RouterLink,
    RouterLinkActive,
    MatToolbarModule,
    MatButtonModule,
    MatMenuModule,
    FooterComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <mat-toolbar color="primary" class="ghosts-toolbar mat-elevation-z4">
      <a [routerLink]="'/'" class="app-title">
        <img src="/ghosts.png" alt="GHOSTS" class="logo">
        GHOSTS
      </a>

      <nav class="nav-links">
        @for (item of navItems(); track item.path || item.label) {
          @if (item.children && item.children.length > 0) {
            <button
              mat-button
              [matMenuTriggerFor]="menu"
              class="nav-link">
              <i class="fas {{item.icon}}"></i>
              <span>{{item.label}}</span>
              <i class="fas fa-chevron-down dropdown-icon"></i>
            </button>
            <mat-menu #menu="matMenu">
              @for (child of item.children; track child.path) {
                <a mat-menu-item [routerLink]="child.path" [target]="child.target || '_self'">
                  <i class="fas {{child.icon}}"></i>
                  <span>{{child.label}}</span>
                </a>
              }
            </mat-menu>
          } @else {
            <a
              mat-button
              [routerLink]="item.path"
              routerLinkActive="active"
              [routerLinkActiveOptions]="{ exact: false }"
              class="nav-link">
              <i class="fas {{item.icon}}"></i>
              <span>{{item.label}}</span>
            </a>
          }
        }
      </nav>
    </mat-toolbar>

    <div class="main-content">
      <div class="content-wrapper">
        <ng-content></ng-content>
      </div>
      <app-footer></app-footer>
    </div>
  `,
  styles: [`
    .ghosts-toolbar.mat-toolbar.mat-primary {
      --mat-toolbar-container-background-color: #1b5e20;
      --mat-toolbar-container-text-color: #ffffff;
      --mdc-top-app-bar-container-color: #1b5e20;
      --mdc-top-app-bar-icon-ink-color: #ffffff;
      --mdc-top-app-bar-title-text-color: #ffffff;
      --mat-toolbar-standard-height: 64px;
      background-image: linear-gradient(90deg, #2e7d32, #2e7d32, #1b5e20);
      background-color: #1b5e20;
      color: #ffffff;
      display: flex;
      align-items: center;
      gap: 16px;
    }

    .ghosts-toolbar.mat-toolbar.mat-primary .mat-mdc-icon-button {
      color: inherit;
    }

    .logo {
      filter: brightness(0) invert(1);
    }

    .app-title {
      display: flex;
      align-items: center;
      gap: 12px;
      font-size: 20px;
      font-weight: 300;
      letter-spacing: 0.08em;
      text-transform: uppercase;
      color: inherit;
      white-space: nowrap;
      text-decoration: none;
      transition: opacity 0.2s;
    }

    .app-title:hover {
      opacity: 0.9;
    }

    .logo {
      height: 32px;
      width: 32px;
    }

    .mat-mdc-button {
      font-weight: 300 !important;
    }

    .nav-links {
      display: flex;
      align-items: center;
      gap: 4px;
      flex: 1;
      overflow-x: auto;
      margin: 0;
      padding: 0;
      border: none;
      outline: none;
      scrollbar-width: none; /* Firefox */
    }

    .nav-links::-webkit-scrollbar {
      display: none; /* Chrome, Safari, Edge */
    }

    .nav-link {
      color: rgba(255, 255, 255, 0.87) !important;
      white-space: nowrap;
      font-size: 14px;
    }

    .nav-link i {
      margin-right: 6px;
      font-size: 16px;
    }

    .nav-link.active {
      background-color: rgba(255, 255, 255, 0.15) !important;
      color: #ffffff !important;
    }

    .nav-link:hover {
      background-color: rgba(255, 255, 255, 0.1) !important;
    }

    .dropdown-icon {
      margin-left: 4px;
      font-size: 10px;
      opacity: 0.8;
    }

    .main-content {
      display: flex;
      flex-direction: column;
      min-height: calc(100vh - 64px);
    }

    .content-wrapper {
      flex: 1;
      padding: 20px;
    }

    @media (max-width: 1024px) {
      .nav-links {
        gap: 2px;
      }

      .nav-link {
        font-size: 13px;
        padding: 0 8px;
      }

      .nav-link span {
        display: none;
      }

      .nav-link i {
        margin-right: 0;
        font-size: 18px;
      }
    }
  `]
})
export class NavigationComponent {
  protected readonly navItems = signal<NavItem[]>([
    { label: 'Scenarios', path: '/scenarios', icon: 'fa-file-alt' },
    { label: 'Executions', path: '/executions', icon: 'fa-play' },
    { label: 'Machines', path: '/machines', icon: 'fa-desktop' },
    { label: 'Machine Groups', path: '/machine-groups', icon: 'fa-users-cog' },
    { label: 'Timelines', path: '/timelines', icon: 'fa-stream' },
    { label: 'NPCs', path: '/npcs', icon: 'fa-user' },
    { label: 'Animations', path: '/animations', icon: 'fa-play-circle' },
    {
      label: 'Activities',
      icon: 'fa-chart-bar',
      children: [
        { label: 'Overview', path: '/activities', icon: 'fa-list' },
        { label: 'Live Network', path: '/activities/dynamic', icon: 'fa-project-diagram', target: '_blank' }
      ]
    },
    { label: 'Social', path: '/social', icon: 'fa-users' }
  ]);
}
