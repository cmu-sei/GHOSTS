import { Component, inject, OnInit, signal } from '@angular/core';
import { ChangeDetectionStrategy } from '@angular/core';
import { StatusService, ConfigService } from '../../../core/services';
import { ApiStatus } from '../../../core/models';

@Component({
  selector: 'app-footer',
  imports: [],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <footer class="footer">
      <span class="notice">
        @if (status()) {
          GHOSTS v{{ status()?.version }} ({{ status()?.versionFile }} on dotnet {{ status()?.versionEnvironment }}).
        } @else {
          GHOSTS.
        }
      </span>
      <span class="notice links">
        <a [href]="swaggerUrl()" target="_blank">API Swagger</a>
        <a [href]="grafanaUrl()" target="_blank">Grafana</a>
        <a href="https://cmu-sei.github.io/GHOSTS/" target="_blank">Docs</a>
        <a class="repo" href="https://github.com/cmu-sei/GHOSTS" target="_blank" rel="noopener"
           aria-label="GHOSTS on GitHub" title="GHOSTS on GitHub"><svg viewBox="0 0 16 16" aria-hidden="true"><path fill="currentColor" d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-3.58-8-8-8z"/></svg></a>
        CERT © 2017. All rights reserved.
      </span>
    </footer>
  `,
  styles: [`
    :host { display: block; flex: none; }
    .footer {
      display: flex; align-items: center; justify-content: space-between; gap: 16px;
      min-height: 24px; padding: 0 16px;
      border-top: 1px solid var(--gh-line); background: var(--gh-surface);
      font-size: 11px; color: var(--gh-faint);
    }
    .notice { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .links { display: flex; align-items: center; gap: 12px; }
    .links a { color: var(--gh-faint); text-decoration: none; }
    .links a:hover { color: var(--gh-ink); }
    .repo { display: inline-flex; }
    .repo svg { width: 12px; height: 12px; }
  `]
})
export class FooterComponent implements OnInit {
  private readonly statusService = inject(StatusService);
  private readonly configService = inject(ConfigService);

  protected readonly status = signal<ApiStatus | null>(null);
  protected readonly grafanaUrl = signal<string>('');
  protected readonly swaggerUrl = signal<string>('');

  ngOnInit(): void {
    this.loadStatus();
    this.constructGrafanaUrl();
    this.constructSwaggerUrl();
  }

  private loadStatus(): void {
    this.statusService.getStatus().subscribe({
      next: (status) => {
        this.status.set(status);
      },
      error: () => {
        // Silently fail - footer will display without version info
      }
    });
  }

  private constructGrafanaUrl(): void {
    // Construct Grafana URL based on current host
    const protocol = window.location.protocol;
    const hostname = window.location.hostname;
    const grafanaUrl = `${protocol}//${hostname}:3000`;
    this.grafanaUrl.set(grafanaUrl);
  }

  private constructSwaggerUrl(): void {
    // Construct Swagger URL based on API URL
    const apiUrl = this.configService.apiUrl;
    // Remove '/api' from the end if present
    const baseUrl = apiUrl.replace(/\/api$/, '');
    const swaggerUrl = `${baseUrl}/swagger`;
    this.swaggerUrl.set(swaggerUrl);
  }
}
