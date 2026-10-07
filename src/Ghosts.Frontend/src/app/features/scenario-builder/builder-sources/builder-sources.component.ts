import { Component, Input, OnDestroy, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { Subscription } from 'rxjs';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { ChangeDetectionStrategy } from '@angular/core';
import { ScenarioBuilderService } from '../../../core/services/scenario-builder.service';
import { ScenarioService } from '../../../core/services/scenario.service';
import { ScenarioHubService } from '../../../core/services/scenario-hub.service';
import { BuilderModel, ExtractionProgress, ExtractionResult, ScenarioSource } from '../../../core/models/scenario-builder.model';
import { BuilderEntitiesComponent } from '../builder-entities/builder-entities.component';
import { BuilderGraphComponent } from '../builder-graph/builder-graph.component';

@Component({
  selector: 'app-builder-sources',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatIconModule,
    MatChipsModule,
    MatTooltipModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    MatSnackBarModule,
    BuilderEntitiesComponent,
    BuilderGraphComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './builder-sources.component.html',
  styleUrls: ['./builder-sources.component.scss'],
})
export class BuilderSourcesComponent implements OnInit, OnDestroy {
  @Input({ required: true }) scenarioId!: number;

  private readonly builderService = inject(ScenarioBuilderService);
  private readonly scenarioService = inject(ScenarioService);
  private readonly hub = inject(ScenarioHubService);
  private readonly fb = inject(FormBuilder);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly sources = signal<ScenarioSource[]>([]);
  protected readonly loading = signal(true);
  /** The scenario's model and effort level for extraction and the conversation, and the configured choices. */
  protected readonly builderModel = signal<BuilderModel | null>(null);
  protected readonly efforts = computed(() => this.builderModel()?.models.find((m) => m.id === this.builderModel()?.model)?.efforts ?? []);
  protected readonly displayedColumns = ['name', 'type', 'preview', 'status', 'chunks', 'extracted', 'actions'];
  protected readonly dragOver = signal(false);

  // Extraction (J9) runs on the server when a source is added; the step follows it over the hub. The button
  // only reruns it for chunks a run left pending, such as when the model was unavailable.
  protected readonly pendingChunks = computed(() => this.sources().reduce((n, s) => n + (s.pendingChunkCount || 0), 0));
  protected readonly extracting = signal(false);
  protected readonly extractionProgress = signal<ExtractionProgress | null>(null);
  protected readonly extractionResult = signal<ExtractionResult | null>(null);
  private readonly entities = viewChild(BuilderEntitiesComponent);
  private readonly graph = viewChild(BuilderGraphComponent);
  private progressSub?: Subscription;

  protected textForm!: FormGroup;
  protected urlForm!: FormGroup;
  protected selectedFile = signal<File | null>(null);

  ngOnInit(): void {
    this.initForms();
    this.loadSources();
    this.builderService.getModel(this.scenarioId).subscribe((choice) => this.builderModel.set(choice));
    this.hub.connect(this.scenarioId);
    this.progressSub = this.hub.extractionProgress$.subscribe((progress) => {
      if (progress.scenarioId !== this.scenarioId) return;
      this.extractionProgress.set(progress);
      if (progress.status === 'completed') {
        this.extracting.set(false);
        this.loadSources();
        this.entities()?.refresh();
        this.graph()?.refresh();
      } else {
        this.extracting.set(true);
      }
    });
  }

  ngOnDestroy(): void {
    this.progressSub?.unsubscribe();
  }

  /** Saves the model and effort; an effort the new model does not take is dropped. */
  protected pickModel(model: string, effort: string | null): void {
    const efforts = this.builderModel()?.models.find((m) => m.id === model)?.efforts ?? [];
    const kept = effort && efforts.includes(effort) ? effort : null;
    this.builderService.setModel(this.scenarioId, model, kept).subscribe({
      next: (choice) => this.builderModel.set(choice),
      error: () => this.snackBar.open('Could not change the model', 'Close', { duration: 3000 }),
    });
  }

  protected extract(): void {
    this.extracting.set(true);
    this.extractionResult.set(null);
    this.extractionProgress.set(null);
    this.builderService.extractAll(this.scenarioId).subscribe({
      next: (result) => {
        this.extractionResult.set(result);
        this.extracting.set(false);
        this.loadSources();
        this.entities()?.refresh();
        this.graph()?.refresh();
      },
      error: (error) => {
        console.error('Error during extraction', error);
        this.snackBar.open('Extraction failed', 'Close', { duration: 3000 });
        this.extracting.set(false);
        this.extractionProgress.set(null);
      },
    });
  }

  private initForms(): void {
    this.textForm = this.fb.group({
      name: ['', Validators.required],
      content: ['', Validators.required],
    });

    this.urlForm = this.fb.group({
      name: ['', Validators.required],
      url: ['', [Validators.required, Validators.pattern(/^https?:\/\/.+/)]],
    });
  }

  private loadSources(): void {
    this.loading.set(true);
    this.builderService.getSources(this.scenarioId).subscribe({
      next: (sources) => {
        this.sources.set(sources);
        this.loading.set(false);
      },
      error: (error) => {
        console.error('Error loading sources', error);
        this.snackBar.open('Failed to load sources', 'Close', { duration: 3000 });
        this.loading.set(false);
      },
    });
  }

  protected addTextSource(): void {
    if (this.textForm.invalid) return;
    const isFirst = this.sources().length === 0;
    const sourceName = this.textForm.value.name as string;

    this.builderService.addText(this.scenarioId, this.textForm.value).subscribe({
      next: () => {
        this.snackBar.open('Text source added', 'Close', { duration: 2000 });
        if (isFirst) this.renameScenarioFromSource(sourceName);
        this.textForm.reset();
        this.loadSources();
      },
      error: (error) => {
        console.error('Error adding text source', error);
        this.snackBar.open('Failed to add text source', 'Close', { duration: 3000 });
      },
    });
  }

  protected addUrlSource(): void {
    if (this.urlForm.invalid) return;
    const isFirst = this.sources().length === 0;
    const sourceName = this.urlForm.value.name as string;

    this.builderService.addUrl(this.scenarioId, this.urlForm.value).subscribe({
      next: () => {
        this.snackBar.open('URL source added', 'Close', { duration: 2000 });
        if (isFirst) this.renameScenarioFromSource(sourceName);
        this.urlForm.reset();
        this.loadSources();
      },
      error: (error) => {
        console.error('Error adding URL source', error);
        this.snackBar.open('Failed to add URL source', 'Close', { duration: 3000 });
      },
    });
  }

  protected onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      this.selectedFile.set(input.files[0]);
      this.uploadFile();
    }
  }

  protected onDragOver(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.dragOver.set(true);
  }

  protected onDragLeave(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.dragOver.set(false);
  }

  protected onDrop(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.dragOver.set(false);

    if (event.dataTransfer?.files && event.dataTransfer.files.length > 0) {
      this.selectedFile.set(event.dataTransfer.files[0]);
      this.uploadFile();
    }
  }

  private uploadFile(): void {
    const file = this.selectedFile();
    if (!file) return;
    const isFirst = this.sources().length === 0;
    // Derive name from filename: strip extension, replace separators
    const baseName = file.name.replace(/\.[^.]+$/, '');

    this.builderService.uploadFile(this.scenarioId, file).subscribe({
      next: () => {
        this.snackBar.open('File uploaded successfully', 'Close', { duration: 2000 });
        if (isFirst) this.renameScenarioFromSource(baseName);
        this.selectedFile.set(null);
        this.loadSources();
      },
      error: (error) => {
        console.error('Error uploading file', error);
        this.snackBar.open('Failed to upload file', 'Close', { duration: 3000 });
      },
    });
  }

  /** Rename the scenario after the first source is added, if it still has the placeholder name. */
  private renameScenarioFromSource(rawName: string): void {
    const derived = rawName
      .replace(/[-_]+/g, ' ')
      .replace(/\b\w/g, c => c.toUpperCase())
      .trim();
    if (!derived) return;

    this.scenarioService.getScenario(this.scenarioId).subscribe({
      next: (scenario) => {
        // Only overwrite if still using the auto-generated placeholder
        if (!scenario.name?.match(/^Scenario [A-Z]/)) return;
        this.scenarioService.updateScenario(this.scenarioId, {
          ...scenario,
          name: derived,
          scenarioParameters: scenario.scenarioParameters || { nations: [], threatActors: [], injects: [], userPools: [], objectives: '', politicalContext: '', rulesOfEngagement: '', victoryConditions: '' },
          technicalEnvironment: scenario.technicalEnvironment || { networkTopology: '', services: '', assets: '', defenses: [], vulnerabilities: [] },
          timeline: scenario.timeline || { exerciseDuration: 0, events: [] }
        })
          .subscribe({ error: () => {} }); // best-effort, silent on failure
      },
      error: () => {}
    });
  }

  protected deleteSource(sourceId: number): void {
    if (!confirm('Are you sure you want to delete this source?')) return;

    this.builderService.deleteSource(this.scenarioId, sourceId).subscribe({
      next: () => {
        this.snackBar.open('Source deleted', 'Close', { duration: 2000 });
        this.loadSources();
      },
      error: (error) => {
        console.error('Error deleting source', error);
        this.snackBar.open('Failed to delete source', 'Close', { duration: 3000 });
      },
    });
  }

  protected getStatusColor(status: string): string {
    switch (status?.toLowerCase()) {
      case 'ready':
        return 'primary';
      case 'processing':
        return 'accent';
      case 'error':
        return 'warn';
      default:
        return '';
    }
  }

  protected formatDate(date: string): string {
    return new Date(date).toLocaleDateString('en-US', {
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  }
}
