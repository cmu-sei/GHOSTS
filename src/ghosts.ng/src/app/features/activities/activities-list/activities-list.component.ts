import { Component, inject, OnInit, OnDestroy, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ChangeDetectionStrategy } from '@angular/core';
import { interval, Subscription } from 'rxjs';
import { switchMap, startWith } from 'rxjs/operators';
import { NpcRecord, AiActionRequest } from '../../../core/models';
import { ActivityService } from '../../../core/services';
import { environment } from '../../../../environments/environment';

@Component({
  selector: 'app-activities-list',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterLink,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    MatTooltipModule,
    DatePipe
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './activities-list.component.html',
  styleUrls: ['./activities-list.component.scss']
})
export class ActivitiesListComponent implements OnInit, OnDestroy {
  private readonly activityService = inject(ActivityService);
  private readonly fb = inject(FormBuilder);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly npcs = signal<NpcRecord[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly displayedColumns = ['avatar', 'name', 'activity', 'time'];

  protected commandForm!: FormGroup;
  private pollSubscription?: Subscription;

  protected readonly handlers = [
    'AWS', 'Azure', 'Blog', 'BlogDrupal', 'BrowserChrome', 'BrowserFirefox',
    'BrowserCrawl', 'Clicks', 'CMD', 'Excel', 'ExecuteFile', 'FTP', 'SFTP',
    'SSH', 'Notepad', 'Outlook', 'Pidgin', 'PowerPoint', 'PowerShell', 'Print',
    'RDP', 'Reboot', 'WMI', 'Sharepoint', 'Social', 'Word', 'Watcher'
  ];

  ngOnInit(): void {
    this.initForm();
    this.startPolling();
  }

  ngOnDestroy(): void {
    this.pollSubscription?.unsubscribe();
  }

  private initForm(): void {
    this.commandForm = this.fb.group({
      handler: ['BrowserChrome', Validators.required],
      action: ['', Validators.required],
      who: ['random', Validators.required],
      scale: [1, [Validators.required, Validators.min(1)]],
      reasoning: [''],
      sentiment: ['']
    });
  }

  private startPolling(): void {
    // Poll every 5 seconds for updates
    this.pollSubscription = interval(5000)
      .pipe(
        startWith(0),
        switchMap(() => this.activityService.getAllNpcs())
      )
      .subscribe({
        next: (npcs) => {
          this.npcs.set(npcs);
          this.loading.set(false);
          this.error.set(null);
        },
        error: (err) => {
          this.error.set(err.message || 'Failed to load NPC activities');
          this.loading.set(false);
        }
      });
  }

  protected loadNpcs(): void {
    this.loading.set(true);
    this.error.set(null);

    this.activityService.getAllNpcs().subscribe({
      next: (npcs) => {
        this.npcs.set(npcs);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err.message || 'Failed to load NPCs');
        this.loading.set(false);
      }
    });
  }

  protected sendCommand(): void {
    if (this.commandForm.invalid) {
      this.snackBar.open('Please fill in all required fields', 'Close', { duration: 3000 });
      return;
    }

    const request: AiActionRequest = {
      handler: this.commandForm.value.handler,
      action: this.commandForm.value.action,
      who: this.commandForm.value.who,
      scale: this.commandForm.value.scale,
      reasoning: this.commandForm.value.reasoning,
      sentiment: this.commandForm.value.sentiment
    };

    this.activityService.sendNpcCommand(request).subscribe({
      next: () => {
        this.snackBar.open('Command sent successfully', 'Close', { duration: 3000 });
        this.commandForm.patchValue({ action: '', reasoning: '', sentiment: '' });
        this.loadNpcs();
      },
      error: (err) => {
        this.snackBar.open(
          err.message || 'Failed to send command',
          'Close',
          { duration: 5000 }
        );
      }
    });
  }

  protected getActivityIcon(activityType?: string): string {
    if (!activityType) return 'fa-question-circle';

    switch (activityType.toLowerCase()) {
      case 'social':
      case 'socialmediapost':
        return 'fa-share-alt';
      case 'belief':
        return 'fa-bolt';
      case 'chat':
        return 'fa-comment';
      case 'knowledge':
      case 'learning':
        return 'fa-graduation-cap';
      case 'relationship':
        return 'fa-users';
      case 'activity':
      case 'nextaction':
        return 'fa-user-plus';
      default:
        return 'fa-info-circle';
    }
  }

  protected getActivityIconClass(activityType?: string): string {
    if (!activityType) return 'default';

    switch (activityType.toLowerCase()) {
      case 'social':
      case 'socialmediapost':
        return 'social';
      case 'belief':
        return 'belief';
      case 'chat':
        return 'chat';
      case 'knowledge':
      case 'learning':
        return 'knowledge';
      case 'relationship':
        return 'relationship';
      case 'activity':
      case 'nextaction':
        return 'activity';
      default:
        return 'default';
    }
  }

  protected getNpcName(npc: NpcRecord): string {
    // NpcProfile has a nested Name object with first/last fields
    if (npc.npcProfile?.name) {
      const name = npc.npcProfile.name;
      return `${name.first || ''} ${name.last || ''}`.trim() || 'Unknown';
    }
    return npc.npcSocialGraph?.name || npc.npcProfile?.username || 'Unknown';
  }

  protected getNpcAvatar(npc: NpcRecord): string | null {
    // Use API endpoint for NPC photo
    return npc.id ? `${environment.apiUrl}/npcs/${npc.id}/photo` : null;
  }
}
