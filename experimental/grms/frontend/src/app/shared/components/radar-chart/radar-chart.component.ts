import { Component, ChangeDetectionStrategy, input, ElementRef, viewChild, AfterViewInit, effect } from '@angular/core';
import * as d3 from 'd3';

export interface RadarDataPoint {
  axis: string;
  value: number;
}

@Component({
  selector: 'app-radar-chart',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<svg #radarSvg></svg>`,
  styles: [`
    :host {
      display: block;
    }
    svg {
      width: 100%;
      height: 100%;
      overflow: visible;
    }
  `]
})
export class RadarChartComponent implements AfterViewInit {
  data = input<RadarDataPoint[]>([]);
  size = input(200);
  color = input('#38bdf8');

  svgRef = viewChild<ElementRef<SVGElement>>('radarSvg');

  constructor() {
    effect(() => {
      this.draw();
    });
  }

  ngAfterViewInit(): void {
    this.draw();
  }

  private draw(): void {
    const el = this.svgRef()?.nativeElement;
    if (!el) return;

    const data = this.data();
    if (!data.length) return;

    const size = this.size();
    const color = this.color();
    const margin = 40;
    const radius = (size - margin * 2) / 2;
    const levels = 4;
    const total = data.length;
    const angleSlice = (Math.PI * 2) / total;

    d3.select(el).selectAll('*').remove();

    const svg = d3.select(el)
      .attr('width', size)
      .attr('height', size);

    const g = svg.append('g')
      .attr('transform', `translate(${size / 2}, ${size / 2})`);

    for (let j = 0; j < levels; j++) {
      const levelRadius = radius * ((j + 1) / levels);
      const points = d3.range(total).map(i => {
        const angle = angleSlice * i - Math.PI / 2;
        return [levelRadius * Math.cos(angle), levelRadius * Math.sin(angle)];
      });
      g.append('polygon')
        .attr('points', points.map(p => p.join(',')).join(' '))
        .attr('fill', 'none')
        .attr('stroke', 'rgba(100, 116, 139, 0.2)')
        .attr('stroke-width', 1);
    }

    data.forEach((d, i) => {
      const angle = angleSlice * i - Math.PI / 2;
      g.append('line')
        .attr('x1', 0).attr('y1', 0)
        .attr('x2', radius * Math.cos(angle))
        .attr('y2', radius * Math.sin(angle))
        .attr('stroke', 'rgba(100, 116, 139, 0.15)')
        .attr('stroke-width', 1);

      g.append('text')
        .attr('x', (radius + 14) * Math.cos(angle))
        .attr('y', (radius + 14) * Math.sin(angle))
        .attr('text-anchor', 'middle')
        .attr('dominant-baseline', 'middle')
        .attr('fill', '#94a3b8')
        .attr('font-size', '9px')
        .text(d.axis);
    });

    const radarLine = d3.lineRadial<RadarDataPoint>()
      .radius(d => ((d.value + 1) / 2) * radius)
      .angle((_, i) => i * angleSlice)
      .curve(d3.curveLinearClosed);

    g.append('path')
      .datum(data)
      .attr('d', radarLine as any)
      .attr('fill', color)
      .attr('fill-opacity', 0.15)
      .attr('stroke', color)
      .attr('stroke-width', 2)
      .attr('transform', 'rotate(-90)');

    data.forEach((d, i) => {
      const angle = angleSlice * i - Math.PI / 2;
      const r = ((d.value + 1) / 2) * radius;
      g.append('circle')
        .attr('cx', r * Math.cos(angle))
        .attr('cy', r * Math.sin(angle))
        .attr('r', 3)
        .attr('fill', color);
    });
  }
}
