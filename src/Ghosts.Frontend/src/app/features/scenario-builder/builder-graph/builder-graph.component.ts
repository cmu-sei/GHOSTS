import { Component, Input, OnInit, OnDestroy, ElementRef, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import * as d3 from 'd3';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatSliderModule } from '@angular/material/slider';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatInputModule } from '@angular/material/input';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { ChangeDetectionStrategy } from '@angular/core';
import { ScenarioBuilderService } from '../../../core/services/scenario-builder.service';
import {
  ScenarioEntity,
  ScenarioEdge,
  ENTITY_TYPES,
  EDGE_TYPES,
  ENTITY_COLORS,
} from '../../../core/models/scenario-builder.model';

interface GraphNode extends d3.SimulationNodeDatum {
  id: string;
  entity: ScenarioEntity;
  radius: number;
}

interface GraphLink extends d3.SimulationLinkDatum<GraphNode> {
  edge: ScenarioEdge;
}

@Component({
  selector: 'app-builder-graph',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatSelectModule,
    MatSliderModule,
    MatCheckboxModule,
    MatInputModule,
    MatTooltipModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './builder-graph.component.html',
  styleUrls: ['./builder-graph.component.scss'],
})
export class BuilderGraphComponent implements OnInit, OnDestroy {
  @Input({ required: true }) scenarioId!: number;

  private readonly builderService = inject(ScenarioBuilderService);
  private readonly elementRef = inject(ElementRef);
  private readonly fb = inject(FormBuilder);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly loading = signal(true);
  protected readonly selectedNode = signal<ScenarioEntity | null>(null);
  protected readonly editMode = signal(false);
  protected readonly relationshipMode = signal(false);
  protected readonly saving = signal(false);
  protected readonly entityTypes = ENTITY_TYPES;
  protected readonly edgeTypes = EDGE_TYPES;
  protected readonly entityColors = ENTITY_COLORS;

  protected filterForm!: FormGroup;
  protected editForm!: FormGroup;
  protected relationshipForm!: FormGroup;

  private svg?: d3.Selection<SVGSVGElement, unknown, null, undefined>;
  private simulation?: d3.Simulation<GraphNode, GraphLink>;
  private allNodes: GraphNode[] = [];
  private allLinks: GraphLink[] = [];
  private entities: ScenarioEntity[] = [];
  private edges: ScenarioEdge[] = [];
  private width = 0;
  private height = 0;
  private isInitialized = false;

  ngOnInit(): void {
    this.initFilterForm();
    this.initEditForm();
    this.initRelationshipForm();
    this.loadGraphData();
    this.isInitialized = true;
  }

  ngOnDestroy(): void {
    this.simulation?.stop();
    this.isInitialized = false;
  }

  // Public method to refresh graph when step becomes active
  public refresh(): void {
    console.log('Refreshing graph data, isInitialized:', this.isInitialized);
    this.retryCount = 0; // Reset retry counter
    this.loadGraphData();
  }

  private initEditForm(): void {
    this.editForm = this.fb.group({
      name: ['', [Validators.required]],
      entityType: [''],
      description: [''],
      confidence: [0.8],
    });
  }

  private initRelationshipForm(): void {
    this.relationshipForm = this.fb.group({
      targetEntityId: ['', Validators.required],
      edgeType: ['Uses', Validators.required],
      label: [''],
      confidence: [0.8],
    });
  }

  private initFilterForm(): void {
    this.filterForm = this.fb.group({
      confidenceThreshold: [0.5],
      selectedTypes: [Array.from(ENTITY_TYPES)],
    });

    this.filterForm.valueChanges.subscribe(() => {
      this.updateVisualization();
    });
  }

  private loadGraphData(): void {
    this.loading.set(true);
    console.log('Loading graph data for scenario', this.scenarioId);
    this.builderService.getGraph(this.scenarioId).subscribe({
      next: (graph) => {
        console.log('Graph data loaded:', {
          nodes: graph.nodes?.length || 0,
          edges: graph.edges?.length || 0,
        });
        this.entities = graph.nodes;
        this.edges = graph.edges;

        if (!this.entities || this.entities.length === 0) {
          console.warn('No entities in graph data');
          this.snackBar.open('No entities found. Run extraction first.', 'Close', { duration: 5000 });
          this.loading.set(false);
          return;
        }

        this.initializeVisualization();
        this.loading.set(false);
      },
      error: (error) => {
        console.error('Error loading graph', error);
        this.snackBar.open('Failed to load graph', 'Close', { duration: 3000 });
        this.loading.set(false);
      },
    });
  }

