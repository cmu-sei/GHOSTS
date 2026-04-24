import {
  Component,
  OnInit,
  OnDestroy,
  signal,
  inject,
  ElementRef,
  ViewChild,
  AfterViewInit,
  ChangeDetectionStrategy,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { Subscription, interval, Subject } from 'rxjs';
import { debounceTime, switchMap } from 'rxjs/operators';
import maplibregl from 'maplibre-gl';
import { MapboxOverlay } from '@deck.gl/mapbox';
import { ScatterplotLayer, TextLayer } from '@deck.gl/layers';

import { ExecutionService } from '../../core/services/execution.service';
import { ExecutionMapService } from '../../core/services/execution-map.service';
import { Execution } from '../../core/models/execution.model';
import {
  GeoJsonFeatureCollection,
  GeoJsonFeature,
  MapLayerInfo,
  MapTimelineInfo,
  MapTimelineBucket,
  MapEntityDetail,
  MapSearchResult,
  FEATURE_TYPE_COLORS,
  STATUS_COLORS,
} from '../../core/models/execution-map.model';

// ── Dark basemap style ──
const DARK_STYLE = 'https://basemaps.cartocdn.com/gl/dark-matter-gl-style/style.json';

// ── Color helpers for deck.gl (RGBA 0-255 arrays) ──

function hexToRgb(hex: string): [number, number, number] {
  return [
    parseInt(hex.slice(1, 3), 16),
    parseInt(hex.slice(3, 5), 16),
    parseInt(hex.slice(5, 7), 16),
  ];
}

const STATUS_RGBA: Record<string, [number, number, number]> = {};
for (const [k, v] of Object.entries(STATUS_COLORS)) {
  STATUS_RGBA[k] = hexToRgb(v);
}
const DEFAULT_STATUS_RGB: [number, number, number] = [100, 116, 139];

const CATEGORY_HEX: Record<string, string> = {
  AttackPath: '#ff4466',
  C2: '#ff8833',
  Backbone: '#22d3ee',
  LAN: '#22d3ee',
  VPN: '#a78bfa',
  Replication: '#fbbf24',
  WAN: '#19C3FF',
};

// Glow colors — alpha baked into the color string (not line-opacity).
// This matches OpenGridWorks' approach where the glow layer uses
// rgba colors at line-opacity:1 so that line-blur spreads the
// semi-transparent color further and more evenly.
const CATEGORY_GLOW: Record<string, string> = {
  AttackPath: 'rgba(255, 68, 102, 0.7)',
  C2: 'rgba(255, 136, 51, 0.65)',
  Backbone: 'rgba(34, 211, 238, 0.6)',
  LAN: 'rgba(34, 211, 238, 0.6)',
  VPN: 'rgba(167, 139, 250, 0.6)',
  Replication: 'rgba(251, 191, 36, 0.6)',
  WAN: 'rgba(25, 195, 255, 0.6)',
};

const RADIUS_BY_TYPE: Record<string, number> = {
  Site: 10,
  Machine: 7,
  Npc: 8,
  Event: 8,
  Poi: 9,
  ScenarioEntity: 8,
};


@Component({
  selector: 'app-execution-map',
  standalone: true,
  imports: [CommonModule, RouterModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './execution-map.component.html',
  styleUrls: ['./execution-map.component.scss'],
})
export class ExecutionMapComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('mapContainer', { static: true }) mapContainer!: ElementRef;

  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly executionService = inject(ExecutionService);
  private readonly mapService = inject(ExecutionMapService);

  // ── State ──
  readonly executionId = signal(0);
  readonly execution = signal<Execution | null>(null);
  readonly layers = signal<MapLayerInfo[]>([]);
  readonly timelineInfo = signal<MapTimelineInfo | null>(null);
  readonly selectedEntity = signal<MapEntityDetail | null>(null);
  readonly searchQuery = signal('');
  readonly searchResults = signal<MapSearchResult[]>([]);
  readonly searchFocused = signal(false);
  readonly loading = signal(false);
  readonly liveRefresh = signal(true);
  readonly panelOpen = signal(false);
  readonly showConnections = signal(true);
  readonly connectionCount = signal(0);
  readonly timelineIndex = signal(0);
  readonly timelinePlaying = signal(false);
  readonly playbackSpeed = signal(1500); // ms per bucket
  readonly hoverInfo = signal<{
    x: number;
    y: number;
    label: string;
    featureType: string;
    status: string;
  } | null>(null);
  readonly eventFeedOpen = signal(true);

  private visibleLayers = new Set<string>();
  private urlInitLayers = false; // true if URL provided initial layer state
  private statusFilter = '';
  private teamFilter = '';
  private map!: maplibregl.Map;
  private deckOverlay!: MapboxOverlay;
  private refreshSub?: Subscription;
  private timelineInterval?: ReturnType<typeof setInterval>;
  private searchSubject = new Subject<string>();
  private allFeatures: GeoJsonFeatureCollection = { type: 'FeatureCollection', features: [] };
  private connectionFeatures: GeoJsonFeatureCollection = { type: 'FeatureCollection', features: [] };
  private globeSpinning = true;
  private spinFrameId?: number;
  private currentZoom = 2.5;
  private flowAnimFrameId?: number;
  // ── Lifecycle ──

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.executionId.set(id);

    // Read layer visibility from URL query params
    const qp = this.route.snapshot.queryParams;
    if (qp['layers']) {
      this.visibleLayers = new Set((qp['layers'] as string).split(','));
      this.urlInitLayers = true;
    }
    if (qp['connections'] === '0') {
      this.showConnections.set(false);
    }

    this.searchSubject
      .pipe(
        debounceTime(300),
        switchMap((q) => this.mapService.search(id, q))
      )
      .subscribe((results) => this.searchResults.set(results));
  }

  ngAfterViewInit(): void {
    this.initMap();
    this.loadData();
    this.startAutoRefresh();
  }

  ngOnDestroy(): void {
    this.refreshSub?.unsubscribe();
    this.stopTimelinePlay();
    this.stopGlobeRotation();
    this.stopFlowAnimation();
    this.map?.remove();
  }

  // ── Map Initialization ──

  private initMap(): void {
    this.map = new maplibregl.Map({
      container: this.mapContainer.nativeElement,
      style: DARK_STYLE,
      center: [42.0, 20.0],
      zoom: 2.5,
    });

    this.map.addControl(
      new maplibregl.NavigationControl({ showCompass: true }),
      'bottom-right'
    );

    // Wait for the style to finish loading before enabling globe + overlays
    this.map.on('style.load', () => {
      // Globe projection must be set after style loads — the style init
      // defaults to mercator and overwrites any constructor value
      this.map.setProjection({ type: 'globe' });

      // deck.gl overlay — non-interleaved so it renders on its own canvas
      // and doesn't flicker during map zoom/pan repaints
      this.deckOverlay = new MapboxOverlay({
        interleaved: false,
        layers: [],
      });
      this.map.addControl(this.deckOverlay as any);

      // Re-render any data that arrived before the style was ready
      this.updateDeckLayers();

      // Flush connection updates that arrived before the style was ready
      if (this.pendingConnectionUpdate) {
        this.pendingConnectionUpdate = false;
        this.updateConnectionLines();
      }

      // Globe auto-rotation
      this.startGlobeRotation();
    });

    // Track zoom for label visibility
    this.map.on('zoom', () => {
      const z = this.map.getZoom();
      const wasShowing = this.currentZoom >= 8;
      this.currentZoom = z;
      if ((z >= 8) !== wasShowing) this.updateDeckLayers();
    });

    const stopSpin = () => this.stopGlobeRotation();
    this.map.on('mousedown', stopSpin);
    this.map.on('touchstart', stopSpin);
    this.map.on('wheel', stopSpin);
  }

  // ── Globe Rotation ──

  private startGlobeRotation(): void {
    this.globeSpinning = true;
    const spin = () => {
      if (!this.globeSpinning || !this.map) return;
      const center = this.map.getCenter();
      center.lng -= 0.1;
      this.map.setCenter(center);
      this.spinFrameId = requestAnimationFrame(spin);
    };
    this.spinFrameId = requestAnimationFrame(spin);
  }

  private stopGlobeRotation(): void {
    this.globeSpinning = false;
    if (this.spinFrameId) {
      cancelAnimationFrame(this.spinFrameId);
      this.spinFrameId = undefined;
    }
  }

  // ── Data Loading ──

  loadData(): void {
    const id = this.executionId();
    if (!id) return;

    this.loading.set(true);

    this.executionService.getExecution(id).subscribe({
      next: (exec) => this.execution.set(exec),
      error: (err) => console.error('Error loading execution:', err),
    });

    this.mapService.getLayers(id).subscribe({
      next: (layers) => {
        this.layers.set(layers);
        // Only use API defaults if URL didn't specify layers
        if (!this.urlInitLayers && this.visibleLayers.size === 0) {
          layers.forEach((l) => {
            if (l.defaultVisible) this.visibleLayers.add(l.layerId);
          });
        }
      },
    });

    this.mapService.getAllFeatures(id).subscribe({
      next: (fc) => {
        this.allFeatures = fc;
        this.updateDeckLayers();
        this.loading.set(false);
        this.fitBounds();
      },
      error: () => this.loading.set(false),
    });

    this.mapService.getConnections(id).subscribe({
      next: (fc) => {
        this.connectionFeatures = fc;
        this.connectionCount.set(fc.features.length);
        this.updateDeckLayers();
      },
    });

    this.mapService.getTimeline(id, 60).subscribe({
      next: (tl) => this.timelineInfo.set(tl),
    });
  }

  refreshData(): void {
    this.loadData();
  }

  // ── deck.gl Layer Management ──

  private getFilteredFeatures(): GeoJsonFeature[] {
    let filtered = this.allFeatures.features;

    if (this.visibleLayers.size > 0) {
      filtered = filtered.filter((f) =>
        this.visibleLayers.has(f.properties.featureType.toLowerCase())
      );
    }

    if (this.statusFilter) {
      filtered = filtered.filter(
        (f) => f.properties.status === this.statusFilter
      );
    }

    if (this.teamFilter) {
      filtered = filtered.filter(
        (f) => f.properties.team === this.teamFilter
      );
    }

    const tl = this.timelineInfo();
    if (tl && tl.buckets.length > 0) {
      const idx = this.timelineIndex();
      const cutoff = tl.buckets[Math.min(idx, tl.buckets.length - 1)].end;
      filtered = filtered.filter((f) => {
        if (!f.properties.validFrom) return true;
        return new Date(f.properties.validFrom) <= new Date(cutoff);
      });
    }

    return filtered.filter((f) => f.geometry.type === 'Point');
  }

  /**
   * Returns the current timeline cutoff date, or null if timeline isn't active.
   */
  private getTimelineCutoff(): Date | null {
    const tl = this.timelineInfo();
    if (!tl || tl.buckets.length === 0) return null;
    const idx = this.timelineIndex();
    const cutoff = tl.buckets[Math.min(idx, tl.buckets.length - 1)].end;
    return new Date(cutoff);
  }

  /**
   * Returns coordinate pairs for events visible at the current timeline
   * position. Used to progressively reveal attack-path connections.
   */
  private getVisibleEventCoords(cutoff: Date): number[][] {
    const coords: number[][] = [];
    for (const f of this.allFeatures.features) {
      const p = f.properties;
      if (p.featureType !== 'Event' || !p.validFrom) continue;
      if (new Date(p.validFrom) <= cutoff) {
        coords.push(f.geometry.coordinates as number[]);
      }
    }
    return coords;
  }

  /**
   * Check if a point [lng, lat] is near any of the event locations.
   * Uses ~1.1 km threshold (0.01 degrees) to handle slight coordinate
   * offsets between events and infrastructure placed in the same area.
   */
  private isNearAnyEvent(point: number[], eventCoords: number[][]): boolean {
    const threshold = 0.01;
    for (const ec of eventCoords) {
      if (Math.abs(point[0] - ec[0]) < threshold && Math.abs(point[1] - ec[1]) < threshold) {
        return true;
      }
    }
    return false;
  }

  private updateDeckLayers(): void {
    if (!this.deckOverlay) {
      this.pendingConnectionUpdate = true;
      return;
    }

    const features = this.getFilteredFeatures();
    const showLabels = this.currentZoom >= 8;

    const deckLayers: any[] = [];

    // Glow layer — larger, transparent circles behind the main dots
    deckLayers.push(
      new ScatterplotLayer({
        id: 'features-glow',
        data: features,
        getPosition: (d: any) => d.geometry.coordinates,
        getRadius: (d: any) =>
          (RADIUS_BY_TYPE[d.properties.featureType] ?? 7) * 2.5,
        getFillColor: (d: any) => {
          const rgb = STATUS_RGBA[d.properties.status] ?? DEFAULT_STATUS_RGB;
          return [rgb[0], rgb[1], rgb[2], 30] as [number, number, number, number];
        },
        radiusUnits: 'pixels' as const,
        radiusMinPixels: 12,
        radiusMaxPixels: 30,
        pickable: false,
        parameters: { depthTest: false },
        updateTriggers: {
          data: [features.length, this.statusFilter, this.teamFilter],
        },
      })
    );

    // Main feature points
    deckLayers.push(
      new ScatterplotLayer({
        id: 'features-main',
        data: features,
        getPosition: (d: any) => d.geometry.coordinates,
        getRadius: (d: any) =>
          RADIUS_BY_TYPE[d.properties.featureType] ?? 7,
        getFillColor: (d: any) => {
          const rgb = STATUS_RGBA[d.properties.status] ?? DEFAULT_STATUS_RGB;
          return [rgb[0], rgb[1], rgb[2], 230] as [number, number, number, number];
        },
        getLineColor: (d: any) => {
          const rgb = STATUS_RGBA[d.properties.status] ?? [148, 163, 184];
          return [
            Math.min(rgb[0] + 80, 255),
            Math.min(rgb[1] + 80, 255),
            Math.min(rgb[2] + 80, 255),
            153,
          ] as [number, number, number, number];
        },
        lineWidthMinPixels: 1.5,
        stroked: true,
        radiusUnits: 'pixels' as const,
        radiusMinPixels: 5,
        radiusMaxPixels: 14,
        pickable: true,
        autoHighlight: true,
        highlightColor: [255, 255, 255, 60],
        onClick: (info: any) => {
          if (info.object) {
            const p = info.object.properties;
            this.selectEntity(p.featureType, p.entityId);
          }
        },
        onHover: (info: any) => {
          if (this.map) {
            this.map.getCanvas().style.cursor = info.object ? 'pointer' : '';
          }
          if (info.object) {
            const p = info.object.properties;
            this.hoverInfo.set({
              x: info.x,
              y: info.y,
              label: p.label,
              featureType: p.featureType,
              status: p.status,
            });
          } else {
            this.hoverInfo.set(null);
          }
        },
        parameters: { depthTest: false },
        updateTriggers: {
          data: [features.length, this.statusFilter, this.teamFilter],
        },
      })
    );

    // Text labels (only at zoom >= 8)
    if (showLabels && features.length > 0) {
      deckLayers.push(
        new TextLayer({
          id: 'features-labels',
          data: features,
          getPosition: (d: any) => d.geometry.coordinates,
          getText: (d: any) => d.properties.label,
          getSize: 12,
          getColor: [203, 213, 225, 200],
          getTextAnchor: 'middle',
          getAlignmentBaseline: 'top',
          getPixelOffset: [0, 18],
          fontFamily: 'Inter, Roboto, Arial, sans-serif',
          outlineColor: [15, 23, 42, 216],
          outlineWidth: 2,
          sizeMinPixels: 10,
          sizeMaxPixels: 14,
          pickable: false,
          parameters: { depthTest: false },
        })
      );
    }

    this.deckOverlay.setProps({ layers: deckLayers });

    // Update native MapLibre connection lines
    this.updateConnectionLines();
  }

  // ── Native MapLibre Connection Lines ──
  // Rendered as native map lines so they follow the globe surface correctly
  // at all zoom levels (deck.gl ArcLayer doesn't work with globe projection).

  private connectionLayersAdded = false;

  private pendingConnectionUpdate = false;

  private updateConnectionLines(): void {
    if (!this.map || !this.map.isStyleLoaded()) {
      this.pendingConnectionUpdate = true;
      return;
    }

    let connections = this.showConnections()
      ? this.connectionFeatures.features
      : [];

    // Filter attack-path / C2 connections by timeline: only show an attack
    // connection once an event has occurred near one of its endpoints.
    const cutoff = this.getTimelineCutoff();
    if (cutoff && connections.length > 0) {
      const eventCoords = this.getVisibleEventCoords(cutoff);

      connections = connections.filter((conn) => {
        const cat = conn.properties.category || '';
        // Infrastructure links always show; only attack/C2 links are temporal
        if (cat !== 'AttackPath') return true;

        // Connection geometry is a LineString [source, target]
        const coords = conn.geometry.coordinates as number[][];
        if (!coords || coords.length < 2) return false;

        const start = coords[0];
        const end = coords[coords.length - 1];

        return this.isNearAnyEvent(start, eventCoords)
            || this.isNearAnyEvent(end, eventCoords);
      });
    }

    // Group connections by category for separate colored layers
    const categories = new Set<string>();
    for (const f of connections) {
      categories.add(f.properties.category || '_default');
    }

    // Build a single GeoJSON source with all connections
    const geojson: any = {
      type: 'FeatureCollection',
      features: connections.map((f) => {
        const coords = f.geometry.coordinates as number[][];
        // Densify the line to make it follow the great-circle path on the globe.
        // MapLibre interpolates linearly between vertices, so 2 vertices =
        // a straight line through the globe interior. Adding intermediate
        // points along the great circle keeps the line on the surface.
        const densified = this.densifyGreatCircle(
          coords[0] as [number, number],
          coords[1] as [number, number],
          32
        );
        return {
          type: 'Feature',
          geometry: { type: 'LineString', coordinates: densified },
          properties: {
            category: f.properties.category || '_default',
            label: f.properties.label,
            status: f.properties.status,
          },
        };
      }),
    };

    // Add or update source
    const src = this.map.getSource('connections-src') as maplibregl.GeoJSONSource;
    if (src) {
      src.setData(geojson);
    } else {
      this.map.addSource('connections-src', { type: 'geojson', data: geojson });
    }

    this.map.triggerRepaint();

    // ── "Electric" multi-layer line stack ──
    // Matched to OpenGridWorks transmission/pipeline rendering:
    //   1. connections-glow  – very wide, heavily blurred halo (the neon bloom)
    //   2. connections-core  – solid bright line, no dashes
    // Key insight from OGW: the glow layer uses blur 12 + width 6-20
    // at opacity 0.35, making the bloom *much* larger than the core.

    const categoryColor: maplibregl.ExpressionSpecification = [
      'match', ['get', 'category'],
      'AttackPath', CATEGORY_HEX['AttackPath'],
      'C2', CATEGORY_HEX['C2'],
      'Backbone', CATEGORY_HEX['Backbone'],
      'WAN', CATEGORY_HEX['WAN'],
      'LAN', CATEGORY_HEX['LAN'],
      'VPN', CATEGORY_HEX['VPN'],
      'Replication', CATEGORY_HEX['Replication'],
      '#475569',
    ];

    // Glow color expression — alpha baked into the rgba color string.
    // OGW uses this approach (e.g. rgba(255,102,0,0.35) at opacity 1)
    // because line-blur interacts differently with color-alpha vs
    // line-opacity: color-alpha spreads the bloom further and softer.
    const glowColor: maplibregl.ExpressionSpecification = [
      'match', ['get', 'category'],
      'AttackPath', CATEGORY_GLOW['AttackPath'],
      'C2', CATEGORY_GLOW['C2'],
      'Backbone', CATEGORY_GLOW['Backbone'],
      'WAN', CATEGORY_GLOW['WAN'],
      'LAN', CATEGORY_GLOW['LAN'],
      'VPN', CATEGORY_GLOW['VPN'],
      'Replication', CATEGORY_GLOW['Replication'],
      'rgba(71, 85, 105, 0.35)',
    ];

    if (!this.connectionLayersAdded) {
      // Outer glow — wide, soft bloom (like OGW planned-tx-glow)
      this.map.addLayer({
        id: 'connections-glow',
        type: 'line',
        source: 'connections-src',
        paint: {
          'line-color': glowColor,
          'line-opacity': 1,
          'line-width': [
            'match', ['get', 'category'],
            'AttackPath', 18,
            'C2', 16,
            'Backbone', 14,
            'WAN', 14,
            'VPN', 12,
            'Replication', 12,
            10,
          ],
          'line-blur': 14,
        },
        layout: { 'line-cap': 'round', 'line-join': 'round' },
      });

      // Inner glow — tighter, brighter halo (like OGW gas-pipeline-glow)
      // This second glow layer concentrates the bloom near the core,
      // making the neon effect much more intense and visible.
      this.map.addLayer({
        id: 'connections-glow-inner',
        type: 'line',
        source: 'connections-src',
        paint: {
          'line-color': glowColor,
          'line-opacity': 1,
          'line-width': [
            'match', ['get', 'category'],
            'AttackPath', 8,
            'C2', 7,
            'Backbone', 6,
            'WAN', 6,
            'VPN', 5,
            'Replication', 5,
            4,
          ],
          'line-blur': 5,
        },
        layout: { 'line-cap': 'round', 'line-join': 'round' },
      });

      // Core layer — solid bright line
      this.map.addLayer({
        id: 'connections-core',
        type: 'line',
        source: 'connections-src',
        paint: {
          'line-color': categoryColor,
          'line-opacity': 0.9,
          'line-width': [
            'match', ['get', 'category'],
            'AttackPath', 2.5,
            'C2', 2,
            'Backbone', 1.8,
            'WAN', 1.8,
            'VPN', 1.5,
            'Replication', 1.5,
            1.2,
          ],
        },
        layout: { 'line-cap': 'round', 'line-join': 'round' },
      });

      // Hover interaction on the core line
      this.map.on('mouseenter', 'connections-core', () => {
        this.map.getCanvas().style.cursor = 'pointer';
      });
      this.map.on('mouseleave', 'connections-core', () => {
        this.map.getCanvas().style.cursor = '';
        this.hoverInfo.set(null);
      });
      this.map.on('mousemove', 'connections-core', (e: any) => {
        if (e.features && e.features.length > 0) {
          const p = e.features[0].properties;
          this.hoverInfo.set({
            x: e.point.x,
            y: e.point.y,
            label: p.label,
            featureType: 'Connection',
            status: p.category ?? p.status,
          });
        }
      });

      this.connectionLayersAdded = true;
      this.startFlowAnimation();
    }

    // Toggle visibility — only hide when user has explicitly turned connections off
    const vis = this.showConnections() ? 'visible' : 'none';
    for (const layerId of ['connections-glow', 'connections-glow-inner', 'connections-core']) {
      this.map.setLayoutProperty(layerId, 'visibility', vis);
    }
  }

  // ── Electric flow animation ──
  // Matches the OpenGridWorks approach:
  //  - Glow "pulse": opacity oscillates 0.12 – 0.55 on a ~3s sin wave
  //  - Core "flow":  dash offset cycles 0→12 over ~3s for current-flow look
  //  - Core "throb": subtle width oscillation ±8% on a ~4s sin wave

  private lastGlowPhase = -1;

  private startFlowAnimation(): void {
    if (this.flowAnimFrameId) return;
    let last = 0;

    const animate = (ts: number) => {
      this.flowAnimFrameId = requestAnimationFrame(animate);

      // ~15 fps throttle — subtle animation doesn't need high framerate
      if (ts - last < 66) return;
      last = ts;

      if (!this.map || !this.map.getLayer('connections-glow')) return;

      // ── Glow breath (4 s period) ──
      // Pulse the line-blur between 10–18 to create a breathing bloom.
      // OGW doesn't animate, but a subtle breath adds life to our
      // fewer, longer lines without changing the color approach.
      const phase = Math.round(100 * Math.sin(ts % 4000 / 4000 * Math.PI * 2)) / 100;
      if (phase === this.lastGlowPhase) return;
      this.lastGlowPhase = phase;
      const blur = 12 + 4 * phase; // 8 – 16
      this.map.setPaintProperty('connections-glow', 'line-blur', blur);
    };

    this.flowAnimFrameId = requestAnimationFrame(animate);
  }

  private stopFlowAnimation(): void {
    if (this.flowAnimFrameId) {
      cancelAnimationFrame(this.flowAnimFrameId);
      this.flowAnimFrameId = undefined;
    }
  }

  /**
   * Densify a two-point line into a great-circle arc with `n` segments.
   * This ensures the line follows the globe surface rather than cutting
   * through the interior when MapLibre renders on a globe projection.
   *
   * Longitudes are unwrapped (allowed to exceed ±180°) so that arcs
   * crossing the antimeridian render as a continuous line rather than
   * wrapping the wrong way around the globe.
   */
  private densifyGreatCircle(
    start: [number, number],
    end: [number, number],
    segments: number
  ): [number, number][] {
    const toRad = Math.PI / 180;
    const toDeg = 180 / Math.PI;

    const lon1 = start[0] * toRad;
    const lat1 = start[1] * toRad;
    const lon2 = end[0] * toRad;
    const lat2 = end[1] * toRad;

    const d =
      2 *
      Math.asin(
        Math.sqrt(
          Math.pow(Math.sin((lat2 - lat1) / 2), 2) +
            Math.cos(lat1) * Math.cos(lat2) * Math.pow(Math.sin((lon2 - lon1) / 2), 2)
        )
      );

    if (d < 1e-10) return [start, end];

    const points: [number, number][] = [];
    let prevLon = start[0];
    for (let i = 0; i <= segments; i++) {
      const f = i / segments;
      const A = Math.sin((1 - f) * d) / Math.sin(d);
      const B = Math.sin(f * d) / Math.sin(d);
      const x = A * Math.cos(lat1) * Math.cos(lon1) + B * Math.cos(lat2) * Math.cos(lon2);
      const y = A * Math.cos(lat1) * Math.sin(lon1) + B * Math.cos(lat2) * Math.sin(lon2);
      const z = A * Math.sin(lat1) + B * Math.sin(lat2);
      const lat = Math.atan2(z, Math.sqrt(x * x + y * y));
      let lon = Math.atan2(y, x) * toDeg;

      // Unwrap longitude: if the jump from the previous point is > 180°,
      // shift by 360° to keep the arc continuous. This prevents MapLibre
      // from drawing a line the long way around when crossing ±180°.
      const delta = lon - prevLon;
      if (delta > 180) lon -= 360;
      else if (delta < -180) lon += 360;
      prevLon = lon;

      points.push([lon, lat * toDeg]);
    }
    return points;
  }

  // ── Layer Toggles ──

  isLayerVisible(layerId: string): boolean {
    return this.visibleLayers.has(layerId);
  }

  toggleLayer(layerId: string): void {
    if (this.visibleLayers.has(layerId)) {
      this.visibleLayers.delete(layerId);
    } else {
      this.visibleLayers.add(layerId);
    }
    this.updateDeckLayers();
    this.syncLayerStateToUrl();
  }

  toggleConnections(): void {
    this.showConnections.set(!this.showConnections());
    this.updateDeckLayers();
    this.syncLayerStateToUrl();
  }

  // ── URL State Sync ──

  private syncLayerStateToUrl(): void {
    const layerParam = Array.from(this.visibleLayers).sort().join(',');
    const connParam = this.showConnections() ? null : '0';
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        layers: layerParam || null,
        connections: connParam,
      },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  // ── Filters ──

  onStatusFilterChange(event: Event): void {
    this.statusFilter = (event.target as HTMLSelectElement).value;
    this.updateDeckLayers();
  }

  onTeamFilterChange(event: Event): void {
    this.teamFilter = (event.target as HTMLSelectElement).value;
    this.updateDeckLayers();
  }

  // ── Search ──

  onSearchInput(event: Event): void {
    const q = (event.target as HTMLInputElement).value;
    this.searchQuery.set(q);
    if (q.length >= 2) {
      this.searchSubject.next(q);
    } else {
      this.searchResults.set([]);
    }
  }

  executeSearch(): void {
    const q = this.searchQuery();
    if (q.length >= 2) {
      this.searchSubject.next(q);
    }
  }

  clearSearch(): void {
    this.searchQuery.set('');
    this.searchResults.set([]);
  }

  onSearchBlur(): void {
    setTimeout(() => this.searchFocused.set(false), 200);
  }

  flyToResult(result: MapSearchResult): void {
    this.stopGlobeRotation();
    this.map.flyTo({
      center: [result.longitude, result.latitude],
      zoom: 16,
      duration: 1200,
    });
    this.searchResults.set([]);
    this.searchFocused.set(false);
    this.selectEntity(result.featureType, result.entityId);
  }

  // ── Entity Selection ──

  selectEntity(featureType: string, entityId: string): void {
    this.mapService
      .getEntityDetail(this.executionId(), featureType, entityId)
      .subscribe({
        next: (detail) => this.selectedEntity.set(detail),
        error: () => this.selectedEntity.set(null),
      });
  }

  // ── Timeline ──

  onTimelineChange(event: Event): void {
    const val = Number((event.target as HTMLInputElement).value);
    this.timelineIndex.set(val);
    this.updateDeckLayers();
  }

  timelinePlay(): void {
    if (this.timelinePlaying()) {
      this.stopTimelinePlay();
    } else {
      this.timelinePlaying.set(true);
      this.startTimelineInterval();
    }
  }

  private startTimelineInterval(): void {
    const tl = this.timelineInfo();
    if (!tl) return;
    if (this.timelineInterval) clearInterval(this.timelineInterval);
    this.timelineInterval = setInterval(() => {
      const idx = this.timelineIndex();
      if (idx >= tl.buckets.length - 1) {
        this.stopTimelinePlay();
        return;
      }
      this.timelineIndex.set(idx + 1);
      this.updateDeckLayers();
    }, this.playbackSpeed());
  }

  cyclePlaybackSpeed(): void {
    const speeds = [2500, 1500, 800, 400];
    const current = this.playbackSpeed();
    const idx = speeds.indexOf(current);
    const next = speeds[(idx + 1) % speeds.length];
    this.playbackSpeed.set(next);
    // Restart interval at new speed if currently playing
    if (this.timelinePlaying()) {
      this.startTimelineInterval();
    }
  }

  getSpeedLabel(): string {
    const ms = this.playbackSpeed();
    if (ms >= 2500) return '0.5x';
    if (ms >= 1500) return '1x';
    if (ms >= 800) return '2x';
    return '4x';
  }

  timelineReset(): void {
    this.stopTimelinePlay();
    this.timelineIndex.set(0);
    this.updateDeckLayers();
  }

  private stopTimelinePlay(): void {
    this.timelinePlaying.set(false);
    if (this.timelineInterval) {
      clearInterval(this.timelineInterval);
      this.timelineInterval = undefined;
    }
  }

  getCurrentTimeLabel(): string {
    const tl = this.timelineInfo();
    if (!tl || tl.buckets.length === 0) return '';
    const idx = Math.min(this.timelineIndex(), tl.buckets.length - 1);
    return this.formatTimeLabel(tl.buckets[idx].end);
  }

  getVisibleEventCount(): number {
    const cutoff = this.getTimelineCutoff();
    if (!cutoff) return 0;
    let count = 0;
    for (const f of this.allFeatures.features) {
      const p = f.properties;
      if (p.featureType !== 'Event' || !p.validFrom) continue;
      if (new Date(p.validFrom) <= cutoff) count++;
    }
    return count;
  }

  getBarHeight(count: number, tl: MapTimelineInfo): number {
    const maxCount = Math.max(...tl.buckets.map((b) => b.count), 1);
    return Math.max((count / maxCount) * 100, 5);
  }

  formatBucketTooltip(bucket: MapTimelineBucket): string {
    return `${this.formatTimeLabel(bucket.start)} \u2014 ${bucket.count} events`;
  }

  // ── Live Refresh ──

  toggleLiveRefresh(): void {
    this.liveRefresh.set(!this.liveRefresh());
    if (this.liveRefresh()) {
      this.startAutoRefresh();
    } else {
      this.refreshSub?.unsubscribe();
    }
  }

  private startAutoRefresh(): void {
    this.refreshSub?.unsubscribe();
    this.refreshSub = interval(10000).subscribe(() => {
      if (this.liveRefresh()) {
        const id = this.executionId();
        this.mapService.getAllFeatures(id).subscribe({
          next: (fc) => {
            this.allFeatures = fc;
            this.updateDeckLayers();
          },
        });
        this.mapService.getConnections(id).subscribe({
          next: (fc) => {
            this.connectionFeatures = fc;
            this.connectionCount.set(fc.features.length);
            this.updateConnectionLines();
          },
        });
      }
    });
  }

  // ── Map Controls ──

  fitBounds(): void {
    const features = this.allFeatures.features.filter(
      (f) => f.geometry.type === 'Point'
    );
    if (features.length === 0) return;

    this.stopGlobeRotation();

    const bounds = new maplibregl.LngLatBounds();
    for (const f of features) {
      const coords = f.geometry.coordinates as number[];
      bounds.extend([coords[0], coords[1]]);
    }

    this.map.fitBounds(bounds, { padding: 80, maxZoom: 16, duration: 1200 });
  }

  // ── Helpers ──

  getFeatureIcon(featureType: string): string {
    const icons: Record<string, string> = {
      Site: 'fa-building',
      Machine: 'fa-server',
      Npc: 'fa-user-secret',
      Event: 'fa-bolt',
      Poi: 'fa-map-marker-alt',
      ScenarioEntity: 'fa-puzzle-piece',
      Connection: 'fa-project-diagram',
      Network: 'fa-network-wired',
    };
    return icons[featureType] ?? 'fa-circle';
  }

  getLayerColor(featureType: string): string {
    return FEATURE_TYPE_COLORS[featureType] ?? '#64748b';
  }

  getStatusColor(status: string): string {
    return STATUS_COLORS[status] ?? '#64748b';
  }

  formatTimeLabel(dateStr: string): string {
    const d = new Date(dateStr);
    return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  }

  objectKeys(obj: Record<string, unknown>): string[] {
    return Object.keys(obj);
  }

  // ── Event Feed ──
  // Returns the most recent events visible at the current timeline position,
  // sorted newest-first. Used by the floating feed panel in the lower-left.

  getVisibleEvents(): {
    label: string;
    description: string;
    status: string;
    category: string;
    time: string;
    featureType: string;
    entityId: string;
    lng: number;
    lat: number;
  }[] {
    const cutoff = this.getTimelineCutoff();
    if (!cutoff) return [];

    const events: {
      label: string;
      description: string;
      status: string;
      category: string;
      time: string;
      featureType: string;
      entityId: string;
      lng: number;
      lat: number;
      date: Date;
    }[] = [];

    for (const f of this.allFeatures.features) {
      const p = f.properties;
      if (p.featureType !== 'Event' || !p.validFrom) continue;
      const d = new Date(p.validFrom);
      if (d <= cutoff) {
        const coords = f.geometry.coordinates as number[];
        events.push({
          label: p.label,
          description: p.description,
          status: p.status,
          category: p.category,
          time: this.formatTimeLabel(p.validFrom),
          featureType: p.featureType,
          entityId: p.entityId,
          lng: coords[0],
          lat: coords[1],
          date: d,
        });
      }
    }

    // Sort newest first, take at most 50
    events.sort((a, b) => b.date.getTime() - a.date.getTime());
    return events.slice(0, 50);
  }

  /**
   * Cinematic fly-to when clicking an event in the feed.
   * Pulls the camera out to a high altitude first, then swoops down
   * to the target location with a slow zoom and pitch tilt — like a
   * drone camera move in a documentary.
   */
  flyToEvent(evt: {
    lng: number;
    lat: number;
    entityId: string;
    featureType: string;
  }): void {
    this.stopGlobeRotation();

    const current = this.map.getCenter();
    const currentZoom = this.map.getZoom();
    const targetZoom = 12;

    // Calculate distance to decide how dramatic the pull-out should be
    const dlng = Math.abs(current.lng - evt.lng);
    const dlat = Math.abs(current.lat - evt.lat);
    const dist = Math.sqrt(dlng * dlng + dlat * dlat);

    // For nearby targets, do a quick swoop; for far ones, pull way out first
    const pullOutZoom = dist < 5 ? Math.min(currentZoom, 6) : dist < 30 ? 3.5 : 2;

    // Phase 1: Pull out + rotate toward target
    this.map.easeTo({
      center: [
        current.lng + (evt.lng - current.lng) * 0.15,
        current.lat + (evt.lat - current.lat) * 0.15,
      ],
      zoom: pullOutZoom,
      pitch: 0,
      bearing: 0,
      duration: 1200,
      easing: (t: number) => t * (2 - t), // ease-out quadratic
    });

    // Phase 2: Swoop down to the target with dramatic pitch
    setTimeout(() => {
      this.map.flyTo({
        center: [evt.lng, evt.lat],
        zoom: targetZoom,
        pitch: 55,
        bearing: -20 + Math.random() * 40, // slight random bearing for variety
        duration: 2800,
        curve: 1.8,
        essential: true,
      });

      // Phase 3: Gentle settle — ease pitch back and nudge zoom
      setTimeout(() => {
        this.map.easeTo({
          pitch: 40,
          bearing: 0,
          zoom: targetZoom + 0.5,
          duration: 1800,
          easing: (t: number) => 1 - Math.pow(1 - t, 3), // ease-out cubic
        });
      }, 2900);
    }, 1300);

    // Select the entity for the detail card
    this.selectEntity(evt.featureType, evt.entityId);
  }

  formatCategoryLabel(category: string): string {
    // Turn camelCase/PascalCase ATT&CK tactic names into readable labels
    // e.g. "InitialAccess" → "Initial Access", "LateralMovement" → "Lateral Movement"
    return category.replace(/([a-z])([A-Z])/g, '$1 $2');
  }

  getCategoryColor(category: string): string {
    const colors: Record<string, string> = {
      Reconnaissance: '#60a5fa',
      InitialAccess: '#f87171',
      Execution: '#fb923c',
      Persistence: '#fbbf24',
      PrivilegeEscalation: '#a78bfa',
      DefenseEvasion: '#c084fc',
      CredentialAccess: '#f472b6',
      Discovery: '#22d3ee',
      LateralMovement: '#34d399',
      Collection: '#a3e635',
      Exfiltration: '#facc15',
      Impact: '#ef4444',
    };
    return colors[category] ?? '#94a3b8';
  }
}
