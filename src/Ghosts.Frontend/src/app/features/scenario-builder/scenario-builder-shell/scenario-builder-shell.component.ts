import { Component, OnInit, ViewChild, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { MatStepperModule } from '@angular/material/stepper';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ChangeDetectionStrategy } from '@angular/core';
import { BuilderSourcesComponent } from '../builder-sources/builder-sources.component';
import { BuilderExtractionComponent } from '../builder-extraction/builder-extraction.component';
import { BuilderEntitiesComponent } from '../builder-entities/builder-entities.component';
import { BuilderGraphComponent } from '../builder-graph/builder-graph.component';
import { BuilderEnrichmentComponent } from '../builder-enrichment/builder-enrichment.component';
import { BuilderCompileComponent } from '../builder-compile/builder-compile.component';

@Component({
  selector: 'app-scenario-builder-shell',
  standalone: true,
  imports: [
    CommonModule,
    MatStepperModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    MatProgressSpinnerModule,
    BuilderSourcesComponent,
    BuilderExtractionComponent,
    BuilderEntitiesComponent,
    BuilderGraphComponent,
    BuilderEnrichmentComponent,
    BuilderCompileComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './scenario-builder-shell.component.html',
  styleUrls: ['./scenario-builder-shell.component.scss'],
})
export class ScenarioBuilderShellComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  @ViewChild(BuilderExtractionComponent) extractionComponent?: BuilderExtractionComponent;
  @ViewChild(BuilderEntitiesComponent) entitiesComponent?: BuilderEntitiesComponent;
  @ViewChild(BuilderGraphComponent) graphComponent?: BuilderGraphComponent;
  @ViewChild(BuilderEnrichmentComponent) enrichmentComponent?: BuilderEnrichmentComponent;

  protected readonly scenarioId = signal<number | null>(null);
  protected readonly loading = signal(true);
  protected readonly assistantOpen = signal(false);

  ngOnInit(): void {
    // Get 'id' from parent route since we're using loadChildren
    const id = this.route.parent?.snapshot.paramMap.get('id') ?? this.route.snapshot.paramMap.get('id');
    if (id) {
      this.scenarioId.set(+id);
      this.loading.set(false);
    } else {
      this.router.navigate(['/scenarios']);
    }
  }

  protected toggleAssistant(): void {
    this.assistantOpen.update((open) => !open);
  }

  protected backToScenarios(): void {
    this.router.navigate(['/scenarios']);
  }

  protected onStepChange(event: any): void {
    const stepIndex = event.selectedIndex;

    // Refresh data when entering certain steps
    switch (stepIndex) {
      case 1: // Extraction step
        this.extractionComponent?.refresh();
        break;
      case 2: // Entities step
        this.entitiesComponent?.refresh();
        break;
      case 3: // Graph step
        // Use setTimeout to ensure DOM is ready
        setTimeout(() => this.graphComponent?.refresh(), 100);
        break;
      case 4: // Enrichment step
        this.enrichmentComponent?.refresh();
        break;
    }
  }
}
