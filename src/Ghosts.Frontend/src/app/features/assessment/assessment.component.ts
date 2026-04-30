import {
  Component,
  signal,
  computed,
  OnInit,
  OnDestroy,
  ChangeDetectionStrategy,
  ElementRef,
  inject,
} from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatChipsModule } from '@angular/material/chips';
import { RouterLink } from '@angular/router';
import { forkJoin, Subscription } from 'rxjs';
import * as d3 from 'd3';
import { MachineService } from '../../core/services/machine.service';
import { ExecutionService } from '../../core/services/execution.service';
import { Machine, MachineStatus } from '../../core/models/machine.model';
import { ExecutionSummary } from '../../core/models/execution.model';

interface MachineWithAge extends Machine {
  minutesSinceReport: number;
  online: boolean;
}

interface ActivityRecord {
  handler: string;
  createdUtc: string;
  machineId: string;
}

@Component({
  selector: 'app-assessment',
  standalone: true,
  imports: [
    DecimalPipe,
    MatButtonModule,
    MatCardModule,
    MatProgressSpinnerModule,
    MatChipsModule,
    RouterLink,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './assessment.component.html',
  styleUrls: ['./assessment.component.scss'],
})
export class AssessmentComponent implements OnInit, OnDestroy {
  private readonly machineService = inject(MachineService);
  private readonly executionService = inject(ExecutionService);
  private readonly el = inject(ElementRef);

  protected loading = signal(true);
  protected machines = signal<MachineWithAge[]>([]);
  protected executions = signal<ExecutionSummary[]>([]);
  protected activities = signal<ActivityRecord[]>([]);
  protected lastRefresh = signal<Date>(new Date());

  private refreshInterval: ReturnType<typeof setInterval> | null = null;
  private sub: Subscription | null = null;

  protected readonly onlineCount = computed(
    () => this.machines().filter((m) => m.online).length
  );
  protected readonly offlineCount = computed(
    () => this.machines().filter((m) => !m.online).length
  );
  protected readonly totalMachines = computed(() => this.machines().length);

  protected readonly runningExecutions = computed(
    () => this.executions().filter((e) => e.status === 'Running').length
  );
  protected readonly completedExecutions = computed(
    () => this.executions().filter((e) => e.status === 'Completed').length
  );
  protected readonly failedExecutions = computed(
    () => this.executions().filter((e) => e.status === 'Failed').length
  );

  protected readonly topHandlers = computed(() => {
    const counts = new Map<string, number>();
    for (const a of this.activities()) {
      counts.set(a.handler, (counts.get(a.handler) || 0) + 1);
    }
    return [...counts.entries()]
      .sort((a, b) => b[1] - a[1])
      .slice(0, 8)
      .map(([handler, count]) => ({ handler, count }));
  });

  ngOnInit(): void {
    this.loadAll();
    this.refreshInterval = setInterval(() => this.loadAll(), 30_000);
  }

  ngOnDestroy(): void {
    if (this.refreshInterval) clearInterval(this.refreshInterval);
    this.sub?.unsubscribe();
  }

  protected refresh(): void {
    this.loadAll();
  }

  private loadAll(): void {
    this.sub?.unsubscribe();
    this.sub = forkJoin({
      machines: this.machineService.getMachines(),
      executions: this.executionService.getExecutions(),
    }).subscribe({
      next: ({ machines, executions }) => {
        const now = Date.now();
        const enriched: MachineWithAge[] = machines.map((m) => {
          const lastReport = m.lastReportedUtc
            ? new Date(m.lastReportedUtc).getTime()
            : 0;
          const minutesSinceReport = lastReport
            ? (now - lastReport) / 60_000
            : Infinity;
          return {
            ...m,
            minutesSinceReport,
            online: minutesSinceReport < 10,
          };
        });

        this.machines.set(enriched);
        this.executions.set(executions);
        this.lastRefresh.set(new Date());
        this.loading.set(false);

        this.loadActivitySample(
          enriched
            .filter((m) => m.online)
            .slice(0, 10)
            .map((m) => m.id)
        );

        setTimeout(() => this.renderCharts(), 0);
      },
      error: () => {
        this.loading.set(false);
      },
    });
  }

  private loadActivitySample(machineIds: string[]): void {
    if (machineIds.length === 0) {
      this.activities.set([]);
      return;
    }

    const calls = machineIds.map((id) =>
      this.machineService.getMachineActivity(id, 0, 50)
    );

    forkJoin(calls).subscribe({
      next: (results) => {
        const all: ActivityRecord[] = [];
        results.forEach((records, i) => {
          for (const r of records) {
            all.push({
              handler: r.handler || 'Unknown',
              createdUtc: r.createdUtc,
              machineId: machineIds[i],
            });
          }
        });
        this.activities.set(all);
        setTimeout(() => this.renderCharts(), 0);
      },
    });
  }

  private renderCharts(): void {
    this.renderMachineStatusChart();
    this.renderExecutionStatusChart();
    this.renderHandlerChart();
    this.renderActivityTimeline();
  }

  private renderMachineStatusChart(): void {
    const container = this.el.nativeElement.querySelector('#machine-status-chart');
    if (!container) return;
    container.innerHTML = '';

    const online = this.onlineCount();
    const offline = this.offlineCount();
    if (online + offline === 0) return;

    const data = [
      { label: 'Online', value: online, color: '#28a745' },
      { label: 'Offline', value: offline, color: '#dc3545' },
    ].filter((d) => d.value > 0);

    this.renderDonut(container, data, 180);
  }

  private renderExecutionStatusChart(): void {
    const container = this.el.nativeElement.querySelector('#execution-status-chart');
    if (!container) return;
    container.innerHTML = '';

    const execs = this.executions();
    if (execs.length === 0) return;

    const statusCounts = new Map<string, number>();
    for (const e of execs) {
      statusCounts.set(e.status, (statusCounts.get(e.status) || 0) + 1);
    }

    const colors: Record<string, string> = {
      Created: '#6c757d',
      Running: '#0d6efd',
      Paused: '#ffc107',
      Completed: '#28a745',
      Failed: '#dc3545',
      Cancelled: '#adb5bd',
    };

    const data = [...statusCounts.entries()].map(([label, value]) => ({
      label,
      value,
      color: colors[label] || '#999',
    }));

    this.renderDonut(container, data, 180);
  }

  private renderDonut(
    container: HTMLElement,
    data: { label: string; value: number; color: string }[],
    size: number
  ): void {
    const radius = size / 2;
    const innerRadius = radius * 0.55;
    const total = d3.sum(data, (d) => d.value);

    const svg = d3
      .select(container)
      .append('svg')
      .attr('width', size)
      .attr('height', size)
      .append('g')
      .attr('transform', `translate(${radius},${radius})`);

    const pie = d3
      .pie<{ label: string; value: number; color: string }>()
      .value((d) => d.value)
      .sort(null);

    const arc = d3
      .arc<d3.PieArcDatum<{ label: string; value: number; color: string }>>()
      .innerRadius(innerRadius)
      .outerRadius(radius - 4);

    svg
      .selectAll('path')
      .data(pie(data))
      .enter()
      .append('path')
      .attr('d', arc)
      .attr('fill', (d) => d.data.color)
      .attr('stroke', 'white')
      .attr('stroke-width', 2);

    svg
      .append('text')
      .attr('text-anchor', 'middle')
      .attr('dominant-baseline', 'central')
      .attr('font-size', '24px')
      .attr('font-weight', '600')
      .attr('fill', '#333')
      .text(total.toString());

    const legend = d3
      .select(container)
      .append('div')
      .attr('class', 'chart-legend');

    data.forEach((d) => {
      const item = legend.append('div').attr('class', 'legend-item');
      item
        .append('span')
        .attr('class', 'legend-swatch')
        .style('background-color', d.color);
      item.append('span').text(`${d.label} (${d.value})`);
    });
  }

  private renderHandlerChart(): void {
    const container = this.el.nativeElement.querySelector('#handler-chart');
    if (!container) return;
    container.innerHTML = '';

    const handlers = this.topHandlers();
    if (handlers.length === 0) return;

    const margin = { top: 8, right: 16, bottom: 8, left: 120 };
    const barHeight = 28;
    const width = container.clientWidth || 400;
    const height = margin.top + margin.bottom + handlers.length * barHeight;

    const svg = d3
      .select(container)
      .append('svg')
      .attr('width', width)
      .attr('height', height);

    const maxVal = d3.max(handlers, (d) => d.count) || 1;

    const x = d3
      .scaleLinear()
      .domain([0, maxVal])
      .range([0, width - margin.left - margin.right]);

    const g = svg
      .append('g')
      .attr('transform', `translate(${margin.left},${margin.top})`);

    handlers.forEach((d, i) => {
      const y = i * barHeight;

      g.append('rect')
        .attr('x', 0)
        .attr('y', y + 4)
        .attr('width', x(d.count))
        .attr('height', barHeight - 8)
        .attr('fill', '#2e7d32')
        .attr('rx', 3);

      g.append('text')
        .attr('x', -8)
        .attr('y', y + barHeight / 2)
        .attr('text-anchor', 'end')
        .attr('dominant-baseline', 'central')
        .attr('font-size', '12px')
        .attr('fill', '#555')
        .text(d.handler);

      g.append('text')
        .attr('x', x(d.count) + 6)
        .attr('y', y + barHeight / 2)
        .attr('dominant-baseline', 'central')
        .attr('font-size', '12px')
        .attr('font-weight', '600')
        .attr('fill', '#333')
        .text(d.count.toString());
    });
  }

  private renderActivityTimeline(): void {
    const container = this.el.nativeElement.querySelector('#activity-timeline');
    if (!container) return;
    container.innerHTML = '';

    const acts = this.activities().filter((a) => a.createdUtc);
    if (acts.length === 0) return;

    const parseTime = (s: string) => new Date(s);
    const times = acts.map((a) => parseTime(a.createdUtc).getTime());
    const minTime = d3.min(times) || 0;
    const maxTime = d3.max(times) || 0;
    if (minTime === maxTime) return;

    const bucketCount = 24;
    const bucketSize = (maxTime - minTime) / bucketCount;
    const buckets = new Array(bucketCount).fill(0);

    for (const t of times) {
      const idx = Math.min(
        Math.floor((t - minTime) / bucketSize),
        bucketCount - 1
      );
      buckets[idx]++;
    }

    const margin = { top: 12, right: 16, bottom: 32, left: 40 };
    const width = container.clientWidth || 500;
    const height = 160;

    const svg = d3
      .select(container)
      .append('svg')
      .attr('width', width)
      .attr('height', height);

    const g = svg
      .append('g')
      .attr('transform', `translate(${margin.left},${margin.top})`);

    const innerW = width - margin.left - margin.right;
    const innerH = height - margin.top - margin.bottom;

    const x = d3
      .scaleLinear()
      .domain([0, bucketCount - 1])
      .range([0, innerW]);

    const maxBucket = d3.max(buckets) || 1;
    const y = d3.scaleLinear().domain([0, maxBucket]).range([innerH, 0]);

    const area = d3
      .area<number>()
      .x((_, i) => x(i))
      .y0(innerH)
      .y1((d) => y(d))
      .curve(d3.curveMonotoneX);

    const line = d3
      .line<number>()
      .x((_, i) => x(i))
      .y((d) => y(d))
      .curve(d3.curveMonotoneX);

    g.append('path')
      .datum(buckets)
      .attr('d', area)
      .attr('fill', 'rgba(46, 125, 50, 0.15)');

    g.append('path')
      .datum(buckets)
      .attr('d', line)
      .attr('fill', 'none')
      .attr('stroke', '#2e7d32')
      .attr('stroke-width', 2);

    const timeFormat = d3.timeFormat('%H:%M');
    const xTime = d3
      .scaleTime()
      .domain([new Date(minTime), new Date(maxTime)])
      .range([0, innerW]);

    g.append('g')
      .attr('transform', `translate(0,${innerH})`)
      .call(d3.axisBottom(xTime).ticks(5).tickFormat(timeFormat as any))
      .selectAll('text')
      .attr('font-size', '10px');

    g.append('g')
      .call(d3.axisLeft(y).ticks(4))
      .selectAll('text')
      .attr('font-size', '10px');
  }
}
