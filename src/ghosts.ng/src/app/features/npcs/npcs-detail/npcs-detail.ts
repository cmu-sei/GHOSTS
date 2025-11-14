import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, ActivatedRoute } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatTabsModule } from '@angular/material/tabs';
import { ChangeDetectionStrategy } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  SocialGraphSummary,
  ConnectionSummary,
  Npc
} from '../../../core/models';
import { RelationshipService, NpcService } from '../../../core/services';
import { environment } from '../../../../environments/environment';

@Component({
  selector: 'app-npcs-detail',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    MatExpansionModule,
    MatTabsModule
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './npcs-detail.html',
  styleUrl: './npcs-detail.scss'
})
export class NpcsDetail implements OnInit {
  private readonly apiUrl = `${environment.apiUrl}`;
  private readonly relationshipService = inject(RelationshipService);
  private readonly npcService = inject(NpcService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly npc = signal<Npc | null>(null);
  protected readonly npcGraph = signal<SocialGraphSummary | null>(null);
  protected readonly connections = signal<ConnectionSummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly loadingConnections = signal(false);
  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.route.paramMap
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(params => {
        const npcId = params.get('npc_id');
        if (npcId) {
          this.loadNpcDetails(npcId);
        }
      });
  }

  protected loadNpcDetails(npcId: string): void {
    this.loading.set(true);
    this.error.set(null);

    // Load full NPC profile
    this.npcService.getNpc(npcId).subscribe({
      next: (npc) => {
        this.npc.set(npc);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err.message || 'Failed to load NPC profile');
        this.loading.set(false);
      }
    });

    // Load social graph summary
    this.relationshipService.getSocialGraphSummaries().subscribe({
      next: (graphs) => {
        const graph = graphs.find(g => g.npcId === npcId || g.id === npcId);
        if (graph) {
          this.npcGraph.set(graph);
          this.loadConnections(graph.id);
        }
      },
      error: (err) => {
        console.error('Failed to load social graph:', err);
      }
    });
  }

  protected loadConnections(graphId: string): void {
    console.log('hi');
    this.loadingConnections.set(true);

    this.relationshipService.getConnectionDetails(graphId).subscribe({
      next: (connections) => {
        this.connections.set(connections);
        this.loadingConnections.set(false);
        console.log('Loaded connections:', connections);
        for(const conn of connections) {
          conn.avatar = `${this.apiUrl}/npcs/${conn.connectedNpcId}/photo`;
        }
      },
      error: (err) => {
        console.error('Failed to load connections:', err);
        this.connections.set([]);
        this.loadingConnections.set(false);
      }
    });
  }

  protected goBack(): void {
    this.router.navigate(['/npcs']);
  }

  protected getConnectionScoreClass(score: number): string {
    if (score >= 75) return 'excellent';
    if (score >= 50) return 'good';
    if (score >= 25) return 'fair';
    return 'poor';
  }

  protected exportInteractionMap(graphId: string): void {
    this.relationshipService.exportInteractionMapJSON(graphId).subscribe({
      next: (blob) => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `interaction-map-${graphId}.json`;
        a.click();
        window.URL.revokeObjectURL(url);
      },
      error: (err) => {
        console.error('Failed to export interaction map:', err);
      }
    });
  }

  protected hasKnowledgeDecay(connection: ConnectionSummary): boolean {
    return connection.hasDecay || false;
  }

  protected getDecayCount(connection: ConnectionSummary): number {
    if (!connection.knowledgeTransfers) return 0;
    return connection.knowledgeTransfers.filter(k => k.value < 0).length;
  }

  protected getNpcPhotoUrl(npcId?: string): string {
    return npcId ? `${this.apiUrl}/npcs/${npcId}/photo` : '';
  }

  protected getNpcName(): string {
    const name = this.npc()?.npcProfile?.name;
    if (!name) return 'Unknown NPC';
    return [name.prefix, name.first, name.middle, name.last, name.suffix]
      .filter(part => !!part && part.trim().length > 0)
      .join(' ');
  }

  protected navigateToNpc(npcId: string): void {
    if (npcId) {
      this.router.navigate(['/npcs', npcId]);
    }
  }
}
