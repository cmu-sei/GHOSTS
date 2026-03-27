import { Component, Input, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatTabsModule } from '@angular/material/tabs';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { ChangeDetectionStrategy } from '@angular/core';
import { ScenarioBuilderService } from '../../../core/services/scenario-builder.service';
import { AttackService } from '../../../core/services/attack.service';
import {
  ScenarioEntity,
  ScenarioEnrichment,
  AttackTechniqueSummary,
  AttackGroupSummary,
} from '../../../core/models/scenario-builder.model';

@Component({
  selector: 'app-builder-enrichment',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatCardModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatIconModule,
    MatChipsModule,
    MatTooltipModule,
    MatTabsModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './builder-enrichment.component.html',
  styleUrls: ['./builder-enrichment.component.scss'],
})
export class BuilderEnrichmentComponent implements OnInit {
  @Input({ required: true }) scenarioId!: number;

  private readonly builderService = inject(ScenarioBuilderService);
  private readonly attackService = inject(AttackService);
  private readonly fb = inject(FormBuilder);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly entities = signal<ScenarioEntity[]>([]);
  protected readonly enrichments = signal<ScenarioEnrichment[]>([]);
  protected readonly techniques = signal<AttackTechniqueSummary[]>([]);
  protected readonly groups = signal<AttackGroupSummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly searching = signal(false);

  protected techniqueSearchForm!: FormGroup;
  protected groupSearchForm!: FormGroup;
  protected selectedEntityId = signal<string>('');

  ngOnInit(): void {
    this.initForms();
    this.loadData();
  }

  private initForms(): void {
    this.techniqueSearchForm = this.fb.group({
      query: [''],
    });

    this.groupSearchForm = this.fb.group({
      query: [''],
    });
  }

  private loadData(): void {
    this.loading.set(true);
    Promise.all([this.loadEntities(), this.loadEnrichments()]).then(() => {
      this.loading.set(false);
    });
  }

  private loadEntities(): Promise<void> {
    return new Promise((resolve) => {
      this.builderService.getEntities(this.scenarioId).subscribe({
        next: (entities) => {
          this.entities.set(entities);
          resolve();
        },
        error: (error) => {
          console.error('Error loading entities', error);
          this.snackBar.open('Failed to load entities', 'Close', { duration: 3000 });
          resolve();
        },
      });
    });
  }

  private loadEnrichments(): Promise<void> {
    return new Promise((resolve) => {
      this.builderService.getEnrichments(this.scenarioId).subscribe({
        next: (enrichments) => {
          this.enrichments.set(enrichments);
          resolve();
        },
        error: (error) => {
          console.error('Error loading enrichments', error);
          this.snackBar.open('Failed to load enrichments', 'Close', { duration: 3000 });
          resolve();
        },
      });
    });
  }

  protected searchTechniques(): void {
    const query = this.techniqueSearchForm.value.query;
    this.searching.set(true);

    this.attackService.searchTechniques(query).subscribe({
      next: (results) => {
        this.techniques.set(results);
        this.searching.set(false);
      },
      error: (error) => {
        console.error('Error searching techniques', error);
        this.snackBar.open('Failed to search techniques', 'Close', { duration: 3000 });
        this.searching.set(false);
      },
    });
  }

  protected searchGroups(): void {
    const query = this.groupSearchForm.value.query;
    this.searching.set(true);

    this.attackService.searchGroups(query).subscribe({
      next: (results) => {
        this.groups.set(results);
        this.searching.set(false);
      },
      error: (error) => {
        console.error('Error searching groups', error);
        this.snackBar.open('Failed to search groups', 'Close', { duration: 3000 });
        this.searching.set(false);
      },
    });
  }

  protected applyTechnique(techniqueId: string): void {
    const entityId = this.selectedEntityId();
    if (!entityId) {
      this.snackBar.open('Please select an entity first', 'Close', { duration: 3000 });
      return;
    }

    this.builderService.applyTechnique(this.scenarioId, { entityId, techniqueId }).subscribe({
      next: () => {
        this.snackBar.open('Technique applied', 'Close', { duration: 2000 });
        this.loadEnrichments();
      },
      error: (error) => {
        console.error('Error applying technique', error);
        this.snackBar.open('Failed to apply technique', 'Close', { duration: 3000 });
      },
    });
  }

  protected applyGroup(groupId: string): void {
    const entityId = this.selectedEntityId();
    if (!entityId) {
      this.snackBar.open('Please select an entity first', 'Close', { duration: 3000 });
      return;
    }

    this.builderService.applyGroup(this.scenarioId, { entityId, groupId }).subscribe({
      next: () => {
        this.snackBar.open('Group applied', 'Close', { duration: 2000 });
        this.loadEnrichments();
      },
      error: (error) => {
        console.error('Error applying group', error);
        this.snackBar.open('Failed to apply group', 'Close', { duration: 3000 });
      },
    });
  }

  protected deleteEnrichment(enrichmentId: number): void {
    if (!confirm('Are you sure you want to delete this enrichment?')) return;

    this.builderService.deleteEnrichment(this.scenarioId, enrichmentId).subscribe({
      next: () => {
        this.snackBar.open('Enrichment deleted', 'Close', { duration: 2000 });
        this.loadEnrichments();
      },
      error: (error) => {
        console.error('Error deleting enrichment', error);
        this.snackBar.open('Failed to delete enrichment', 'Close', { duration: 3000 });
      },
    });
  }

  protected getEntityName(entityId: string | null): string {
    if (!entityId) return 'Global';
    const entity = this.entities().find((e) => e.id === entityId);
    return entity?.name || 'Unknown';
  }

  protected getThreatActorEntities(): ScenarioEntity[] {
    return this.entities().filter(
      (e) =>
        e.entityType === 'ThreatActor' ||
        e.entityType.includes('ThreatActor') ||
        e.entityType === 'Organization' ||
        e.entityType === 'Campaign'
    );
  }

  protected getNonThreatActorEntities(): ScenarioEntity[] {
    return this.entities().filter(
      (e) =>
        e.entityType !== 'ThreatActor' &&
        !e.entityType.includes('ThreatActor') &&
        e.entityType !== 'Organization' &&
        e.entityType !== 'Campaign'
    );
  }
}