  private retryCount = 0;
  private readonly maxRetries = 10;

  private initializeVisualization(): void {
    console.log('Initializing visualization, attempt', this.retryCount + 1);

    // Wait for DOM to be ready
    setTimeout(() => {
      const container = this.elementRef.nativeElement.querySelector('.graph-svg-container');
      if (!container) {
        console.warn('Graph container not found, attempt', this.retryCount + 1);
        if (this.retryCount < this.maxRetries) {
          this.retryCount++;
          setTimeout(() => this.initializeVisualization(), 100);
          return;
        }
        console.error('Graph container not found after maximum retries!');
        this.snackBar.open('Graph container not found. Try switching back and forth between tabs.', 'Close', { duration: 5000 });
        return;
      }

      this.width = container.clientWidth;
      this.height = container.clientHeight;
      console.log('Container dimensions:', { width: this.width, height: this.height });

      if (this.width === 0 || this.height === 0) {
        console.warn('Container has zero dimensions, retrying...', this.retryCount + 1);
        if (this.retryCount < this.maxRetries) {
          this.retryCount++;
          setTimeout(() => this.initializeVisualization(), 100);
          return;
        }
      }

      this.retryCount = 0; // Reset for future refreshes
      this.buildVisualization(container);
    }, 50);
  }

  private buildVisualization(container: Element): void {

    // Clear existing SVG
    d3.select(container).selectAll('*').remove();

    this.svg = d3
      .select(container)
      .append('svg')
      .attr('width', this.width)
      .attr('height', this.height);

    // Add defs for patterns and markers
    const defs = this.svg.append('defs');

    // Add subtle grid pattern like MiroFish
    const gridPattern = defs
      .append('pattern')
      .attr('id', 'grid')
      .attr('width', 20)
      .attr('height', 20)
      .attr('patternUnits', 'userSpaceOnUse');

    gridPattern
      .append('path')
      .attr('d', 'M 20 0 L 0 0 0 20')
      .attr('fill', 'none')
      .attr('stroke', '#f0f0f0')
      .attr('stroke-width', 0.5);

    // Add background with grid
    this.svg
      .append('rect')
      .attr('width', this.width)
      .attr('height', this.height)
      .attr('fill', 'url(#grid)')
      .style('pointer-events', 'none');

    // Add zoom behavior
    const zoom = d3
      .zoom<SVGSVGElement, unknown>()
      .scaleExtent([0.1, 4])
      .on('zoom', (event) => {
        g.attr('transform', event.transform);
      });

    this.svg.call(zoom as any);

    // Create container group for zoom/pan
    const g = this.svg.append('g');

    // Add arrow marker for directed edges
    defs
      .append('marker')
      .attr('id', 'arrowhead')
      .attr('viewBox', '-0 -5 10 10')
      .attr('refX', 20)
      .attr('refY', 0)
      .attr('orient', 'auto')
      .attr('markerWidth', 8)
      .attr('markerHeight', 8)
      .append('path')
      .attr('d', 'M 0,-5 L 10,0 L 0,5')
      .attr('fill', '#666666')
      .attr('fill-opacity', 0.4);

    // Prepare data
    this.prepareGraphData();

    // Create simulation (tighter for high-tech look)
    this.simulation = d3
      .forceSimulation(this.allNodes)
      .force(
        'link',
        d3
          .forceLink<GraphNode, GraphLink>(this.allLinks)
          .id((d) => d.id)
          .distance(100)
      )
      .force('charge', d3.forceManyBody().strength(-300))
      .force('center', d3.forceCenter(this.width / 2, this.height / 2))
      .force('collision', d3.forceCollide<GraphNode>().radius((d) => d.radius + 5));

    // Create link elements
    const linkGroup = g.append('g').attr('class', 'links');
    const nodeGroup = g.append('g').attr('class', 'nodes');
    const labelGroup = g.append('g').attr('class', 'labels');

    this.updateVisualization();
  }

