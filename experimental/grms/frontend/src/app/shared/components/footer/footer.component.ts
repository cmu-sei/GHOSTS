import { Component, inject, OnInit, signal, ChangeDetectionStrategy } from '@angular/core';
import { ConfigService } from '../../../core/services/config.service';

@Component({
  selector: 'app-footer',
  imports: [],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <footer class="footer">
      <div class="footer-container">
        <div class="footer-content">
          <div class="footer-left">
            <p class="footer-text">
              GHOSTS GRMS — Geopolitical Response Modeling Service.
              CERT &copy; 2017. All rights reserved.
            </p>
          </div>
          <div class="footer-right">
            <p class="footer-text">
              <a [href]="docsUrl()" class="footer-link" target="_blank">API Docs</a>
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

      .footer-left,
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
      color: #2e7d32;
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
  private readonly configService = inject(ConfigService);

  protected readonly docsUrl = signal<string>('');

  ngOnInit(): void {
    this.constructDocsUrl();
  }

  private constructDocsUrl(): void {
    try {
      const apiUrl = this.configService.apiUrl;
      const baseUrl = apiUrl.replace(/\/api$/, '');
      this.docsUrl.set(`${baseUrl}/docs`);
    } catch {
      // Config not loaded yet; fall back to relative docs path.
      this.docsUrl.set('/docs');
    }
  }
}
