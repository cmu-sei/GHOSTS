import { Component, ChangeDetectionStrategy, input, computed, ElementRef, viewChild, AfterViewInit, effect } from '@angular/core';
import * as d3 from 'd3';

@Component({
  selector: 'app-gauge',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="gauge-container">
      <svg #gaugeSvg></svg>
      <div class="gauge-value">{{ displayValue() }}</div>
      <div class="gauge-label">{{ label() }}</div>
    </div>
  `,
  styles: [`
    @use 'variables' as *;

    .gauge-container {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 4px;
    }

    svg {
      overflow: visible;
    }

    .gauge-value {
      font-size: 1.5rem;
      font-weight: 700;
      color: $text;
      margin-top: -8px;
    }

    .gauge-label {
      font-size: 0.75rem;
      color: $text-mute;
      text-transform: uppercase;
      letter-spacing: 0.05em;
    }
  `]
})
export class GaugeComponent implements AfterViewInit {
  value = input(0);
  label = input('');
  size = input(100);
  color = input<'auto' | string>('auto');

  svgRef = viewChild<ElementRef<SVGElement>>('gaugeSvg');

  displayValue = computed(() => Math.round(this.value() * 100) + '%');

  private resolvedColor = computed(() => {
    if (this.color() !== 'auto') return this.color();
    const v = this.value();
    if (v < 0.33) return '#22c55e';
    if (v < 0.66) return '#f59e0b';
    return '#ef4444';
  });

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

    const size = this.size();
    const value = this.value();
    const color = this.resolvedColor();
    const thickness = 8;
    const radius = (size - thickness) / 2;

    d3.select(el).selectAll('*').remove();

    const svg = d3.select(el)
      .attr('width', size)
      .attr('height', size * 0.6);

    const g = svg.append('g')
      .attr('transform', `translate(${size / 2}, ${size * 0.55})`);

    const arcGen = d3.arc<any>()
      .innerRadius(radius - thickness)
      .outerRadius(radius)
      .startAngle(-Math.PI * 0.75)
      .cornerRadius(4);

    g.append('path')
      .datum({ endAngle: Math.PI * 0.75 })
      .attr('d', arcGen as any)
      .attr('fill', 'rgba(100, 116, 139, 0.2)');

    g.append('path')
      .datum({ endAngle: -Math.PI * 0.75 + (Math.PI * 1.5 * value) })
      .attr('d', arcGen as any)
      .attr('fill', color);
  }
}
