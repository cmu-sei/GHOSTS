import { Component, Input, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { ChangeDetectionStrategy } from '@angular/core';
import { ScenarioBuilderService } from '../../../core/services/scenario-builder.service';
import { ScenarioEntity, ENTITY_TYPES, ENTITY_COLORS } from '../../../core/models/scenario-builder.model';

@Component({
  selector: 'app-builder-entities',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatIconModule,
    MatChipsModule,
    MatCheckboxModule,
    MatTooltipModule,
    MatDialogModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './builder-entities.component.html',
  styleUrls: ['./builder-entities.component.scss'],
})
export class BuilderEntitiesComponent implements OnInit {
  @Input({ required: true }) scenarioId!: number;

  private readonly builderService = inject(ScenarioBuilderService);
  private readonly fb = inject(FormBuilder);
  private readonly snackBar = inject(MatSnackBar);
  private readonly dialog = inject(MatDialog);

  protected readonly entities = signal<ScenarioEntity[]>([]);
  protected readonly filteredEntities = signal<ScenarioEntity[]>([]);
  protected readonly loading = signal(true);
  protected readonly displayedColumns = [
    'name',
    'type',
    'description',
    'confidence',
    'origin',
    'reviewed',
    'actions',
  ];
  protected readonly entityTypes = ENTITY_TYPES;
  protected readonly entityColors = ENTITY_COLORS;
  protected readonly selectedType = signal<string>('');
  protected readonly editingEntity = signal<ScenarioEntity | null>(null);
  protected readonly selectedEntities = signal<Set<string>>(new Set());

  protected entityForm!: FormGroup;
  protected showAddForm = signal(false);

  ngOnInit(): void {
    this.initForm();
    this.loadEntities();
  }

  private initForm(): void {
    this.entityForm = this.fb.group({
      name: ['', Validators.required],
      entityType: ['Person', Validators.required],
      description: [''],
      properties: [''],
      confidence: [0.8, [Validators.required, Validators.min(0), Validators.max(1)]],
    });
  }

  private loadEntities(): void {
    this.loading.set(true);
    this.builderService.getEntities(this.scenarioId).subscribe({
      next: (entities) => {
        this.entities.set(entities);
        this.applyFilter();
        this.loading.set(false);
      },
      error: (error) => {
        console.error('Error loading entities', error);
        this.snackBar.open('Failed to load entities', 'Close', { duration: 3000 });
        this.loading.set(false);
      },
    });
  }

  private applyFilter(): void {
    const typeFilter = this.selectedType();
    if (!typeFilter) {
      this.filteredEntities.set(this.entities());
    } else {
      this.filteredEntities.set(
        this.entities().filter((e) => e.entityType === typeFilter)
      );
    }
  }

  protected onTypeFilterChange(type: string): void {
    this.selectedType.set(type);
    this.applyFilter();
  }

  protected toggleAddForm(): void {
    this.showAddForm.update((show) => !show);
    if (!this.showAddForm()) {
      this.entityForm.reset({ entityType: 'Person', confidence: 0.8 });
    }
  }

  protected createEntity(): void {
    if (this.entityForm.invalid) return;

    this.builderService.createEntity(this.scenarioId, this.entityForm.value).subscribe({
      next: () => {
        this.snackBar.open('Entity created', 'Close', { duration: 2000 });
        this.toggleAddForm();
        this.loadEntities();
      },
      error: (error) => {
        console.error('Error creating entity', error);
        this.snackBar.open('Failed to create entity', 'Close', { duration: 3000 });
      },
    });
  }

  protected editEntity(entity: ScenarioEntity): void {
    this.editingEntity.set(entity);
    this.entityForm.patchValue({
      name: entity.name,
      entityType: entity.entityType,
      description: entity.description,
      properties: entity.properties,
      confidence: entity.confidence,
    });
    this.showAddForm.set(true);
  }

  protected updateEntity(): void {
    const entity = this.editingEntity();
    if (!entity || this.entityForm.invalid) return;

    const updateDto = {
      ...this.entityForm.value,
      isReviewed: entity.isReviewed,
    };

    this.builderService.updateEntity(this.scenarioId, entity.id, updateDto).subscribe({
      next: () => {
        this.snackBar.open('Entity updated', 'Close', { duration: 2000 });
        this.editingEntity.set(null);
        this.toggleAddForm();
        this.loadEntities();
      },
      error: (error) => {
        console.error('Error updating entity', error);
        this.snackBar.open('Failed to update entity', 'Close', { duration: 3000 });
      },
    });
  }

  protected deleteEntity(entityId: string): void {
    if (!confirm('Are you sure you want to delete this entity?')) return;

    this.builderService.deleteEntity(this.scenarioId, entityId).subscribe({
      next: () => {
        this.snackBar.open('Entity deleted', 'Close', { duration: 2000 });
        this.loadEntities();
      },
      error: (error) => {
        console.error('Error deleting entity', error);
        this.snackBar.open('Failed to delete entity', 'Close', { duration: 3000 });
      },
    });
  }

  protected toggleEntitySelection(entityId: string): void {
    this.selectedEntities.update((selected) => {
      const newSet = new Set(selected);
      if (newSet.has(entityId)) {
        newSet.delete(entityId);
      } else {
        newSet.add(entityId);
      }
      return newSet;
    });
  }

  protected mergeSelectedEntities(): void {
    const selected = Array.from(this.selectedEntities());
    if (selected.length !== 2) {
      this.snackBar.open('Please select exactly 2 entities to merge', 'Close', { duration: 3000 });
      return;
    }

    const [keepId, mergeId] = selected;
    if (!confirm('Merge these entities? The second entity will be deleted.')) return;

    this.builderService.mergeEntities(this.scenarioId, keepId, mergeId).subscribe({
      next: () => {
        this.snackBar.open('Entities merged', 'Close', { duration: 2000 });
        this.selectedEntities.set(new Set());
        this.loadEntities();
      },
      error: (error) => {
        console.error('Error merging entities', error);
        this.snackBar.open('Failed to merge entities', 'Close', { duration: 3000 });
      },
    });
  }

  protected getEntityColor(type: string): string {
    return this.entityColors[type] || '#9E9E9E';
  }

  protected isEditing(): boolean {
    return this.editingEntity() !== null;
  }

  // Public method to refresh entities when step becomes active
  public refresh(): void {
    this.loadEntities();
  }
}
