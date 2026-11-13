import { Component, inject, OnInit, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ChangeDetectionStrategy } from '@angular/core';
import { ApiStatus } from '../../core/models';
import { StatusService } from '../../core/services';
import { GhostComponent } from '../../shared/components/ghost/ghost.component';

@Component({
  selector: 'app-home',
  imports: [
    MatButtonModule,
    MatProgressSpinnerModule,
    GhostComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="sheet">
      <section class="welcome" aria-labelledby="welcome-title">
        <div class="top">
          <div class="intro">
            <p class="eyebrow">Welcome to GHOSTS</p>
            <h1 class="headline" id="welcome-title">Realistic user behavior simulation and orchestration, <em>for cyber and cognitive training, exercises, and research.</em></h1>
            <p class="lede">
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
            <p class="small">If you find GHOSTS useful for your needs, please consider <a href="https://github.com/cmu-sei/GHOSTS">starring the repository</a>. Otherwise, please use the links below to get started.</p>
            <div class="end">
              <a mat-flat-button href="https://cmu-sei.github.io/GHOSTS/" target="_blank" rel="noopener noreferrer">Documentation</a>
              <a mat-button href="https://github.com/cmu-sei/GHOSTS" target="_blank" rel="noopener noreferrer">GitHub</a>
            </div>
          </div>

          <div class="stage" aria-hidden="true">
            <app-ghost class="ghost" />
            <span class="shadow"></span>
          </div>
        </div>

        <div class="status" aria-label="Current server status">
          @if (loading()) {
            <div class="status-note">
              <mat-spinner diameter="20"></mat-spinner>
              <span>Loading server status…</span>
            </div>
          } @else if (error()) {
            <div class="status-note">
              <span class="error-message">{{ error() }}</span>
              <button mat-button (click)="loadStatus()">Retry</button>
            </div>
          } @else if (status()) {
            <div class="fact">
              <span class="label">Machines</span>
              <span class="value">{{ status()?.machines }}</span>
            </div>
            <div class="fact">
              <span class="label">Groups</span>
              <span class="value">{{ status()?.groups }}</span>
            </div>
            <div class="fact">
              <span class="label">NPCs</span>
              <span class="value">{{ status()?.npcs }}</span>
            </div>
            <div class="fact">
              <span class="label">Server</span>
              <span class="meta">v{{ status()?.version }}</span>
              <span class="meta">File {{ status()?.versionFile }}</span>
              <span class="meta">dotnet {{ status()?.versionEnvironment }}</span>
            </div>
          }
        </div>
      </section>
    </div>
  `,
  styles: [`
    .sheet {
      max-width: 1080px;
      margin: 12px auto;
    }

    .welcome {
      padding: 30px 34px 22px;
      border: 1px solid var(--gh-line);
      border-radius: 14px;
      background:
        radial-gradient(120% 140% at 100% 0%, color-mix(in srgb, var(--gh-brand) 8%, transparent) 0%, transparent 46%),
        radial-gradient(90% 120% at 0% 0%, color-mix(in srgb, var(--gh-accent) 6%, transparent) 0%, transparent 52%),
        var(--gh-surface);
      animation: welcome-in 360ms cubic-bezier(0.2, 0.8, 0.2, 1);
    }

    @keyframes welcome-in {
      from {
        opacity: 0;
        transform: translateY(6px);
      }
    }

    .top {
      display: grid;
      grid-template-columns: minmax(0, 1.6fr) minmax(0, 1fr);
      gap: 36px;
    }

    .eyebrow {
      margin: 0;
      font: 500 10.5px/1 var(--gh-mono);
      letter-spacing: 0.16em;
      text-transform: uppercase;
      color: var(--gh-accent);
    }

    .headline {
      margin: 12px 0 0;
      font: 500 30px/1.15 var(--gh-serif);
      letter-spacing: -0.015em;
      color: var(--gh-ink);
    }

    .headline em {
      font-style: italic;
      font-weight: 400;
      color: light-dark(#4a5268, #a3adba);
    }

    .lede {
      margin: 14px 0 0;
      font: 400 16px/1.6 var(--gh-serif);
      color: var(--gh-body);
    }

    .small {
      margin: 12px 0 0;
      color: var(--gh-muted);
    }

    .end {
      display: flex;
      gap: 8px;
      margin-top: 18px;
    }

    .stage {
      position: relative;
      display: grid;
      place-items: center;
      min-height: 260px;
    }

    .ghost {
      width: min(220px, 70%);
      animation: float 4.5s ease-in-out infinite;
    }

    .shadow {
      position: absolute;
      bottom: 22%;
      width: min(130px, 42%);
      height: 14px;
      border-radius: 50%;
      background: radial-gradient(closest-side, rgb(var(--gh-shadow) / 0.22), transparent);
      animation: breathe 4.5s ease-in-out infinite;
    }

    @keyframes float {
      0%, 100% { transform: translateY(-6px) rotate(-2deg); }
      50% { transform: translateY(-26px) rotate(2deg); }
    }

    @keyframes breathe {
      0%, 100% { transform: scale(1); opacity: 1; }
      50% { transform: scale(0.72); opacity: 0.55; }
    }

    .status {
      display: grid;
      grid-template-columns: repeat(3, minmax(0, 1fr)) minmax(0, 1.4fr);
      margin: 24px 0 0;
      border-top: 1px solid var(--gh-line);
    }

    .fact {
      display: flex;
      flex-direction: column;
      gap: 6px;
      padding: 16px 18px 0 0;
    }

    .fact + .fact {
      padding-left: 18px;
      border-left: 1px solid var(--gh-line);
    }

    .label {
      font: 500 11px/1 var(--gh-mono);
      letter-spacing: 0.1em;
      text-transform: uppercase;
      color: var(--gh-faint);
    }

    .value {
      margin-top: 4px;
      font: 500 26px/1.1 var(--gh-serif);
      color: var(--gh-ink);
    }

    .meta {
      font: 400 12px/1.5 var(--gh-mono);
      color: var(--gh-muted);
    }

    .status-note {
      grid-column: 1 / -1;
      display: flex;
      align-items: center;
      gap: 12px;
      padding-top: 16px;
      color: var(--gh-muted);
    }

    .error-message {
      color: var(--gh-bad);
    }

    @media (max-width: 860px) {
      .top {
        grid-template-columns: minmax(0, 1fr);
      }

      .stage {
        min-height: 200px;
      }

      .status {
        grid-template-columns: repeat(2, minmax(0, 1fr));
      }

      .fact:nth-child(3) {
        padding-left: 0;
        border-left: 0;
      }
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
