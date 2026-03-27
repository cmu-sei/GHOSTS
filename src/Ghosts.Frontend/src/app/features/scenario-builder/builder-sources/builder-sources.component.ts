import { Component, Input, OnInit, inject, signal } from '@angular/core';
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
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { ChangeDetectionStrategy } from '@angular/core';
import { ScenarioBuilderService } from '../../../core/services/scenario-builder.service';
import { ScenarioSource } from '../../../core/models/scenario-builder.model';

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
    MatSnackBarModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './builder-sources.component.html',
  styleUrls: ['./builder-sources.component.scss'],
})
export class BuilderSourcesComponent implements OnInit {
  @Input({ required: true }) scenarioId!: number;

  private readonly builderService = inject(ScenarioBuilderService);
  private readonly fb = inject(FormBuilder);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly sources = signal<ScenarioSource[]>([]);
  protected readonly loading = signal(true);
  protected readonly displayedColumns = ['name', 'type', 'status', 'chunks', 'actions'];
  protected readonly dragOver = signal(false);

  protected textForm!: FormGroup;
  protected urlForm!: FormGroup;
  protected selectedFile = signal<File | null>(null);

  ngOnInit(): void {
    this.initForms();
    this.loadSources();
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

    this.builderService.addText(this.scenarioId, this.textForm.value).subscribe({
      next: () => {
        this.snackBar.open('Text source added', 'Close', { duration: 2000 });
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

    this.builderService.addUrl(this.scenarioId, this.urlForm.value).subscribe({
      next: () => {
        this.snackBar.open('URL source added', 'Close', { duration: 2000 });
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

    this.builderService.uploadFile(this.scenarioId, file).subscribe({
      next: () => {
        this.snackBar.open('File uploaded successfully', 'Close', { duration: 2000 });
        this.selectedFile.set(null);
        this.loadSources();
      },
      error: (error) => {
        console.error('Error uploading file', error);
        this.snackBar.open('Failed to upload file', 'Close', { duration: 3000 });
      },
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
