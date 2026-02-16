import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { ChangeDetectionStrategy } from '@angular/core';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialog } from '@angular/material/dialog';
import { DatePipe } from '@angular/common';
import { Npc, Scenario } from '../../../core/models';
import { NpcService, ScenarioService } from '../../../core/services';
import { ConfigService } from '../../../core/services/config.service';
import { GenerateNpcsDialogComponent } from '../generate-npcs-dialog/generate-npcs-dialog.component';
import { SearchBarComponent } from '../../../shared/components/search-bar/search-bar.component';

@Component({
  selector: 'app-npcs-list',
  imports: [
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    DatePipe,
    SearchBarComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page-header">
      <h1>NPCs</h1>
      <div class="header-actions">
        <button mat-raised-button color="accent" (click)="openGenerateDialog()">
          <i class="fas fa-users"></i>
          Generate NPCs
        </button>
      </div>
    </div>

    <div class="search-container">
      <app-search-bar (searchChange)="onSearchChange($event)"></app-search-bar>
      @if (searchTerm()) {
        <div class="search-results-info">
          Showing {{ filteredNpcs().length }} of {{ npcs().length }} NPCs
        </div>
      }
    </div>

    @if (loading()) {
      <div class="loading">
        <mat-spinner></mat-spinner>
      </div>
    } @else if (error()) {
      <div class="error">
        <p>Error loading NPCs: {{ error() }}</p>
        <button mat-button (click)="loadNpcs()">Retry</button>
      </div>
    } @else if (npcs().length === 0) {
      <div class="empty-state">
        <i class="fas fa-user-slash"></i>
        <p>No NPCs found.</p>
      </div>
    } @else {
      <div class="table-container">
        <table mat-table [dataSource]="filteredNpcs()">
          <ng-container matColumnDef="photo">
            <th mat-header-cell *matHeaderCellDef>Photo</th>
            <td mat-cell *matCellDef="let npc">
              <img [src]="getNpcPhotoUrl(npc.id)" alt="NPC Photo" class="npc-photo">
            </td>
          </ng-container>

          <ng-container matColumnDef="name">
            <th mat-header-cell *matHeaderCellDef>Name</th>
            <td mat-cell *matCellDef="let npc">
              <a href="/npcs/{{ npc.id }}">{{ getNpcName(npc) }}</a>
            </td>
          </ng-container>

          <ng-container matColumnDef="campaign">
            <th mat-header-cell *matHeaderCellDef>Campaign</th>
            <td mat-cell *matCellDef="let npc">{{ npc.campaign || '—' }}</td>
          </ng-container>

          <ng-container matColumnDef="enclave">
            <th mat-header-cell *matHeaderCellDef>Enclave</th>
            <td mat-cell *matCellDef="let npc">{{ npc.enclave || '—' }}</td>
          </ng-container>

          <ng-container matColumnDef="team">
            <th mat-header-cell *matHeaderCellDef>Team</th>
            <td mat-cell *matCellDef="let npc">{{ npc.team || '—' }}</td>
          </ng-container>

          <ng-container matColumnDef="scenario">
            <th mat-header-cell *matHeaderCellDef>Scenario</th>
            <td mat-cell *matCellDef="let npc">{{ getScenarioName(npc.scenarioId) }}</td>
          </ng-container>

          <ng-container matColumnDef="created">
            <th mat-header-cell *matHeaderCellDef>Created</th>
            <td mat-cell *matCellDef="let npc">
              {{ npc.createdUtc | date:'medium' }}
            </td>
          </ng-container>

          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef>Actions</th>
            <td mat-cell *matCellDef="let npc">
              <button mat-button class="icon-button">
                <i class="fas fa-ellipsis-v"></i>
              </button>
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
        </table>
      </div>
    }
  `,
  styles: [`
    .page-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 24px;
    }

    h1 {
      margin: 0;
      font-size: 24px;
      font-weight: 500;
    }

    .header-actions {
      display: flex;
      gap: 12px;

      button i {
        margin-right: 6px;
      }
    }

    .search-container {
      margin-bottom: 24px;
      display: flex;
      align-items: center;
      gap: 16px;
    }

    .search-results-info {
      color: rgba(0, 0, 0, 0.6);
      font-size: 14px;
    }

    .loading, .error, .empty-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      padding: 48px;
      gap: 16px;
    }

    .error p {
      margin: 0;
      color: #f44336;
      text-align: center;
    }

    .empty-state mat-icon {
      font-size: 48px;
      color: rgba(0, 0, 0, 0.32);
    }

    .table-container {
      background: #ffffff;
      border-radius: 4px;
      box-shadow: 0 2px 4px rgba(0,0,0,0.1);
    }

    table {
      width: 100%;
    }

    td.mat-cell, th.mat-header-cell {
      padding: 12px 16px;
    }

    th.mat-header-cell {
      font-weight: 600;
    }

    .npc-photo {
      width: 36px;
      height: 36px;
      border-radius: 50%;
      object-fit: cover;
    }
  `]
})
export class NpcsListComponent implements OnInit {
  private readonly npcService = inject(NpcService);
  private readonly scenarioService = inject(ScenarioService);
  private readonly configService = inject(ConfigService);
  private readonly dialog = inject(MatDialog);

  protected readonly npcs = signal<Npc[]>([]);
  protected readonly scenarios = signal<Scenario[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly searchTerm = signal('');
  protected readonly displayedColumns = ['photo', 'name', 'campaign', 'enclave', 'team', 'scenario', 'created', 'actions'];

  protected readonly scenarioMap = computed(() => {
    const map = new Map<number, string>();
    this.scenarios().forEach(s => map.set(s.id, s.name));
    return map;
  });

  protected readonly filteredNpcs = computed(() => {
    const search = this.searchTerm().toLowerCase().trim();
    if (!search) {
      return this.npcs();
    }
    return this.npcs().filter(npc => {
      const name = this.getNpcName(npc).toLowerCase();
      const campaign = npc.campaign?.toLowerCase() || '';
      const enclave = npc.enclave?.toLowerCase() || '';
      const team = npc.team?.toLowerCase() || '';
      return name.includes(search) ||
             campaign.includes(search) ||
             enclave.includes(search) ||
             team.includes(search);
    });
  });

  ngOnInit(): void {
    this.loadScenarios();
    this.loadNpcs();
  }

  private loadScenarios(): void {
    this.scenarioService.getScenarios().subscribe({
      next: (scenarios) => this.scenarios.set(scenarios),
      error: (err) => console.error('Failed to load scenarios', err)
    });
  }

  protected onSearchChange(searchTerm: string): void {
    this.searchTerm.set(searchTerm);
  }

  protected openGenerateDialog(): void {
    const dialogRef = this.dialog.open(GenerateNpcsDialogComponent, {
      width: '500px'
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        // Refresh the list after generating NPCs
        this.loadNpcs();
      }
    });
  }

  protected loadNpcs(): void {
    this.loading.set(true);
    this.error.set(null);

    this.npcService.getNpcs().subscribe({
      next: (npcs) => {
        this.npcs.set(npcs);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err.message || 'Failed to load NPCs');
        this.loading.set(false);
      }
    });
  }

  protected getNpcName(npc: Npc): string {
    const name = npc.npcProfile?.name;
    if (!name) {
      return npc.id ?? 'Unknown NPC';
    }

    return [name.prefix, name.first, name.middle, name.last, name.suffix]
      .filter(part => !!part && part.trim().length > 0)
      .join(' ');
  }

  protected getNpcPhotoUrl(npcId: string | undefined): string {
    return npcId ? `${this.configService.apiUrl}/npcs/${npcId}/photo` : '';
  }

  protected getScenarioName(scenarioId: number | undefined): string {
    if (!scenarioId) {
      return '—';
    }
    return this.scenarioMap().get(scenarioId) || `Scenario #${scenarioId}`;
  }
}
