import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDividerModule } from '@angular/material/divider';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MAT_CHECKBOX_DEFAULT_OPTIONS } from '@angular/material/checkbox';
import { TimelineLocalService } from '../../../core/services';
import {
  CreateLocalTimelineRequest,
  LocalTimeline,
  TimelineHandler,
  TimelineScheduleType,
  TimelineEvent
} from '../../../core/models';
import { TimelineJsonDialogComponent } from '../timeline-json-dialog/timeline-json-dialog.component';

const HANDLER_TYPES: readonly string[] = [
  'BrowserFirefox',
  'BrowserChrome',
  'Command',
  'Notepad',
  'Outlook',
  'Word',
  'Excel',
  'PowerPoint',
  'NpcSystem',
  'Reboot',
  'Curl',
  'Clicks',
  'Watcher',
  'LightWord',
  'LightExcel',
  'LightPowerPoint',
  'PowerShell',
  'Bash',
  'Print',
  'Ssh',
  'Sftp',
  'Pidgin',
  'Rdp',
  'Wmi',
  'Outlookv2',
  'Ftp',
  'AwsCli'
];

const TIME_PATTERN = /^([01]?\d|2[0-3]):([0-5]\d):([0-5]\d)$/;

@Component({
  selector: 'app-timeline-editor',
  standalone: true,
  providers: [
    {
      provide: MAT_CHECKBOX_DEFAULT_OPTIONS,
      useValue: { clickAction: 'check-indeterminate' }
    }
  ],
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatCheckboxModule,
    MatButtonModule,
    MatIconModule,
    MatDividerModule,
    MatExpansionModule,
    MatSnackBarModule,
    MatDialogModule
  ],
  template: `
    <section class="editor-container">
      <header class="editor-header">
        <div>
          <h1>{{ editingId ? 'Edit timeline' : 'Create timeline' }}</h1>
          <p class="subtitle">
            Configure handlers and their events, or import an existing timeline definition.
          </p>
        </div>
        <div class="header-actions">
          <button mat-stroked-button color="primary" (click)="importFromJson()">
            <i class="fas fa-file-import"></i>
            Import JSON
          </button>
          <button mat-button color="accent" (click)="previewJson()">
            <i class="fas fa-code"></i>
            Preview JSON
          </button>
        </div>
      </header>

      <form class="timeline-form" [formGroup]="form" (ngSubmit)="onSubmit()">
        <mat-card>
          <mat-card-content>
            <div class="form-grid">
              <mat-form-field appearance="outline">
                <mat-label>Name</mat-label>
                <input matInput formControlName="name" placeholder="Ex: Morning operations" />
                @if (form.controls.name.invalid) {
                  <mat-error>Name is required</mat-error>
                }
              </mat-form-field>
            </div>
          </mat-card-content>
        </mat-card>

        <mat-card class="handlers-card">
          <mat-card-header>
            <mat-card-title>Timeline handlers</mat-card-title>
            <mat-card-subtitle>
              Handlers run sequentially and can contain nested events or commands.
            </mat-card-subtitle>
          </mat-card-header>
          <mat-card-content>
            <mat-accordion [multi]="true">
              @for (handler of handlerArray.controls; track handler; let i = $index) {
                <mat-expansion-panel [expanded]="expandedPanel === handler" (opened)="expandedPanel = handler">
                  <mat-expansion-panel-header>
                    <mat-panel-title>
                      {{ handler.value.handlerType || 'Handler' }} · Step {{ i + 1 }}
                    </mat-panel-title>
                    <mat-panel-description>
                      {{ handler.value.utcTimeOn }} → {{ handler.value.utcTimeOff }}
                    </mat-panel-description>
                  </mat-expansion-panel-header>

                  <div class="handler-grid" [formGroup]="handler">
                    <div class="handler-row">
                      <mat-form-field appearance="outline">
                        <mat-label>Handler type</mat-label>
                        <mat-select formControlName="handlerType">
                          @for (type of handlerTypes; track type) {
                            <mat-option [value]="type">{{ type }}</mat-option>
                          }
                        </mat-select>
                      </mat-form-field>

                      <mat-form-field appearance="outline">
                        <mat-label>Initial</mat-label>
                        <input matInput formControlName="initial" placeholder="Optional initialization command" />
                      </mat-form-field>
                    </div>

                    <div class="handler-row">
                      <mat-form-field appearance="outline">
                        <mat-label>UTC time on</mat-label>
                        <input matInput formControlName="utcTimeOn" placeholder="HH:mm:ss" />
                        <mat-error>Invalid time format</mat-error>
                      </mat-form-field>

                      <mat-form-field appearance="outline">
                        <mat-label>UTC time off</mat-label>
                        <input matInput formControlName="utcTimeOff" placeholder="HH:mm:ss" />
                        <mat-error>Invalid time format</mat-error>
                      </mat-form-field>

                      <mat-form-field appearance="outline">
                        <mat-label>Schedule type</mat-label>
                        <mat-select formControlName="scheduleType">
                          @for (schedule of scheduleTypes; track schedule) {
                            <mat-option [value]="schedule">{{ schedule }}</mat-option>
                          }
                        </mat-select>
                      </mat-form-field>

                      <mat-form-field appearance="outline">
                        <mat-label>Schedule (cron expression)</mat-label>
                        <input matInput formControlName="schedule" placeholder="*/30 * * * *" />
                      </mat-form-field>
                    </div>

                    <div class="handler-row handler-options">
                      <mat-checkbox formControlName="loop">Loop handler</mat-checkbox>
                      <span class="spacer"></span>
                      <button mat-button type="button" color="warn" (click)="removeHandler(i)">
                        <i class="fas fa-trash"></i>
                        Remove handler
                      </button>
                    </div>

                    <mat-form-field class="handler-args" appearance="outline">
                      <mat-label>Handler arguments (JSON)</mat-label>
                      <textarea
                        matInput
                        formControlName="handlerArgsJson"
                        rows="4"
                        placeholder='{ "key": "value" }'></textarea>
                      @if (handler.get('handlerArgsJson')?.hasError('invalidJson')) {
                        <mat-error>Invalid JSON payload</mat-error>
                      }
                    </mat-form-field>

                    <mat-divider></mat-divider>

                    <div class="events-header">
                      <h3>Events</h3>
                      <button mat-stroked-button color="primary" type="button" (click)="addEvent(i)">
                        <i class="fas fa-plus"></i>
                        Add event
                      </button>
                    </div>

                    <div class="events-grid">
                      @if (eventArray(handler).length === 0) {
                        <p class="events-empty">No events yet. Add actions that run inside this handler.</p>
                      } @else {
                        @for (event of eventArray(handler).controls; track event; let eventIndex = $index) {
                          <div class="event-card" [formGroup]="event">
                            <div class="event-header">
                              <h4>Event {{ eventIndex + 1 }}</h4>
                              <button mat-button class="icon-button" type="button" (click)="removeEvent(i, eventIndex)">
                                <i class="fas fa-trash" style="color: #f44336;"></i>
                              </button>
                            </div>

                            <div class="event-row">
                              <mat-form-field appearance="outline">
                                <mat-label>Command</mat-label>
                                <input matInput formControlName="command" />
                              </mat-form-field>

                              <mat-form-field appearance="outline">
                                <mat-label>Trackable ID</mat-label>
                                <input matInput formControlName="trackableId" />
                              </mat-form-field>
                            </div>

                            <mat-form-field appearance="outline">
                              <mat-label>Command arguments (JSON array or comma separated)</mat-label>
                              <input matInput formControlName="commandArgsText" placeholder='["--flag", "value"]' />
                            </mat-form-field>

                            <div class="event-row">
                              <mat-form-field appearance="outline">
                                <mat-label>Delay before (ms)</mat-label>
                                <input matInput formControlName="delayBefore" type="number" min="0" />
                              </mat-form-field>

                              <mat-form-field appearance="outline">
                                <mat-label>Delay after (ms)</mat-label>
                                <input matInput formControlName="delayAfter" type="number" min="0" />
                              </mat-form-field>
                            </div>
                          </div>
                        }
                      }
                    </div>
                  </div>
                </mat-expansion-panel>
              }
            </mat-accordion>

            <div class="handlers-footer">
              <button mat-raised-button color="primary" type="button" (click)="addHandler()">
                <i class="fas fa-plus"></i>
                Add handler
              </button>
            </div>
          </mat-card-content>
        </mat-card>

        <div class="form-actions">
          <button mat-button type="button" (click)="cancel()">Cancel</button>
          <span class="spacer"></span>
          <button mat-raised-button color="primary" type="submit">
            {{ editingId ? 'Save changes' : 'Create timeline' }}
          </button>
        </div>
      </form>
    </section>
  `,
  styles: [`
    .editor-container {
      display: flex;
      flex-direction: column;
      gap: 24px;
    }

    .editor-header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      flex-wrap: wrap;
      gap: 16px;
    }

    h1 {
      margin: 0;
      font-size: 28px;
      font-weight: 600;
    }

    .subtitle {
      margin: 4px 0 0;
      color: rgba(0, 0, 0, 0.6);
    }

    .header-actions {
      display: flex;
      gap: 12px;
      align-items: center;
    }

    .timeline-form {
      display: flex;
      flex-direction: column;
      gap: 24px;
    }

    mat-card {
      padding-bottom: 16px;
    }

    .form-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
      gap: 16px;
    }

    .handlers-card mat-card-content {
      display: flex;
      flex-direction: column;
      gap: 24px;
    }

    .handler-grid {
      display: flex;
      flex-direction: column;
      gap: 16px;
    }

    .handler-row {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
      gap: 16px;
    }

    .handler-options {
      align-items: center;
    }

    .handler-args textarea {
      font-family: 'Fira Code', 'Menlo', monospace;
    }

    .events-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
    }

    .events-grid {
      display: grid;
      gap: 16px;
    }

    .events-empty {
      margin: 0;
      padding: 16px;
      background: rgba(27, 94, 32, 0.08);
      border-radius: 6px;
      color: rgba(0, 0, 0, 0.6);
    }

    .event-card {
      border: 1px solid rgba(0, 0, 0, 0.08);
      border-radius: 8px;
      padding: 16px;
      display: flex;
      flex-direction: column;
      gap: 16px;
      background: #fafafa;
    }

    .event-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
    }

    .event-row {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
      gap: 16px;
    }

    .form-actions {
      display: flex;
      align-items: center;
      gap: 12px;
    }

    .spacer {
      flex: 1 1 auto;
    }

    @media (max-width: 768px) {
      .handler-row,
      .event-row {
        grid-template-columns: 1fr;
      }
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TimelineEditorComponent implements OnInit {
  protected readonly handlerTypes = HANDLER_TYPES;
  protected readonly scheduleTypes: TimelineScheduleType[] = ['Other', 'Cron'];
  protected expandedPanel: FormGroup | null = null;

  private readonly fb = inject(FormBuilder);
  protected readonly form = this.fb.group({
    name: ['', Validators.required],
    timeLineHandlers: this.fb.array<FormGroup>([])
  });

  protected editingId: string | null = null;

  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);
  private readonly timelineLocalService = inject(TimelineLocalService);
  private readonly dialog = inject(MatDialog);

  get handlerArray(): FormArray<FormGroup> {
    return this.form.controls.timeLineHandlers as FormArray<FormGroup>;
  }

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => {
      const id = params.get('id');
      if (id) {
        this.editingId = id;
        this.loadTimeline(id);
      } else {
        this.ensureAtLeastOneHandler();
      }
    });
  }

  protected addHandler(handler?: TimelineHandler): void {
    const group = this.createHandlerGroup(handler);
    this.handlerArray.push(group);
    this.expandedPanel = group;
  }

  protected removeHandler(index: number): void {
    this.handlerArray.removeAt(index);
    if (this.handlerArray.length === 0) {
      this.ensureAtLeastOneHandler();
    }
  }

  protected addEvent(handlerIndex: number, event?: TimelineEvent): void {
    const handler = this.handlerArray.at(handlerIndex) as FormGroup;
    this.eventArray(handler).push(this.createEventGroup(event));
  }

  protected removeEvent(handlerIndex: number, eventIndex: number): void {
    const handler = this.handlerArray.at(handlerIndex) as FormGroup;
    this.eventArray(handler).removeAt(eventIndex);
  }

  protected eventArray(handler: FormGroup): FormArray<FormGroup> {
    return handler.get('timeLineEvents') as FormArray<FormGroup>;
  }

  protected previewJson(): void {
    try {
      const payload = this.buildTimelinePayload();
      const preview: LocalTimeline = {
        id: this.editingId ?? 'preview',
        name: payload.name,
        timeLineHandlers: payload.timeLineHandlers,
        status: 'Stop'
      };
      this.dialog.open(TimelineJsonDialogComponent, {
        width: '720px',
        data: preview,
        autoFocus: false,
        restoreFocus: false
      });
    } catch (error) {
      this.snackBar.open(
        `Unable to preview JSON: ${(error as Error).message ?? 'unknown error'}`,
        undefined,
        { duration: 3500 }
      );
    }
  }

  protected importFromJson(): void {
    const input = window.prompt('Paste a timeline JSON payload');
    if (!input) {
      return;
    }
    try {
      const parsed = JSON.parse(input) as CreateLocalTimelineRequest;
      if (!parsed.name || !Array.isArray(parsed.timeLineHandlers)) {
        throw new Error('Invalid timeline shape');
      }
      this.applyTimeline(parsed);
      this.snackBar.open('Timeline imported', undefined, { duration: 2500 });
    } catch (error) {
      this.snackBar.open(`Import failed: ${(error as Error).message}`, undefined, {
        duration: 3500
      });
    }
  }

  protected cancel(): void {
    this.router.navigate(['/timelines']);
  }

  protected onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    try {
      const payload = this.buildTimelinePayload();
      if (this.editingId) {
        this.timelineLocalService.update(this.editingId, payload);
      } else {
        const created = this.timelineLocalService.create(payload);
        this.editingId = created.id;
      }
      this.router.navigate(['/timelines']);
    } catch (error) {
      this.snackBar.open(`Unable to save timeline: ${(error as Error).message}`, undefined, {
        duration: 3500
      });
    }
  }

  private loadTimeline(id: string): void {
    const timeline = this.timelineLocalService.getById(id);
    if (!timeline) {
      this.snackBar.open('Timeline not found', undefined, { duration: 3500 });
      this.router.navigate(['/timelines']);
      return;
    }
    this.applyTimeline(timeline);
  }

  private applyTimeline(timeline: CreateLocalTimelineRequest | LocalTimeline): void {
    this.form.controls.name.setValue(timeline.name);
    this.handlerArray.clear();
    timeline.timeLineHandlers.forEach(handler => this.addHandler(handler));
    if (this.handlerArray.length === 0) {
      this.ensureAtLeastOneHandler();
    }
    this.expandedPanel = this.handlerArray.at(0) as FormGroup;
  }

  private ensureAtLeastOneHandler(): void {
    if (this.handlerArray.length === 0) {
      this.addHandler();
    }
  }

  private createHandlerGroup(handler?: TimelineHandler): FormGroup {
    const group = this.fb.group({
      handlerType: [handler?.handlerType ?? HANDLER_TYPES[0], Validators.required],
      initial: [handler?.initial ?? ''],
      utcTimeOn: [handler?.utcTimeOn ?? '00:00:00', [Validators.required, Validators.pattern(TIME_PATTERN)]],
      utcTimeOff: [handler?.utcTimeOff ?? '24:00:00', [Validators.required, Validators.pattern(TIME_PATTERN)]],
      loop: [handler?.loop ?? true],
      scheduleType: [handler?.scheduleType ?? 'Other'],
      schedule: [handler?.schedule ?? ''],
      handlerArgsJson: [
        JSON.stringify(handler?.handlerArgs ?? {}, null, 2),
        this.jsonValidator.bind(this)
      ],
      timeLineEvents: this.fb.array<FormGroup>([])
    });

    if (handler?.timeLineEvents?.length) {
      handler.timeLineEvents.forEach(event => this.eventArray(group).push(this.createEventGroup(event)));
    }

    return group;
  }

  private createEventGroup(event?: TimelineEvent): FormGroup {
    return this.fb.group({
      command: [event?.command ?? ''],
      trackableId: [event?.trackableId ?? ''],
      commandArgsText: [this.stringifyArgs(event?.commandArgs)],
      delayBefore: [event?.delayBefore ?? null],
      delayAfter: [event?.delayAfter ?? null]
    });
  }

  private buildTimelinePayload(): CreateLocalTimelineRequest {
    const handlers = this.handlerArray.controls.map(handler => {
      const events = this.eventArray(handler)
        .controls
        .map(event => ({
          command: this.toNullable(event.value.command),
          trackableId: this.toNullable(event.value.trackableId),
          commandArgs: this.parseCommandArgs(event.value.commandArgsText),
          delayBefore: this.toNullableNumber(event.value.delayBefore),
          delayAfter: this.toNullableNumber(event.value.delayAfter)
        }))
        .filter(event => Object.values(event).some(value => value !== null && value !== undefined));

      return {
        handlerType: handler.value.handlerType,
        initial: this.toNullable(handler.value.initial),
        utcTimeOn: handler.value.utcTimeOn,
        utcTimeOff: handler.value.utcTimeOff,
        loop: handler.value.loop,
        scheduleType: handler.value.scheduleType,
        schedule: this.toNullable(handler.value.schedule),
        handlerArgs: JSON.parse(handler.value.handlerArgsJson || '{}'),
        timeLineEvents: events.length ? events : undefined
      } satisfies TimelineHandler;
    });

    return {
      name: this.form.value.name?.trim() ?? 'Untitled timeline',
      timeLineHandlers: handlers
    };
  }

  private stringifyArgs(args: unknown[] | null | undefined): string {
    if (!args) {
      return '';
    }
    try {
      return JSON.stringify(args);
    } catch {
      return '';
    }
  }

  private parseCommandArgs(value: string | null | undefined): unknown[] | null {
    if (!value) {
      return null;
    }
    const trimmed = value.trim();
    if (!trimmed) {
      return null;
    }
    try {
      const parsed = JSON.parse(trimmed);
      if (Array.isArray(parsed)) {
        return parsed;
      }
    } catch {
      // fall back to comma separated parsing
    }
    return trimmed.split(',').map(item => item.trim()).filter(item => item.length > 0);
  }

  private toNullable(value: string | null | undefined): string | null {
    if (value === undefined || value === null) {
      return null;
    }
    const trimmed = value.trim();
    return trimmed.length > 0 ? trimmed : null;
  }

  private toNullableNumber(value: number | null | undefined): number | null {
    if (value === undefined || value === null) {
      return null;
    }
    const numberValue = Number(value);
    return Number.isNaN(numberValue) ? null : numberValue;
  }

  private jsonValidator(control: { value: string }): { invalidJson: true } | null {
    try {
      if (!control.value) {
        return null;
      }
      JSON.parse(control.value);
      return null;
    } catch {
      return { invalidJson: true };
    }
  }
}
