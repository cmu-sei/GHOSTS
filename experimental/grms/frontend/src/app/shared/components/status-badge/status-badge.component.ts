import { Component, inject, signal, ChangeDetectionStrategy, OnInit, OnDestroy } from '@angular/core';
import { interval, Subscription, switchMap, catchError, of } from 'rxjs';
import { HealthService } from '../../../core/services/health.service';
import { HealthStatus } from '../../../core/models/health.model';

@Component({
  selector: 'app-status-badge',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="status-badge" [class.online]="online()" [class.offline]="!online()">
      <span class="dot"></span>
      <span class="label">{{ online() ? 'Online' : 'Offline' }}</span>
    </div>
  `,
  styles: [`
    @use 'variables' as *;

    .status-badge {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 6px 10px;
      border-radius: 20px;
      font-size: 0.75rem;
      font-weight: 500;
    }

    .dot {
      width: 8px;
      height: 8px;
      border-radius: 50%;
      transition: background $transition;
    }

    .online {
      .dot {
        background: $green;
        box-shadow: 0 0 6px rgba(34, 197, 94, 0.5);
      }
      .label { color: $green; }
    }

    .offline {
      .dot {
        background: $red;
        box-shadow: 0 0 6px rgba(239, 68, 68, 0.5);
      }
      .label { color: $red; }
    }
  `]
})
export class StatusBadgeComponent implements OnInit, OnDestroy {
  private readonly healthService = inject(HealthService);
  private sub?: Subscription;

  online = signal(false);
  health = signal<HealthStatus | null>(null);

  ngOnInit(): void {
    this.sub = interval(30000).pipe(
      switchMap(() => this.healthService.getHealth().pipe(
        catchError(() => of(null))
      ))
    ).subscribe(status => {
      this.online.set(!!status);
      this.health.set(status);
    });

    this.healthService.getHealth().pipe(
      catchError(() => of(null))
    ).subscribe(status => {
      this.online.set(!!status);
      this.health.set(status);
    });
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }
}
