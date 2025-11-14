import { Component, inject, OnInit, signal } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDividerModule } from '@angular/material/divider';
import { ChangeDetectionStrategy } from '@angular/core';
import { ApiStatus } from '../../core/models';
import { StatusService } from '../../core/services';

@Component({
  selector: 'app-home',
  imports: [
    MatCardModule,
    MatProgressSpinnerModule,
    MatDividerModule
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
          <p><strong>Realistic User Behavior Simulation and Orchestration for Cyber/Cognitive Training, Exercises, and Research</strong></p>
          <p>
            GHOSTS is an agent orchestration framework that simulates realistic users
            on all types of computer systems, generating human-like activity across
            applications, networks, and workflows. Beyond simple automation, it can
            dynamically reason, chat, and create content via integrated LLMs, enabling
            adaptive, context-aware behavior. Designed for cyber training, research,
            and simulation, it produces realistic network traffic, supports complex
            multi-agent scenarios, and leaves behind realistic artifacts. Its modular
            architecture allows the addition of new agents, behaviors, and lightweight
            clients, making it a flexible platform for high-fidelity simulations.
          </p>
          <p class="small">If you find GHOSTS useful for your needs, please consider <a href="https://github.com/cmu-sei/GHOSTS">starring the repository</a>. Otherwise, please use the quick links at right to get started.</p>
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
                  <span class="status-label">Machines:</span>
                  <span class="status-value">{{ status()?.machines }}</span>
                </div>
                <div class="status-item">
                  <span class="status-label">Groups:</span>
                  <span class="status-value">{{ status()?.groups }}</span>
                </div>
                <div class="status-item">
                  <span class="status-label">NPCs:</span>
                  <span class="status-value">{{ status()?.npcs }}</span>
                </div>
                <div class="status-item version-item">
                  <span class="status-label">Version:</span>
                  <span class="status-value">{{ status()?.version }}</span>
                </div>
                <div class="status-item version-item">
                  <span class="status-label">File Version:</span>
                  <span class="status-value">{{ status()?.versionFile }}</span>
                </div>
                <div class="status-item version-item">
                  <span class="status-label">dotnet:</span>
                  <span class="status-value">{{ status()?.versionEnvironment }}</span>
                </div>
              </div>
            }
          </mat-card-content>
        </mat-card>

        <div class="quick-links">
          <h3>Quick Links</h3>
          <div class="links-grid">
            <a href="https://cmu-sei.github.io/GHOSTS/" target="_blank" rel="noopener noreferrer" class="link-item">
              <span class="link-icon">📖</span>
              <span>Documentation</span>
            </a>
            <a href="https://github.com/cmu-sei/GHOSTS" target="_blank" rel="noopener noreferrer" class="link-item">
              <span class="link-icon">💻</span>
              <span>GitHub</span>
            </a>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .home-container {
      display: grid;
      grid-template-columns: 4fr 1fr;
      gap: 32px;
      padding: 24px;
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
      color: rgba(0, 0, 0, 0.87);
    }

    .description {
      font-size: 1rem;
      line-height: 1.6;
      color: rgba(0, 0, 0, 0.7);
    }

    .description p {
      margin: 0 0 16px 0;
    }

    .quick-links {
      margin-top: 24px;
    }

    .small {
      font-size: 0.85rem;
    }

    .quick-links h3 {
      font-size: 0.95rem;
      font-weight: normal;
      margin: 0 0 12px 0;
    }

    .links-grid {
      display: flex;
      flex-direction: column;
      gap: 12px;
    }

    .link-item {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 10px 12px;
      background: #f5f5f5;
      border-radius: 6px;
      text-decoration: none;
      color: rgba(0, 0, 0, 0.87);
      transition: all 0.2s ease;
      font-size: 0.85rem;
    }

    .link-item:hover {
      background: #e0e0e0;
    }

    .link-icon {
      font-size: 18px;
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
      font-size: 0.95rem;
      font-weight: normal;
      margin-bottom: 12px;
    }

    .loading-container,
    .error-container {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      padding: 24px;
    }

    .error-message {
      color: #f44336;
      margin-bottom: 16px;
    }

    .status-data {
      display: flex;
      flex-direction: column;
      gap: 8px;
    }

    .status-item {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 4px 0;
    }

    .status-item.version-item .status-label,
    .status-item.version-item .status-value {
      color: rgba(0, 0, 0, 0.4);
      font-size: 0.8rem;
    }

    .status-label {
      font-weight: normal;
      color: rgba(0, 0, 0, 0.6);
      font-size: 0.85rem;
    }

    .status-value {
      font-weight: normal;
      color: rgba(0, 0, 0, 0.87);
      font-size: 0.85rem;
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
