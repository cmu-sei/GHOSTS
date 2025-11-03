import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router, ActivatedRoute } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatTableModule } from '@angular/material/table';
import { ChangeDetectionStrategy } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  SocialGraphSummary,
  ConnectionSummary
} from '../../../core/models';
import { RelationshipService } from '../../../core/services';
import { environment } from '../../../../environments/environment';

@Component({
  selector: 'app-social-list',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatCardModule,
    MatButtonModule,
    MatChipsModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    MatExpansionModule,
    MatTableModule
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './social-list.component.html',
  styleUrls: ['./social-list.component.scss']
})
export class SocialListComponent implements OnInit {
  private readonly relationshipService = inject(RelationshipService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  private detailOrigin: 'social' | 'npcs';

  protected readonly socialGraphs = signal<SocialGraphSummary[]>([]);
  protected readonly selectedGraph = signal<SocialGraphSummary | null>(null);
  protected readonly connections = signal<ConnectionSummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly loadingConnections = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly connectionColumns = ['name', 'status', 'interactions', 'score', 'actions'];
  private pendingNpcId: string | null = null;

  constructor() {
    this.detailOrigin = this.determineDetailOrigin();
  }

  ngOnInit(): void {
    this.route.paramMap
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(params => this.syncSelectionWithRoute(params.get('npc_id')));

    this.loadSocialGraphs();
  }

  protected loadSocialGraphs(): void {
    this.loading.set(true);
    this.error.set(null);

    this.relationshipService.getSocialGraphSummaries().subscribe({
      next: (graphs) => {
        this.socialGraphs.set(graphs);
        this.loading.set(false);
        const npcIdFromRoute = this.route.snapshot.paramMap.get('npc_id') ?? this.pendingNpcId;
        if (npcIdFromRoute) {
          this.syncSelectionWithRoute(npcIdFromRoute);
        }
      },
      error: (err) => {
        this.error.set(err.message || 'Failed to load social graphs');
        this.loading.set(false);
      }
    });
  }

  protected viewDetails(graph: SocialGraphSummary): void {
    const npcId = graph.npcId ?? graph.id;
    const origin = this.detailOrigin ?? 'social';
    this.router.navigate(['/npcs', npcId], { state: { origin } });
  }

  protected closeDetails(): void {
    this.clearSelection();
    const target = this.detailOrigin === 'npcs' ? ['/npcs'] : ['/social'];
    this.router.navigate(target);
  }

  protected getKnowledgeStrengthClass(strength: number): string {
    // Map strength values 0-9 to z0-z9 classes
    const index = Math.min(Math.max(Math.floor(strength), 0), 9);
    return `z${index}`;
  }

  protected getConnectionScoreClass(score: number): string {
    if (score >= 75) return 'excellent';
    if (score >= 50) return 'good';
    if (score >= 25) return 'fair';
    return 'poor';
  }

  protected getInteractionChange(interaction: any): 'up' | 'down' | 'neutral' {
    if (interaction.value > 0) return 'up';
    if (interaction.value < 0) return 'down';
    return 'neutral';
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

  private syncSelectionWithRoute(npcId: string | null): void {
    if (!npcId) {
      this.pendingNpcId = null;
      this.clearSelection();
      return;
    }

    const graph = this.socialGraphs().find(g => g.npcId === npcId || g.id === npcId);
    if (graph) {
      this.pendingNpcId = null;
      this.openGraph(graph);
      return;
    }

    this.pendingNpcId = npcId;
  }

  private openGraph(graph: SocialGraphSummary): void {
    if (this.selectedGraph()?.id === graph.id && this.connections().length > 0) {
      return;
    }

    this.selectedGraph.set(graph);
    this.loadingConnections.set(true);
    this.connections.set([]);

    this.relationshipService.getConnectionDetails(graph.id).subscribe({
      next: (connections) => {
        this.connections.set(connections);
        this.loadingConnections.set(false);
      },
      error: (err) => {
        console.error('Failed to load connections:', err);
        this.connections.set([]);
        this.loadingConnections.set(false);
      }
    });
  }

  private clearSelection(): void {
    this.selectedGraph.set(null);
    this.connections.set([]);
    this.loadingConnections.set(false);
  }

  private determineDetailOrigin(): 'social' | 'npcs' {
    const navigation = this.router.getCurrentNavigation();
    const state = navigation?.extras?.state as { origin?: 'social' | 'npcs' } | undefined;

    if (state?.origin === 'social' || state?.origin === 'npcs') {
      return state.origin;
    }

    const url = navigation?.extractedUrl?.toString() || this.router.url || '';
    const normalized = url.startsWith('/') ? url : `/${url}`;
    return normalized.startsWith('/npcs') ? 'npcs' : 'social';
  }
}
