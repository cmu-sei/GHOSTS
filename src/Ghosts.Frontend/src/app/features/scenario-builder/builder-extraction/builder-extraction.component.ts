import { Component, Input, OnInit, OnDestroy, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { ChangeDetectionStrategy } from '@angular/core';
import { ScenarioBuilderService } from '../../../core/services/scenario-builder.service';
import { ConfigService } from '../../../core/services/config.service';
import { ScenarioSource, ExtractionResult } from '../../../core/models/scenario-builder.model';
import * as signalR from '@microsoft/signalr';

interface ExtractionProgress {
  scenarioId: number;
  status: string;
  chunksProcessed: number;
  totalChunks: number;
  entitiesCreated: number;
  edgesCreated: number;
  timestamp: string;
}

@Component({
  selector: 'app-builder-extraction',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatProgressBarModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './builder-extraction.component.html',
  styleUrls: ['./builder-extraction.component.scss'],
})
export class BuilderExtractionComponent implements OnInit, OnDestroy {
  @Input({ required: true }) scenarioId!: number;

  private readonly builderService = inject(ScenarioBuilderService);
  private readonly configService = inject(ConfigService);
  private readonly snackBar = inject(MatSnackBar);
  private hubConnection?: signalR.HubConnection;

  protected readonly sources = signal<ScenarioSource[]>([]);
  protected readonly loading = signal(true);
  protected readonly extracting = signal(false);
  protected readonly extractionResult = signal<ExtractionResult | null>(null);
  protected readonly extractionProgress = signal<ExtractionProgress | null>(null);

  ngOnInit(): void {
    this.loadSources();
    this.startSignalRConnection();
  }

  ngOnDestroy(): void {
    this.stopSignalRConnection();
  }

  private startSignalRConnection(): void {
    const apiUrl = this.configService.apiUrl.replace('/api', '');
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(`${apiUrl}/api/hubs/scenarioBuilder?scenarioId=${this.scenarioId}`)
      .withAutomaticReconnect()
      .build();

    this.hubConnection.on('extractionProgress', (progress: ExtractionProgress) => {
      console.log('Extraction progress:', progress);
      this.extractionProgress.set(progress);

      if (progress.status === 'completed') {
        this.extracting.set(false);
        this.loadSources(); // Reload to get updated chunk statuses
      }
    });

    this.hubConnection.start()
      .then(() => console.log('SignalR connected for extraction progress'))
      .catch(err => console.error('SignalR connection error:', err));
  }

  private stopSignalRConnection(): void {
    if (this.hubConnection) {
      this.hubConnection.stop()
        .then(() => console.log('SignalR disconnected'))
        .catch(err => console.error('SignalR disconnect error:', err));
    }
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

  // Public method to reload sources when step becomes active
  public refresh(): void {
    this.loadSources();
  }

  protected extractAll(): void {
    this.extracting.set(true);
    this.extractionResult.set(null);
    this.extractionProgress.set(null);

    this.builderService.extractAll(this.scenarioId).subscribe({
      next: (result) => {
        this.extractionResult.set(result);
        this.extracting.set(false);
        this.snackBar.open('Extraction completed', 'Close', { duration: 3000 });
      },
      error: (error) => {
        console.error('Error during extraction', error);
        this.snackBar.open('Extraction failed', 'Close', { duration: 3000 });
        this.extracting.set(false);
        this.extractionProgress.set(null);
      },
    });
  }

  protected getProgressValue(): number {
    const sources = this.sources();
    if (sources.length === 0) return 0;

    const processedCount = sources.filter(
      (s) => s.status === 'Ready' || s.status === 'Extracted'
    ).length;
    return (processedCount / sources.length) * 100;
  }

  protected getTotalChunks(): number {
    return this.sources().reduce((acc, s) => acc + (s.chunkCount || 0), 0);
  }

  protected getStatusColor(status: string): string {
    switch (status?.toLowerCase()) {
      case 'ready':
      case 'extracted':
        return 'primary';
      case 'processing':
        return 'accent';
      case 'error':
        return 'warn';
      default:
        return '';
    }
  }
}
