import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { ChangeDetectionStrategy } from '@angular/core';
import { JobInfo, AnimationJobTypes, AnimationStartRequest, AnimationStopRequest } from '../../../core/models';
import { AnimationService, ConfigService } from '../../../core/services';

@Component({
  selector: 'app-animations-list',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatSelectModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    DatePipe
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './animations-list.component.html',
  styleUrls: ['./animations-list.component.scss']
})
export class AnimationsListComponent implements OnInit {
  private readonly animationService = inject(AnimationService);
  private readonly fb = inject(FormBuilder);
  private readonly snackBar = inject(MatSnackBar);
  private readonly configService = inject(ConfigService);

  protected readonly runningJobs = signal<JobInfo[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly displayedColumns = ['name', 'startTime', 'actions'];
  protected readonly jobTypes = Object.values(AnimationJobTypes);

  protected startForm!: FormGroup;

  private readonly defaultConfigs = this.createDefaultConfigs();

  ngOnInit(): void {
    this.initForm();
    this.loadRunningJobs();
  }

  private initForm(): void {
    this.startForm = this.fb.group({
      jobType: [AnimationJobTypes.SOCIALGRAPH, Validators.required],
      configuration: [
        JSON.stringify(this.defaultConfigs[AnimationJobTypes.SOCIALGRAPH], null, 2),
        Validators.required
      ]
    });

    // Update configuration when job type changes
    this.startForm.get('jobType')?.valueChanges.subscribe((jobType: AnimationJobTypes) => {
      this.startForm.patchValue({
        configuration: JSON.stringify(this.defaultConfigs[jobType], null, 2)
      });
    });
  }

  protected loadRunningJobs(): void {
    this.loading.set(true);
    this.error.set(null);

    this.animationService.getRunningJobs().subscribe({
      next: (jobs) => {
        console.log('Running jobs received from API:', jobs);
        this.runningJobs.set(jobs);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err.message || 'Failed to load running jobs');
        this.loading.set(false);
      }
    });
  }

  protected startAnimation(): void {
    if (this.startForm.invalid) {
      this.snackBar.open('Please fill in all required fields', 'Close', { duration: 3000 });
      return;
    }

    // Validate JSON
    let configJson: string;
    try {
      const parsed = JSON.parse(this.startForm.value.configuration);
      configJson = JSON.stringify(parsed);
    } catch (e) {
      this.snackBar.open('Invalid JSON configuration', 'Close', { duration: 3000 });
      return;
    }

    const request: AnimationStartRequest = {
      jobId: this.startForm.value.jobType,
      jobConfiguration: configJson
    };

    this.animationService.startAnimation(request).subscribe({
      next: () => {
        this.snackBar.open('Animation started successfully', 'Close', { duration: 3000 });
        this.loadRunningJobs();
      },
      error: (err) => {
        this.snackBar.open(
          err.message || 'Failed to start animation',
          'Close',
          { duration: 5000 }
        );
      }
    });
  }

  protected stopAnimation(jobId: string, job?: any): void {
    console.log('Stopping animation with jobId:', jobId);
    console.log('Full job object:', job);
    console.log('Job properties:', {
      id: job?.id,
      name: job?.name,
      startTime: job?.startTime,
      allKeys: job ? Object.keys(job) : []
    });

    if (!jobId) {
      console.error('JobId is null or undefined!');
      this.snackBar.open('Invalid job ID - check console for details', 'Close', { duration: 3000 });
      return;
    }

    const request: AnimationStopRequest = { jobId };
    console.log('Stop request:', request);

    this.animationService.stopAnimation(request).subscribe({
      next: () => {
        this.snackBar.open('Animation stopped successfully', 'Close', { duration: 3000 });
        this.loadRunningJobs();
      },
      error: (err) => {
        console.error('Stop animation error:', err);
        this.snackBar.open(
          err.message || 'Failed to stop animation',
          'Close',
          { duration: 5000 }
        );
      }
    });
  }

  protected formatJobType(type: string): string {
    return type.replace(/([A-Z])/g, ' $1').trim();
  }

  private createDefaultConfigs(): Record<AnimationJobTypes, any> {
    const apiUrl = this.configService.apiUrl.replace(/\/$/, '');
    const timelinesUrl = `${apiUrl}/timelines`;
    const chatUrl = `${apiUrl}/chat`;
    const windowOrigin = this.getWindowOrigin();
    const ollamaHost = `${windowOrigin}:11434`;

    return {
      [AnimationJobTypes.SOCIALGRAPH]: {
        isEnabled: true,
        isMultiThreaded: true,
        isInteracting: true,
        turnLength: 5000,
        maximumSteps: 100,
        chanceOfKnowledgeTransfer: 0.75,
        decay: {
          isEnabled: true,
          rate: 0.05
        }
      },
      [AnimationJobTypes.SOCIALSHARING]: {
        isEnabled: true,
        isMultiThreaded: true,
        isInteracting: true,
        isSendingTimelinesToGhostsApi: true,
        isSendingTimelinesDirectToSocializer: false,
        postUrl: timelinesUrl,
        turnLength: 5000,
        maximumSteps: 100,
        contentEngine: {
          source: 'ollama',
          model: 'mistral:7b',
          host: ollamaHost,
          temperature: 0.7
        }
      },
      [AnimationJobTypes.SOCIALBELIEF]: {
        isEnabled: true,
        isMultiThreaded: true,
        isInteracting: true,
        turnLength: 5000,
        maximumSteps: 100
      },
      [AnimationJobTypes.CHAT]: {
        isEnabled: true,
        isMultiThreaded: true,
        isInteracting: true,
        isSendingTimelinesToGhostsApi: true,
        turnLength: 5000,
        maximumSteps: 100,
        percentReplyVsNew: 50,
        postProbabilities: {
          text: 70,
          image: 20,
          link: 10
        },
        postUrl: chatUrl,
        contentEngine: {
          source: 'OpenAi',
          model: 'gpt-4',
          temperature: 0.7
        }
      },
      [AnimationJobTypes.FULLAUTONOMY]: {
        isEnabled: true,
        isMultiThreaded: true,
        isInteracting: true,
        isSendingTimelinesToGhostsApi: true,
        turnLength: 5000,
        maximumSteps: 100,
        contentEngine: {
          source: 'OpenAi',
          model: 'gpt-4',
          temperature: 0.7
        }
      }
    };
  }

  private getWindowOrigin(): string {
    if (typeof window === 'undefined') {
      throw new Error('Window is not defined. Cannot determine origin.');
    }

    return `${window.location.protocol}//${window.location.hostname}`;
  }
}
