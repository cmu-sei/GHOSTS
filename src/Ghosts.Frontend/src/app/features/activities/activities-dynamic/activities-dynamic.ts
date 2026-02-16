import { Component, OnInit, OnDestroy, ElementRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import * as d3 from 'd3';
import * as signalR from '@microsoft/signalr';
import { NpcService } from '../../../core/services/npc.service';
import { ConfigService } from '../../../core/services/config.service';

interface NetworkNode extends d3.SimulationNodeDatum {
  id: string;
  name: string;
  knowledge: string[];
  image: string;
}

interface NetworkLink extends d3.SimulationLinkDatum<NetworkNode> {
  source: string | NetworkNode;
  target: string | NetworkNode;
  strength: number;
}

interface MoodState {
  label: string;
  value: number;
}

@Component({
  selector: 'app-activities-dynamic',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './activities-dynamic.html',
  styleUrls: ['./activities-dynamic.scss']
})
export class ActivitiesDynamicComponent implements OnInit, OnDestroy {
  private readonly npcService = inject(NpcService);
  private readonly configService = inject(ConfigService);
  private readonly elementRef = inject(ElementRef);
  private connection?: signalR.HubConnection;
  private simulation?: d3.Simulation<NetworkNode, NetworkLink>;
  private svg?: d3.Selection<SVGSVGElement, unknown, null, undefined>;
  private nodes: NetworkNode[] = [];
  private links: NetworkLink[] = [];
  private moodState: MoodState[] = [];
  private linkLayer?: d3.Selection<SVGGElement, unknown, null, undefined>;
  private nodeLayer?: d3.Selection<SVGGElement, unknown, null, undefined>;
  private iconLayer?: d3.Selection<SVGGElement, unknown, null, undefined>;

  ngOnInit(): void {
    this.loadNpcsAndInitialize();
  }

  ngOnDestroy(): void {
    this.connection?.stop();
    this.simulation?.stop();
  }

  private loadNpcsAndInitialize(): void {
    this.npcService.getNpcs().subscribe({
      next: (npcs) => {
        // Create nodes from NPCs (filter out undefined IDs)
        this.nodes = npcs
          .filter(npc => npc.id !== undefined)
          .map(npc => ({
            id: npc.id!,
            name: this.formatNpcName(npc.npcProfile?.name),
            knowledge: [],
            image: `${this.configService.apiUrl}/npcs/${npc.id}/photo`
          }));

        // Create initial links (sequential connections)
        this.links = [];
        for (let i = 1; i < this.nodes.length; i++) {
          this.links.push({
            source: this.nodes[i - 1].id,
            target: this.nodes[i].id,
            strength: 1
          });
        }

        this.initializeVisualization();
        this.initializeSignalR();
      },
      error: (err) => {
        console.error('Failed to load NPCs:', err);
      }
    });
  }

  private formatNpcName(name: any): string {
    if (!name) return 'Unknown';

    // Handle string name
    if (typeof name === 'string') return name;

    // Handle object with first/last properties
    const first = name.first || name.First || '';
    const middle = name.middle || name.Middle || '';
    const last = name.last || name.Last || '';

    if (!first && !last) return 'Unknown';
    if (!first) return last;
    if (!last) return middle ? `${first} ${middle}` : first;

    return middle ? `${first} ${middle} ${last}` : `${first} ${last}`;
  }

  private initializeVisualization(): void {
    const width = window.innerWidth;
    const height = window.innerHeight;

    const svgElement = d3.select(this.elementRef.nativeElement)
      .select('svg') as d3.Selection<SVGSVGElement, unknown, null, undefined>;

    this.svg = svgElement
      .attr('width', width)
      .attr('height', height);

    // Add filter for glow effect
    svgElement.append('defs').html(`
      <filter id="pulse-glow" x="-50%" y="-50%" width="200%" height="200%">
        <feGaussianBlur in="SourceGraphic" stdDeviation="2.5" result="blur"/>
        <feColorMatrix in="blur" type="matrix"
          values="1 0 0 0 0  0 1 0 0 0  0 0 1 0 0  0 0 0 1 0"/>
        <feMerge>
          <feMergeNode/>
          <feMergeNode in="SourceGraphic"/>
        </feMerge>
      </filter>`);

    const tooltip = d3.select('#tooltip');

    this.simulation = d3.forceSimulation(this.nodes)
      .force('link', d3.forceLink<NetworkNode, NetworkLink>(this.links).id(d => d.id).distance(150))
      .force('charge', d3.forceManyBody().strength(-300))
      .force('center', d3.forceCenter(width / 2, height / 2));

    this.linkLayer = svgElement.append('g');
    const link = this.linkLayer
      .attr('stroke', '#999')
      .attr('stroke-opacity', 0.6)
      .selectAll('line')
      .data(this.links)
      .join('line')
      .attr('stroke-width', d => d.strength);

    this.nodeLayer = svgElement.append('g');
    const node = this.nodeLayer
      .selectAll('image')
      .data(this.nodes)
      .join('image')
      .attr('xlink:href', d => d.image || 'default.png')
      .attr('width', 36)
      .attr('height', 36)
      .attr('clip-path', 'circle(18px at 18px 18px)')
      .on('mouseover', (event, d) => {
        tooltip.style('opacity', '1')
          .style('left', `${event.pageX + 10}px`)
          .style('top', `${event.pageY - 10}px`)
          .html(`<strong>${d.name}</strong><br/>Knowledge: ${d.knowledge.join(', ') || 'None'}`);
      })
      .on('mouseout', () => tooltip.style('opacity', '0'))
      .call(this.drag(this.simulation) as any);

    this.iconLayer = svgElement.append('g');
    const label = svgElement.append('g')
      .selectAll('text')
      .data(this.nodes)
      .join('text')
      .text(d => d.name)
      .attr('fill', 'black')
      .attr('text-anchor', 'middle')
      .attr('dy', 30)
      .attr('font-size', 10)
      .attr('pointer-events', 'none');

    const icons = this.iconLayer.selectAll('text')
      .data(this.nodes)
      .join('text')
      .text('')
      .attr('text-anchor', 'start')
      .attr('font-size', 18)
      .attr('fill', 'white')
      .attr('opacity', 0);

    this.simulation.on('tick', () => {
      link
        .attr('x1', d => (d.source as NetworkNode).x!)
        .attr('y1', d => (d.source as NetworkNode).y!)
        .attr('x2', d => (d.target as NetworkNode).x!)
        .attr('y2', d => (d.target as NetworkNode).y!);

      node
        .attr('x', d => d.x! - 18)
        .attr('y', d => d.y! - 18);

      label
        .attr('x', d => d.x!)
        .attr('y', d => d.y! + 30);

      icons
        .attr('x', d => d.x! + 22)
        .attr('y', d => d.y! - 10);
    });
  }

  private drag(simulation: d3.Simulation<NetworkNode, NetworkLink>) {
    return d3.drag<SVGImageElement, NetworkNode>()
      .on('start', (event, d) => {
        if (!event.active) simulation.alphaTarget(0.3).restart();
        d.fx = d.x;
        d.fy = d.y;
      })
      .on('drag', (event, d) => {
        d.fx = event.x;
        d.fy = event.y;
      })
      .on('end', (event, d) => {
        if (!event.active) simulation.alphaTarget(0);
        d.fx = null;
        d.fy = null;
      });
  }

  private initializeSignalR(): void {
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(`${this.configService.apiUrl}/hubs/activities`)
      .configureLogging(signalR.LogLevel.Information)
      .withAutomaticReconnect()
      .build();

    this.connection.on('show', (eventId: string, npcId: string, type: string, message: any, time: string) => {
      this.handleSignalRUpdate(eventId, npcId, type, message, time);
    });

    this.connection.start()
      .then(() => console.log('SignalR connected'))
      .catch(err => console.error('SignalR connection error:', err));
  }

  private handleSignalRUpdate(eventId: string, npcId: string, type: string, message: any, time: string): void {
    const n1 = this.nodes.find(x => x.id === npcId);
    const npcName = n1?.name || npcId;
    this.logUpdate(type, `${type} event from ${npcName}: ${typeof message === 'string' ? message : JSON.stringify(message)}`);

    if (type === 'chat' || type === 'relationship') this.updateMood('happy', 1);
    if (type === 'belief' || type === 'activity-other') this.updateMood('sad', -1);

    if (type === 'relationship' && typeof message === 'string' && message.includes('improved relationship')) {
      this.handleRelationshipImprovement(message);
    }

    const n = this.nodes.find(node => node.id === npcId);
    if (!n) return;

    // Flash the node
    d3.selectAll('image')
      .filter((d: any) => d.id === npcId)
      .transition().duration(150)
      .attr('opacity', 0.85)
      .transition().duration(1000)
      .attr('opacity', 1);

    this.flashIcon(npcId, type);
    this.showMessageBubble(n, type, message);
  }

  private handleRelationshipImprovement(message: string): void {
    const parts = message.match(/(.+?) improved relationship with (.+)/);
    if (parts && parts.length === 3) {
      const from = this.nodes.find(n => n.name === parts[1]);
      const to = this.nodes.find(n => n.name === parts[2]);
      if (from && to && this.linkLayer) {
        const tempLine = this.linkLayer.append('line')
          .attr('x1', from.x!)
          .attr('y1', from.y!)
          .attr('x2', to.x!)
          .attr('y2', to.y!)
          .attr('stroke', 'silver')
          .attr('stroke-width', 1)
          .attr('stroke-dasharray', '4,4')
          .attr('opacity', 0.6)
          .style('filter', 'url(#pulse-glow)');

        tempLine.transition().duration(1000)
          .attr('stroke', 'aliceblue')
          .attr('stroke-width', 2);

        tempLine.transition().delay(29000).duration(1000)
          .style('opacity', 0)
          .remove();
      }
    }
  }

  private flashIcon(npcId: string, type: string): void {
    const emojiMap: Record<string, string> = {
      knowledge: '🎓',
      relationship: '🤝',
      chat: '💬',
      belief: '⚡',
      activity: '🧠',
      'activity-other': '❗'
    };
    const emoji = emojiMap[type] || '✨';

    if (this.iconLayer) {
      this.iconLayer.selectAll('text')
        .filter((d: any) => d.id === npcId)
        .text(emoji)
        .attr('opacity', 1)
        .transition().delay(30000).duration(1000)
        .attr('opacity', 0)
        .on('end', function() {
          d3.select(this).text('');
        });
    }
  }

  private showMessageBubble(node: NetworkNode, type: string, message: any): void {
    if (!this.svg) return;

    const bubbleWidth = 280;
    const bubbleHeight = 200;
    const offset = 51;
    const viewportWidth = window.innerWidth;
    const viewportHeight = window.innerHeight;

    // Calculate x position - if node is in right half, put bubble on left side
    let bubbleX: number;
    if (node.x! > viewportWidth / 2) {
      // Node is on right side, put bubble on left
      bubbleX = node.x! - bubbleWidth - offset;
    } else {
      // Node is on left side, put bubble on right
      bubbleX = node.x! + offset;
    }

    // Ensure bubble doesn't go off screen horizontally
    if (bubbleX < 10) {
      bubbleX = 10;
    } else if (bubbleX + bubbleWidth > viewportWidth - 10) {
      bubbleX = viewportWidth - bubbleWidth - 10;
    }

    // Calculate y position - prevent overflow on top and bottom
    let bubbleY = node.y! - 30;
    if (bubbleY < 10) {
      bubbleY = 10;
    } else if (bubbleY + bubbleHeight > viewportHeight - 110) {
      // Account for bottom console (97px + some padding)
      bubbleY = viewportHeight - bubbleHeight - 110;
    }

    const box = this.svg.append('foreignObject')
      .attr('x', bubbleX)
      .attr('y', bubbleY)
      .attr('width', bubbleWidth)
      .attr('height', bubbleHeight)
      .style('opacity', 0)
      .transition().duration(135).style('opacity', 1).selection();

    let html = '';
    if (typeof message === 'object' && message !== null) {
      html = `<div style='color:#000;'>Used <strong>${message.handler}</strong> to ${message.action}<br/>because ${message.reasoning}</div>`;
      if (message.sentiment) {
        this.updateMood(message.sentiment, 1);
      }
    } else {
      html = `<div style='color:#000;'>${message}</div>`;
    }

    box.append('xhtml:div')
      .style('color', '#000')
      .style('background-color', '#fff')
      .style('border', '1px solid silver')
      .style('border-radius', '8px')
      .style('padding', '6px 10px')
      .style('font-size', '12px')
      .style('box-shadow', '2px 2px 6px rgba(0,0,0,0.15)')
      .style('width', '268px')
      .style('max-width', '268px')
      .style('max-height', '188px')
      .style('box-sizing', 'border-box')
      .style('overflow-wrap', 'break-word')
      .style('word-break', 'break-word')
      .style('white-space', 'normal')
      .style('line-height', '1.4')
      .style('font-family', 'monospace')
      .style('overflow-y', 'auto')
      .html(html);

    box.transition()
      .delay(8000)
      .duration(1000)
      .style('opacity', 0)
      .remove();
  }

  private updateMood(label: string, value: number): void {
    const existing = this.moodState.find(m => m.label === label);
    if (existing) {
      existing.value += value;
    } else {
      this.moodState.push({ label, value });
    }

    const top = this.moodState
      .filter(m => typeof m.value === 'number')
      .sort((a, b) => Math.abs(b.value) - Math.abs(a.value))
      .slice(0, 2);

    const display = top.map(m => `${this.addEmoji(this.capitalize(m.label))} (${m.value})`).join(', ');
    const moodMeter = document.getElementById('mood-meter');
    if (moodMeter) {
      moodMeter.innerText = `Range Mood: ${display || '😐 Neutral'}`;
    }
  }

  private capitalize(s: string): string {
    return s.charAt(0).toUpperCase() + s.slice(1);
  }

  private addEmoji(label: string): string {
    const emojiMap: Record<string, string> = {
      Happy: '😁',
      Anxious: '😨',
      Sad: '☹️',
      Angry: '😡',
      Curious: '🤔',
      Confident: '😎',
      Bored: '🥱'
    };
    const emoji = emojiMap[label] || '✨';
    return `${emoji} ${label}`;
  }

  private logUpdate(type: string, message: string): void {
    const emojiMap: Record<string, string> = {
      knowledge: '🎓',
      relationship: '🤝',
      chat: '💬',
      belief: '⚡',
      activity: '🧠',
      'activity-other': '❗'
    };
    const emoji = emojiMap[type] || '✨';
    const consoleDiv = document.getElementById('update-console');
    if (consoleDiv) {
      const entry = document.createElement('div');
      const time = new Date().toLocaleTimeString();
      const msgText = typeof message === 'object' && message !== null
        ? `Used ${(message as any).handler} to ${(message as any).action} because ${(message as any).reasoning}`
        : message;
      entry.textContent = `[${time}] ${emoji} ${msgText}`;
      consoleDiv.appendChild(entry);
      consoleDiv.scrollTop = consoleDiv.scrollHeight;
    }
  }
}
