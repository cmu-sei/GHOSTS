import { Component, ChangeDetectionStrategy, output, input, inject, signal, computed } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSliderModule } from '@angular/material/slider';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { LeaderProfile } from '../../../core/models/leader.model';
import { GeopoliticalEvent, EVENT_TYPES, ACTION_CATEGORIES, REVERSIBILITY_OPTIONS, VISIBILITY_OPTIONS } from '../../../core/models/event.model';

export interface PredictionSubmitEvent {
  event: GeopoliticalEvent;
  targetLeaderId: string;
}

@Component({
  selector: 'app-event-form',
  standalone: true,
  imports: [
    DecimalPipe, ReactiveFormsModule, MatFormFieldModule, MatInputModule,
    MatSelectModule, MatSliderModule, MatButtonModule, MatIconModule
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './event-form.component.html',
  styleUrl: './event-form.component.scss'
})
export class EventFormComponent {
  private readonly fb = inject(FormBuilder);
  submitEvent = output<PredictionSubmitEvent>();

  leaders = input<LeaderProfile[]>([]);

  leadersByPeriod = computed(() => {
    const groups: { period: string; leaders: LeaderProfile[] }[] = [];
    const map = new Map<string, LeaderProfile[]>();
    for (const l of this.leaders()) {
      const period = l.period || 'current';
      if (!map.has(period)) map.set(period, []);
      map.get(period)!.push(l);
    }
    for (const [period, leaders] of map) {
      groups.push({ period, leaders });
    }
    groups.sort((a, b) => a.period === 'current' ? -1 : b.period === 'current' ? 1 : a.period.localeCompare(b.period));
    return groups;
  });

  selectedActorId = signal('');
  selectedTargetId = signal('');

  eventTypes = EVENT_TYPES;
  actionCategories = ACTION_CATEGORIES;
  reversibilityOptions = REVERSIBILITY_OPTIONS;
  visibilityOptions = VISIBILITY_OPTIONS;

  form = this.fb.group({
    event_type: ['military_action', Validators.required],
    severity: [0.5, [Validators.required, Validators.min(0), Validators.max(1)]],
    description: ['', Validators.required],
    actor: ['', Validators.required],
    target: ['', Validators.required],
    action_category: ['military', Validators.required],
    reversibility: ['reversible', Validators.required],
    visibility: ['public', Validators.required],
  });

  onActorSelected(leaderId: string): void {
    this.selectedActorId.set(leaderId);
    const leader = this.leaders().find(l => l.id === leaderId);
    if (leader) {
      this.form.patchValue({ actor: leader.country });
    }
  }

  onTargetSelected(leaderId: string): void {
    this.selectedTargetId.set(leaderId);
    const leader = this.leaders().find(l => l.id === leaderId);
    if (leader) {
      this.form.patchValue({ target: leader.country });
    }
  }

  onSubmit(): void {
    if (this.form.invalid) return;
    const targetId = this.selectedTargetId();
    if (!targetId && this.leaders().length) return;

    const v = this.form.getRawValue();
    const event: GeopoliticalEvent = {
      event_type: v.event_type!,
      severity: v.severity!,
      description: v.description!,
      structured: {
        actor: v.actor!,
        target: v.target!,
        action_category: v.action_category!,
        reversibility: v.reversibility!,
        visibility: v.visibility!,
      }
    };
    this.submitEvent.emit({ event, targetLeaderId: targetId });
  }
}