  private prepareGraphData(): void {
    // Create nodes with connection-based sizing (smaller, more high-tech)
    const connectionCounts = new Map<string, number>();
    this.edges.forEach((edge) => {
      connectionCounts.set(edge.sourceEntityId, (connectionCounts.get(edge.sourceEntityId) || 0) + 1);
      connectionCounts.set(edge.targetEntityId, (connectionCounts.get(edge.targetEntityId) || 0) + 1);
    });

    this.allNodes = this.entities.map((entity) => ({
      id: entity.id,
      entity,
      radius: Math.min(20, Math.max(8, 8 + (connectionCounts.get(entity.id) || 0) * 1.5)),
    }));

    this.allLinks = this.edges.map((edge) => ({
      source: edge.sourceEntityId,
      target: edge.targetEntityId,
      edge,
    }));

    console.log('Prepared graph data:', {
      nodes: this.allNodes.length,
      links: this.allLinks.length,
    });
  }

  private updateVisualization(): void {
    if (!this.svg || !this.simulation) return;

    const { confidenceThreshold, selectedTypes } = this.filterForm.value;

    // Filter nodes
    const filteredNodes = this.allNodes.filter(
      (node) =>
        selectedTypes.includes(node.entity.entityType) &&
        node.entity.confidence >= confidenceThreshold
    );
    const nodeIds = new Set(filteredNodes.map((n) => n.id));

    // Filter links
    const filteredLinks = this.allLinks.filter(
      (link) =>
        nodeIds.has((link.source as GraphNode).id || (link.source as string)) &&
        nodeIds.has((link.target as GraphNode).id || (link.target as string))
    );

    // Update simulation
    this.simulation.nodes(filteredNodes);
    this.simulation.force(
      'link',
      d3
        .forceLink<GraphNode, GraphLink>(filteredLinks)
        .id((d) => d.id)
        .distance(150)
    );
    // Low alpha so filters don't cause big jumps
    this.simulation.alpha(0.05).restart();

    // Update link elements
    const linkGroup = this.svg.select('.links');
    const link = linkGroup
      .selectAll<SVGLineElement, GraphLink>('line')
      .data(filteredLinks, (d) => d.edge.id);

    link.exit().remove();

    const linkEnter = link
      .enter()
      .append('line')
      .attr('stroke', '#666666')
      .attr('stroke-width', (d) => Math.max(1, (d.edge.weight || 1) * 0.8))
      .attr('stroke-dasharray', (d) => (d.edge.confidence < 0.5 ? '3,3' : '0'))
      .attr('stroke-opacity', 0.4)
      .attr('marker-end', 'url(#arrowhead)');

    const linkMerge = linkEnter.merge(link);

    // Update node elements
    const nodeGroup = this.svg.select('.nodes');
    const node = nodeGroup
      .selectAll<SVGCircleElement, GraphNode>('circle')
      .data(filteredNodes, (d) => d.id);

    node.exit().remove();

    const nodeEnter = node
      .enter()
      .append('circle')
      .attr('r', (d) => d.radius)
      .attr('fill', (d) => this.entityColors[d.entity.entityType] || '#9E9E9E')
      .attr('stroke', '#fff')
      .attr('stroke-width', 1.5)
      .attr('stroke-opacity', 0.8)
      .style('cursor', 'pointer')
      .call(this.drag(this.simulation) as any);

    const nodeMerge = nodeEnter.merge(node);

    // Update label elements
    const labelGroup = this.svg.select('.labels');
    const label = labelGroup
      .selectAll<SVGTextElement, GraphNode>('text')
      .data(filteredNodes, (d) => d.id);

    label.exit().remove();

    const labelEnter = label
      .enter()
      .append('text')
      .text((d) => d.entity.name)
      .attr('text-anchor', 'middle')
      .attr('dy', (d) => d.radius + 12)
      .attr('font-size', 9)
      .attr('fill', '#333333')
      .style('pointer-events', 'none');

    const labelMerge = labelEnter.merge(label);

    // Update positions on tick
    this.simulation.on('tick', () => {
      linkMerge
        .attr('x1', (d) => (d.source as GraphNode).x!)
        .attr('y1', (d) => (d.source as GraphNode).y!)
        .attr('x2', (d) => (d.target as GraphNode).x!)
        .attr('y2', (d) => (d.target as GraphNode).y!);

      nodeMerge.attr('cx', (d) => d.x!).attr('cy', (d) => d.y!);

      labelMerge.attr('x', (d) => d.x!).attr('y', (d) => d.y!);
    });
  }

