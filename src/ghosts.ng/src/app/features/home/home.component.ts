import { Component, inject, OnInit, signal } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDividerModule } from '@angular/material/divider';
import { ChangeDetectionStrategy } from '@angular/core';
import { DatePipe, JsonPipe } from '@angular/common';
import { ApiStatus } from '../../core/models';
import { StatusService } from '../../core/services';

@Component({
  selector: 'app-home',
  imports: [
    MatCardModule,
    MatProgressSpinnerModule,
    MatDividerModule,
    DatePipe,
    JsonPipe
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="home-container">
      <div class="welcome-section">
        <div class="logo-container">
          <img src="ghosts.png" alt="GHOSTS Logo" class="ghost-logo" />
        </div>

        <h1>Welcome to GHOSTS</h1>

        <div class="description">
          <p>
            GHOSTS clients simulates what anyone might do at a computer, creating documents,
            browsing websites, and downloading files.
          </p>
          <p>
            Plus, GHOSTS drives all sorts of popular applications on many versions of Windows
            and Linux machines.
          </p>
          <p>
            Whether you're a friendly administrator or a powerful cyber adversary, GHOSTS can
            replicate that agent's expected behavior.
          </p>
        </div>

        <mat-divider class="section-divider"></mat-divider>

        <div class="quick-links">
          <h3>Quick Links</h3>
          <div class="links-grid">
            <a href="https://cmu-sei.github.io/GHOSTS/" target="_blank" rel="noopener noreferrer" class="link-item">
              <span class="link-icon">📖</span>
              <span>GHOSTS Documentation</span>
            </a>
            <a href="https://github.com/cmu-sei/GHOSTS" target="_blank" rel="noopener noreferrer" class="link-item">
              <span class="link-icon">💻</span>
              <span>GitHub Repository</span>
            </a>
          </div>
        </div>
      </div>

      <div class="status-section">
        <mat-card>
          <mat-card-header>
            <mat-card-title>Current Server Status</mat-card-title>
          </mat-card-header>
          <mat-card-content>
            @if (loading()) {
              <div class="loading-container">
                <mat-spinner diameter="40"></mat-spinner>
              </div>
            } @else if (error()) {
              <div class="error-container">
                <p class="error-message">{{ error() }}</p>
                <button mat-button (click)="loadStatus()">Retry</button>
              </div>
            } @else if (status()) {
              <div class="status-data">
                <div class="status-item">
                  <span class="status-label">Version:</span>
                  <span class="status-value">{{ status()?.version }}</span>
                </div>
                <div class="status-item">
                  <span class="status-label">File Version:</span>
                  <span class="status-value">{{ status()?.versionFile }}</span>
                </div>
                <mat-divider></mat-divider>
                <div class="status-item highlight">
                  <span class="status-label">Machines:</span>
                  <span class="status-value">{{ status()?.machines }}</span>
                </div>
                <div class="status-item highlight">
                  <span class="status-label">Groups:</span>
                  <span class="status-value">{{ status()?.groups }}</span>
                </div>
                <div class="status-item highlight">
                  <span class="status-label">NPCs:</span>
                  <span class="status-value">{{ status()?.npcs }}</span>
                </div>
                <mat-divider></mat-divider>
                <div class="status-item">
                  <span class="status-label">Last Updated:</span>
                  <span class="status-value">{{ status()?.created | date:'medium' }}</span>
                </div>
              </div>

              <details class="json-details">
                <summary>View Raw JSON</summary>
                <pre class="json-display">{{ status() | json }}</pre>
              </details>
            }
          </mat-card-content>
        </mat-card>
      </div>
    </div>
  `,
  styles: [`
    .home-container {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 32px;
      max-width: 1200px;
      margin: 0 auto;
      padding: 0 16px;
    }

    @media (max-width: 768px) {
      .home-container {
        grid-template-columns: 1fr;
      }
    }

    .welcome-section {
      display: flex;
      flex-direction: column;
      gap: 24px;
    }

    .logo-container {
      margin-bottom: 16px;
      padding-top: 24px;
    }

    .ghost-logo {
      width: 150px;
      height: auto;
      animation: float 3s ease-in-out infinite;
    }

    @keyframes float {
      0%, 100% {
        transform: translateY(0px);
      }
      50% {
        transform: translateY(-20px);
      }
    }

    h1 {
      font-size: 2.5rem;
      font-weight: 300;
      margin: 0;
      color: #3f51b5;
    }

    .description {
      font-size: 1rem;
      line-height: 1.6;
      color: rgba(0, 0, 0, 0.7);
    }

    .description p {
      margin: 0 0 16px 0;
    }

    .section-divider {
      margin: 16px 0;
    }

    .quick-links h3 {
      font-size: 1.25rem;
      font-weight: 500;
      margin: 0 0 16px 0;
    }

    .links-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
      gap: 16px;
    }

    .link-item {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 12px 16px;
      background: #f5f5f5;
      border-radius: 8px;
      text-decoration: none;
      color: #3f51b5;
      transition: all 0.3s ease;
    }

    .link-item:hover {
      background: #e8eaf6;
      transform: translateY(-2px);
      box-shadow: 0 4px 8px rgba(0,0,0,0.1);
    }

    .link-icon {
      font-size: 24px;
    }

    .status-section {
      position: sticky;
      top: 20px;
      align-self: start;
    }

    mat-card {
      box-shadow: 0 2px 8px rgba(0,0,0,0.1);
    }

    mat-card-title {
      font-size: 1.1rem;
      font-weight: 500;
      margin-bottom: 16px;
    }

    .loading-container,
    .error-container {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      padding: 32px;
    }

    .error-message {
      color: #f44336;
      margin-bottom: 16px;
    }

    .status-data {
      display: flex;
      flex-direction: column;
      gap: 12px;
    }

    .status-item {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 8px 0;
    }

    .status-item.highlight {
      background: #f5f5f5;
      padding: 12px 16px;
      border-radius: 4px;
      margin: 4px 0;
    }

    .status-label {
      font-weight: 500;
      color: rgba(0, 0, 0, 0.6);
      font-size: 0.875rem;
    }

    .status-value {
      font-weight: 600;
      color: #3f51b5;
      font-size: 0.95rem;
    }

    .status-item.highlight .status-value {
      font-size: 1.25rem;
    }

    .json-details {
      margin-top: 16px;
      padding-top: 16px;
      border-top: 1px solid rgba(0, 0, 0, 0.12);
    }

    .json-details summary {
      cursor: pointer;
      color: #3f51b5;
      font-weight: 500;
      user-select: none;
    }

    .json-details summary:hover {
      text-decoration: underline;
    }

    .json-display {
      margin-top: 12px;
      padding: 16px;
      background: #f5f5f5;
      border: 1px solid #e0e0e0;
      border-radius: 4px;
      overflow-x: auto;
      font-size: 0.875rem;
      line-height: 1.5;
    }
  `]
})
export class HomeComponent implements OnInit {
  private readonly statusService = inject(StatusService);

  protected readonly status = signal<ApiStatus | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.loadStatus();
  }

  protected loadStatus(): void {
    this.loading.set(true);
    this.error.set(null);

    this.statusService.getStatus().subscribe({
      next: (status) => {
        this.status.set(status);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err.message || 'Failed to load server status');
        this.loading.set(false);
      }
    });
  }
}
