import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatTabsModule } from '@angular/material/tabs';
import { MatExpansionModule } from '@angular/material/expansion';
import { ChangeDetectionStrategy } from '@angular/core';
import { SocialGraphWithDetails, SocialConnectionDetail } from '../../../core/models';
import { RelationshipService } from '../../../core/services';

@Component({
  selector: 'app-relationships-list',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatChipsModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    MatTabsModule,
    MatExpansionModule
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './relationships-list.component.html',
  styleUrls: ['./relationships-list.component.scss']
})
export class RelationshipsListComponent implements OnInit {
  private readonly relationshipService = inject(RelationshipService);

  protected readonly socialGraphs = signal<SocialGraphWithDetails[]>([]);
  protected readonly selectedGraph = signal<SocialGraphWithDetails | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly displayedColumns = ['name', 'connections', 'knowledge', 'beliefs', 'actions'];
  protected readonly connectionColumns = ['name', 'status', 'interactions', 'score'];

  ngOnInit(): void {
    this.loadSocialGraphs();
  }

  protected loadSocialGraphs(): void {
    this.loading.set(true);
    this.error.set(null);

    this.relationshipService.getAllSocialGraphs().subscribe({
      next: (graphs) => {
        console.log('Social graphs received:', graphs);
        graphs.forEach(g => {
          console.log(`Graph ${g.name}:`, {
            knowledge: g.knowledge,
            knowledgeCount: g.knowledge?.length,
            connections: g.connections?.length,
            beliefs: g.beliefs?.length
          });
        });
        this.socialGraphs.set(graphs);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err.message || 'Failed to load social graphs');
        this.loading.set(false);
      }
    });
  }

  protected viewDetails(graph: SocialGraphWithDetails): void {
    console.log('Viewing details for graph:', graph);
    console.log('Knowledge data:', graph.knowledge);
    this.selectedGraph.set(graph);
  }

  protected closeDetails(): void {
    this.selectedGraph.set(null);
  }

  protected getConnectionCount(graph: SocialGraphWithDetails): number {
    return graph.connections?.length || 0;
  }

  protected getKnowledgeTopicCount(graph: SocialGraphWithDetails): number {
    // Get unique topics
    const topics = new Set(graph.knowledge?.map(k => k.topic) || []);
    return topics.size;
  }

  protected getGroupedKnowledge(graph: SocialGraphWithDetails): Array<{ topic: string; total: number }> {
    if (!graph.knowledge || graph.knowledge.length === 0) {
      return [];
    }

    // Group by topic and sum values
    const grouped = new Map<string, number>();
    graph.knowledge.forEach(k => {
      const current = grouped.get(k.topic) || 0;
      grouped.set(k.topic, current + k.value);
    });

    // Convert to array and sort by topic
    return Array.from(grouped.entries())
      .map(([topic, total]) => ({ topic, total }))
      .sort((a, b) => a.topic.localeCompare(b.topic));
  }

  protected getBeliefsCount(graph: SocialGraphWithDetails): number {
    return graph.beliefs?.length || 0;
  }

  protected getRelationshipStatusClass(status: number): string {
    if (status >= 75) return 'excellent';
    if (status >= 50) return 'good';
    if (status >= 25) return 'fair';
    return 'poor';
  }

  protected getRelationshipStatusLabel(status: number): string {
    if (status >= 75) return 'Excellent';
    if (status >= 50) return 'Good';
    if (status >= 25) return 'Fair';
    return 'Poor';
  }

  protected getConnectionScore(connection: SocialConnectionDetail): number {
    if (!connection.interactionCount || connection.interactionCount === 0) return 0;
    return Math.round((connection.totalInteractionValue || 0) / connection.interactionCount);
  }

  protected getInteractionTrend(connection: SocialConnectionDetail): 'up' | 'down' | 'neutral' {
    const interactions = connection.interactions || [];
    if (interactions.length < 2) return 'neutral';

    const recent = interactions.slice(-3);
    const older = interactions.slice(-6, -3);

    if (recent.length === 0 || older.length === 0) return 'neutral';

    const recentAvg = recent.reduce((sum, i) => sum + i.value, 0) / recent.length;
    const olderAvg = older.reduce((sum, i) => sum + i.value, 0) / older.length;

    if (recentAvg > olderAvg + 5) return 'up';
    if (recentAvg < olderAvg - 5) return 'down';
    return 'neutral';
  }

  protected getKnowledgeStrengthClass(value: number): string {
    // Map knowledge values to z0-z9 classes like the original
    const index = Math.min(Math.floor(value / 10), 9);
    return `z${index}`;
  }

  protected exportCSV(): void {
    this.relationshipService.exportRelationshipsCSV().subscribe({
      next: (blob) => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = 'relationships.csv';
        a.click();
        window.URL.revokeObjectURL(url);
      },
      error: (err) => {
        console.error('Failed to export relationships:', err);
      }
    });
  }
}