  private drag(simulation: d3.Simulation<GraphNode, GraphLink>) {
    let dragStartX = 0;
    let dragStartY = 0;
    const CLICK_THRESHOLD = 4; // pixels — below this it's a click, not a drag

    return d3
      .drag<SVGCircleElement, GraphNode>()
      .on('start', (event, d) => {
        dragStartX = event.x;
        dragStartY = event.y;
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
        const dx = event.x - dragStartX;
        const dy = event.y - dragStartY;
        const dist = Math.sqrt(dx * dx + dy * dy);

        if (dist < CLICK_THRESHOLD) {
          // This was a click, not a drag — release the pin and fire click
          d.fx = null;
          d.fy = null;
          this.onNodeClick(d);
        } else {
          // Actual drag — pin the node where the user dropped it
          d.fx = event.x;
          d.fy = event.y;
        }
      });
  }

  private onNodeClick(node: GraphNode): void {
    this.selectedNode.set(node.entity);
    this.editMode.set(false);
    this.relationshipMode.set(false);
  }

  protected closeDetailPanel(): void {
    this.selectedNode.set(null);
    this.editMode.set(false);
    this.relationshipMode.set(false);
  }

  protected startEdit(): void {
    const entity = this.selectedNode();
    if (!entity) return;
    this.editForm.patchValue({
      name: entity.name,
      entityType: entity.entityType,
      description: entity.description,
      confidence: entity.confidence,
    });
    this.relationshipMode.set(false);
    this.editMode.set(true);
  }

  protected cancelEdit(): void {
    this.editMode.set(false);
    this.relationshipMode.set(false);
  }

  protected saveEdit(): void {
    const entity = this.selectedNode();
    if (!entity || this.editForm.invalid) return;
    this.saving.set(true);

    const dto = {
      ...this.editForm.value,
      isReviewed: entity.isReviewed,
    };

    this.builderService.updateEntity(this.scenarioId, entity.id, dto).subscribe({
      next: (updated) => {
        this.selectedNode.set(updated);
        this.editMode.set(false);
        this.saving.set(false);
        this.snackBar.open('Entity updated', 'Close', { duration: 2000 });
        this.loadGraphData();
      },
      error: () => {
        this.saving.set(false);
        this.snackBar.open('Failed to update entity', 'Close', { duration: 3000 });
      },
    });
  }

  protected startAddRelationship(): void {
    this.relationshipForm.reset({ edgeType: 'Uses', confidence: 0.8 });
    this.editMode.set(false);
    this.relationshipMode.set(true);
  }

  protected saveRelationship(): void {
    const entity = this.selectedNode();
    if (!entity || this.relationshipForm.invalid) return;
    this.saving.set(true);

    const { targetEntityId, edgeType, label, confidence } = this.relationshipForm.value;

    this.builderService.createEdge(this.scenarioId, {
      sourceEntityId: entity.id,
      targetEntityId,
      edgeType,
      label: label || edgeType,
      weight: 1,
      confidence,
    }).subscribe({
      next: () => {
        this.relationshipMode.set(false);
        this.saving.set(false);
        this.snackBar.open('Relationship added', 'Close', { duration: 2000 });
        this.loadGraphData();
      },
      error: () => {
        this.saving.set(false);
        this.snackBar.open('Failed to add relationship', 'Close', { duration: 3000 });
      },
    });
  }

  protected getOtherNodes(): GraphNode[] {
    const selected = this.selectedNode();
    if (!selected) return this.allNodes;
    return this.allNodes.filter(n => n.id !== selected.id);
  }

  protected getEntityColor(type: string): string {
    return this.entityColors[type] || '#9E9E9E';
  }

  protected resetView(): void {
    if (!this.svg) return;
    this.svg
      .transition()
      .duration(750)
      .call(
        d3.zoom<SVGSVGElement, unknown>().transform as any,
        d3.zoomIdentity
      );
  }

  protected onTypeToggle(type: string, checked: boolean): void {
    const currentTypes = this.filterForm.value.selectedTypes as string[];
    if (checked) {
      this.filterForm.patchValue({
        selectedTypes: [...currentTypes, type],
      });
    } else {
      this.filterForm.patchValue({
        selectedTypes: currentTypes.filter((t) => t !== type),
      });
    }
  }

  protected formatConfidence(value: number): string {
    return `${(value * 100).toFixed(0)}%`;
  }
}
