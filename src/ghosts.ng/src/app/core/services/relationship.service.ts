import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import {
  SocialGraphWithDetails,
  RelationshipSummary,
  InteractionMap,
  SocialGraphSummary,
  ConnectionSummary
} from '../models';

@Injectable({
  providedIn: 'root'
})
export class RelationshipService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}`;

  // Get all social graphs
  getAllSocialGraphs(): Observable<SocialGraphWithDetails[]> {
    return this.http.get<SocialGraphWithDetails[]>(`${this.apiUrl}/social-graphs`);
  }

  // Get social graph by ID with details
  getSocialGraphById(id: string): Observable<SocialGraphWithDetails> {
    return this.http.get<SocialGraphWithDetails>(`${this.apiUrl}/social-graphs/${id}`);
  }

  // Get social graph for a specific NPC
  getSocialGraphByNpcId(npcId: string): Observable<SocialGraphWithDetails> {
    return this.http.get<SocialGraphWithDetails>(`${this.apiUrl}/npcs/${npcId}/social-graph`);
  }

  // Get relationships summary
  getRelationshipsSummary(): Observable<RelationshipSummary[]> {
    return this.http.get<RelationshipSummary[]>(`${this.apiUrl}/relationships/summary`);
  }

  // Get interaction map for visualization
  getInteractionMap(socialGraphId: string): Observable<InteractionMap> {
    return this.http.get<InteractionMap>(`${this.apiUrl}/social-graphs/${socialGraphId}/interactions`);
  }

  // Export relationships as CSV
  exportRelationshipsCSV(): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/relationships/export`, {
      responseType: 'blob'
    });
  }

  // Get connections for a social graph
  getConnections(socialGraphId: string): Observable<any[]> {
    return this.http.get<any[]>(`${this.apiUrl}/social-graphs/${socialGraphId}/connections`);
  }

  // Get knowledge for a social graph
  getKnowledge(socialGraphId: string): Observable<any[]> {
    return this.http.get<any[]>(`${this.apiUrl}/social-graphs/${socialGraphId}/knowledge`);
  }

  // Get beliefs for a social graph
  getBeliefs(socialGraphId: string): Observable<any[]> {
    return this.http.get<any[]>(`${this.apiUrl}/social-graphs/${socialGraphId}/beliefs`);
  }

  // Social-specific endpoints

  // Get all social graphs with knowledge summaries
  getSocialGraphSummaries(): Observable<SocialGraphSummary[]> {
    return this.http.get<any[]>(`${this.apiUrl}/social`).pipe(
      map(graphs => graphs.map(graph => this.transformToSocialGraphSummary(graph)))
    );
  }

  // Get detailed connections for a social graph
  getConnectionDetails(socialGraphId: string): Observable<ConnectionSummary[]> {
    return this.http.get<any>(`${this.apiUrl}/social/${socialGraphId}`).pipe(
      map(graph => this.transformToConnectionSummaries(graph))
    );
  }

  private transformToSocialGraphSummary(graph: any): SocialGraphSummary {
    // Extract name from npcProfile.name structure
    let displayName = 'Unknown NPC';
    if (graph.npcProfile?.name) {
      const nameObj = graph.npcProfile.name;
      const parts = [nameObj.first, nameObj.last].filter(p => p);
      if (parts.length > 0) {
        displayName = parts.join(' ');
      }
    } else if (graph.name) {
      displayName = graph.name;
    }

    // Aggregate knowledge topics from the Knowledge array
    const knowledgeMap = new Map<string, { count: number; totalValue: number; values: number[] }>();

    if (graph.knowledge && Array.isArray(graph.knowledge)) {
      graph.knowledge.forEach((k: any) => {
        if (!knowledgeMap.has(k.topic)) {
          knowledgeMap.set(k.topic, { count: 0, totalValue: 0, values: [] });
        }
        const topicData = knowledgeMap.get(k.topic)!;
        topicData.count++;
        topicData.totalValue += k.value;
        topicData.values.push(k.value);
      });
    }

    const knowledgeTopics = Array.from(knowledgeMap.entries()).map(([topic, data]) => ({
      topic,
      count: data.count,
      totalValue: data.totalValue,
      averageValue: data.totalValue / data.count,
      strength: this.calculateKnowledgeStrength(data.totalValue, data.count)
    })).sort((a, b) => b.totalValue - a.totalValue);

    // Aggregate preferences (defensive - handle undefined/null)
    const preferenceMap = new Map<string, { weight: number; strength: number; count: number }>();
    if (graph.preferences && Array.isArray(graph.preferences)) {
      graph.preferences.forEach((p: any) => {
        if (p && p.name) {
          if (!preferenceMap.has(p.name)) {
            preferenceMap.set(p.name, { weight: 0, strength: 0, count: 0 });
          }
          const prefData = preferenceMap.get(p.name)!;
          prefData.weight += (p.weight || 0);
          prefData.strength += (p.strength || 0);
          prefData.count++;
        }
      });
    }

    const preferences = Array.from(preferenceMap.entries()).map(([name, data]) => ({
      name,
      weight: data.count > 0 ? data.weight / data.count : 0,
      strength: data.count > 0 ? data.strength / data.count : 0,
      count: data.count
    })).sort((a, b) => b.strength - a.strength);

    const npcIdForPhoto = graph.npcId ?? graph.id;
    return {
      id: graph.id,
      name: displayName,
      npcId: graph.npcId ?? graph.id,
      avatar: npcIdForPhoto ? `${this.apiUrl}/npcs/${npcIdForPhoto}/photo` : undefined,
      campaign: graph.campaign,
      enclave: graph.enclave,
      team: graph.team,
      currentStep: graph.currentStep,
      knowledgeTopics,
      preferences,
      connectionCount: graph.connections?.length ?? 0,
      beliefCount: graph.beliefs?.length ?? 0
    };
  }

  private transformToConnectionSummaries(graph: any): ConnectionSummary[] {
    if (!graph.connections || !Array.isArray(graph.connections)) {
      return [];
    }

    return graph.connections.map((connection: any) => {
      const interactions = connection.interactions?.map((i: any) => ({
        id: i.id,
        step: i.step,
        value: i.value,
        change: i.value > 0 ? 'up' as const : i.value < 0 ? 'down' as const : 'neutral' as const
      })) ?? [];

      const knowledgeTransfers = graph.knowledge
        ?.filter((k: any) => k.fromNpcId === connection.connectedNpcId || k.toNpcId === connection.connectedNpcId)
        .map((k: any) => ({
          id: k.id,
          topic: k.topic,
          step: k.step,
          value: k.value,
          fromNpcId: k.fromNpcId,
          fromNpcName: k.fromNpcName ?? 'Unknown'
        })) ?? [];

      const totalValue = interactions.reduce((sum: number, i: any) => sum + i.value, 0);

      // Only use connectedNpcId if it's a valid GUID (not empty GUID)
      const isValidGuid = connection.connectedNpcId && connection.connectedNpcId !== '00000000-0000-0000-0000-000000000000';
      const avatarUrl = isValidGuid ? `${this.apiUrl}/npcs/${connection.connectedNpcId}/photo` : undefined;

      return {
        id: connection.id,
        connectedNpcId: connection.connectedNpcId,
        name: connection.name,
        avatar: avatarUrl,
        relationshipStatus: connection.relationshipStatus,
        interactionCount: interactions.length,
        totalInteractionValue: totalValue,
        connectionScore: this.calculateConnectionScore(connection.relationshipStatus, interactions.length, totalValue),
        interactions,
        knowledgeTransfers,
        hasDecay: knowledgeTransfers.some((k: any) => k.value < 0)
      };
    });
  }

  private calculateKnowledgeStrength(totalValue: number, count: number): number {
    if (count === 0) return 0;
    const avgValue = totalValue / count;
    const score = (avgValue * count) / 10.0;
    return Math.min(9, Math.max(0, Math.round(score)));
  }

  private calculateConnectionScore(relationshipStatus: number, interactionCount: number, totalValue: number): number {
    const baseScore = Math.max(0, Math.min(100, relationshipStatus));
    const interactionBonus = Math.min(20, interactionCount * 2);
    const valueModifier = Math.max(-20, Math.min(20, totalValue / 5.0));
    const finalScore = baseScore + interactionBonus + valueModifier;
    return Math.max(0, Math.min(100, Math.round(finalScore * 100) / 100));
  }

  // Export interaction map as JSON
  exportInteractionMapJSON(socialGraphId: string): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/social-graphs/${socialGraphId}/interaction-map`, {
      responseType: 'blob'
    });
  }
}
