import { Component, inject, OnInit, signal } from '@angular/core';
import { ChangeDetectionStrategy } from '@angular/core';
import { StatusService } from '../../../core/services';
import { ApiStatus } from '../../../core/models';

@Component({
  selector: 'app-footer',
  imports: [],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <footer class="footer">
      <div class="footer-container">
        <div class="footer-content">
          <div class="footer-left">
            @if (status()) {
              <p class="footer-text">
                GHOSTS v{{ status()?.version }}
                ({{ status()?.versionFile }} on dotnet {{ status()?.versionEnvironment }}).
                CERT © 2017. All rights reserved.
              </p>
            } @else {
              <p class="footer-text">
                GHOSTS. CERT © 2017. All rights reserved.
              </p>
            }
          </div>
          <div class="footer-right">
            <p class="footer-text">
              <a href="/swagger" class="footer-link" target="_blank">API Swagger</a>
              <span class="separator">|</span>
              <a [href]="grafanaUrl()" class="footer-link" target="_blank">Grafana</a>
              <span class="separator">|</span>
              <a href="https://github.com/cmu-sei/GHOSTS" class="footer-link" target="_blank">GitHub</a>
              <span class="separator">|</span>
              <a href="https://cmu-sei.github.io/GHOSTS/" class="footer-link" target="_blank">Docs</a>
            </p>
          </div>
        </div>
      </div>
    </footer>
  `,
  styles: [`
    .footer {
      border-top: 1px solid rgba(0, 0, 0, 0.12);
      padding: 1rem 20px;
      margin-top: 1.5rem;
      background-color: #fafafa;
      width: 100%;
      box-sizing: border-box;
      overflow-x: hidden;
    }

    .footer-container {
      width: 100%;
      padding: 0;
      margin: 0;
      box-sizing: border-box;
    }

    .footer-content {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 16px;
      max-width: 100%;
      overflow: hidden;
    }

    @media (max-width: 1024px) {
      .footer-content {
        flex-wrap: wrap;
      }

      .footer-left {
        flex: 1 1 100%;
        max-width: 100%;
      }

      .footer-right {
        flex: 1 1 100%;
        max-width: 100%;
        text-align: left;
      }
    }

    @media (max-width: 768px) {
      .footer-content {
        flex-direction: column;
        text-align: center;
      }

      .footer-left,
      .footer-right {
        width: 100%;
      }
    }

    .footer-left {
      flex: 1 1 auto;
      min-width: 0;
      overflow: hidden;
    }

    .footer-right {
      flex: 0 1 auto;
      text-align: right;
      white-space: nowrap;
    }

    @media (max-width: 768px) {
      .footer-right {
        text-align: center;
        white-space: normal;
      }

      .footer-left .footer-text {
        white-space: normal;
      }
    }

    .footer-text {
      margin: 0;
      font-size: 0.875rem;
      color: rgba(0, 0, 0, 0.6);
      line-height: 1.5;
      overflow: hidden;
      text-overflow: ellipsis;
    }

    .footer-left .footer-text {
      white-space: nowrap;
    }

    .footer-link {
      color: rgba(0, 0, 0, 0.6);
      text-decoration: none;
      transition: color 0.2s ease;
    }

    .footer-link:hover {
      color: #3f51b5;
      text-decoration: underline;
    }

    .separator {
      margin: 0 8px;
      color: rgba(0, 0, 0, 0.4);
    }

    @media (max-width: 768px) {
      .separator {
        margin: 0 4px;
      }
    }
  `]
})
export class FooterComponent implements OnInit {
  private readonly statusService = inject(StatusService);

  protected readonly status = signal<ApiStatus | null>(null);
  protected readonly grafanaUrl = signal<string>('http://localhost:3000');

  ngOnInit(): void {
    this.loadStatus();
    this.constructGrafanaUrl();
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
}
