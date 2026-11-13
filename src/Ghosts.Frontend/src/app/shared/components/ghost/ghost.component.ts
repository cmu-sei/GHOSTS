import { ChangeDetectionStrategy, Component } from '@angular/core';

/** The GHOSTS ghost, arms up with the little one on his chest. Drawn in the brand greens, so he follows the theme. */
@Component({
  selector: 'app-ghost',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <svg viewBox="0 0 120 120" role="img" aria-label="GHOSTS">
      <defs>
        <linearGradient id="gh-ghost-body" x1="0.15" y1="0" x2="0.7" y2="1">
          <stop offset="0" style="stop-color: var(--gh-brand-light)" />
          <stop offset="1" style="stop-color: var(--gh-brand-dark)" />
        </linearGradient>
        <radialGradient id="gh-ghost-sheen" cx="0.36" cy="0.18" r="0.55">
          <stop offset="0" stop-color="#fff" stop-opacity="0.32" />
          <stop offset="1" stop-color="#fff" stop-opacity="0" />
        </radialGradient>
      </defs>
      <g stroke="url(#gh-ghost-body)" stroke-width="12" stroke-linecap="round" fill="none">
        <path d="M33 62 C22 56 17 46 15 33" />
        <path d="M87 62 C98 56 103 46 105 33" />
      </g>
      <path fill="url(#gh-ghost-body)"
            d="M26 54 A34 34 0 0 1 94 54 V100 a11.33 7.5 0 0 1 -22.67 0 a11.33 7.5 0 0 1 -22.67 0 a11.33 7.5 0 0 1 -22.66 0 Z" />
      <path fill="url(#gh-ghost-sheen)" d="M26 54 A34 34 0 0 1 94 54 V100 H26 Z" />
      <ellipse cx="47.5" cy="46" rx="5.5" ry="7.5" fill="#fff" />
      <ellipse cx="72.5" cy="46" rx="5.5" ry="7.5" fill="#fff" />
      <path fill="#fff" d="M42 79 A18 18 0 0 1 78 79 V92 a6 4 0 0 1 -12 0 a6 4 0 0 1 -12 0 a6 4 0 0 1 -12 0 Z" />
      <circle cx="54" cy="78" r="3.4" fill="#0f3d24" />
      <circle cx="66" cy="78" r="3.4" fill="#0f3d24" />
    </svg>`,
  styles: [`
    :host { display: block; }
    svg { display: block; width: 100%; height: auto; filter: drop-shadow(0 0 24px var(--gh-brand-glow)); }
  `]
})
export class GhostComponent {}
