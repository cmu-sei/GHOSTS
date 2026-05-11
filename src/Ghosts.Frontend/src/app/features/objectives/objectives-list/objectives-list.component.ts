import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatChipsModule } from '@angular/material/chips';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatMenuModule } from '@angular/material/menu';
import { ChangeDetectionStrategy } from '@angular/core';
import { ObjectiveService } from '../../../core/services';
import {
  Objective,
  OBJECTIVE_TYPES,
  OBJECTIVE_STATUSES,
  TASK_SCORES
} from '../../../core/models/objective.model';

@Component({
  selector: 'app-objectives-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatButtonModule,
    MatCardModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatChipsModule,
    MatTooltipModule,
    MatMenuModule
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './objectives-list.component.html',
  styleUrls: ['./objectives-list.component.scss']
})
export class ObjectivesListComponent implements OnInit {
  private readonly objectiveService = inject(ObjectiveService);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly objectives = signal<Objective[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly searchTerm = signal('');
  protected readonly filterType = signal<string>('');
  protected readonly filterStatus = signal<string>('');
  protected readonly expandedIds = signal<Set<number>>(new Set());
  protected readonly showNewForm = signal(false);
  protected readonly editingId = signal<number | null>(null);
  protected readonly addingChildToId = signal<number | null>(null);

  protected readonly objectiveTypes = OBJECTIVE_TYPES;
  protected readonly objectiveStatuses = OBJECTIVE_STATUSES;
  protected readonly taskScores = TASK_SCORES;

  protected newObjective = this.freshObjective();
  protected newChild = this.freshChild();
  protected editForm: any = {};

  protected readonly filteredObjectives = computed(() => {
    let result = this.objectives();
    const search = this.searchTerm().toLowerCase().trim();
    const type = this.filterType();
    const status = this.filterStatus();

    if (search) {
      result = result.filter(o => this.matchesSearch(o, search));
    }
    if (type) {
      result = result.filter(o => this.matchesType(o, type));
    }
    if (status) {
      result = result.filter(o => this.matchesStatus(o, status));
    }
    return result;
  });

  protected readonly stats = computed(() => {
    const all = this.objectives();
    const flat = this.flattenAll(all);
    return {
      total: all.length,
      active: all.filter(o => o.status === 'Active').length,
      achieved: all.filter(o => o.status === 'Achieved').length,
      children: flat.length - all.length,
      trained: flat.filter(o => o.score === 'T').length
    };
  });

  ngOnInit(): void {
    this.loadObjectives();
  }

  private searchTimer: any;

  protected onSearchChange(value: string): void {
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.searchTerm.set(value), 200);
  }

  protected loadObjectives(): void {
    this.loading.set(true);
    this.error.set(null);

    this.objectiveService.getAll().subscribe({
      next: (objectives) => {
        this.objectives.set(objectives);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Failed to load objectives. Make sure the API is running.');
        this.loading.set(false);
      }
    });
  }

  protected toggleExpand(id: number): void {
    const current = new Set(this.expandedIds());
    if (current.has(id)) {
      current.delete(id);
    } else {
      current.add(id);
    }
    this.expandedIds.set(current);
  }

  protected isExpanded(id: number): boolean {
    return this.expandedIds().has(id);
  }

  // -- Inline create --

  private freshObjective() {
    return { name: '', description: '', type: 'MET', priority: 3, successCriteria: '', assigned: '' };
  }

  private freshChild() {
    return { name: '', description: '', assigned: '' };
  }

  protected openNewForm(): void {
    this.newObjective = this.freshObjective();
    this.showNewForm.set(true);
  }

  protected cancelNew(): void {
    this.showNewForm.set(false);
  }

  protected saveNew(): void {
    if (!this.newObjective.name.trim()) return;
    this.objectiveService.create({
      ...this.newObjective,
      parentId: null,
      scenarioId: null
    }).subscribe({
      next: (created) => {
        this.showNewForm.set(false);
        this.loadObjectives();
        const expanded = new Set(this.expandedIds());
        expanded.add(created.id);
        this.expandedIds.set(expanded);
      },
      error: () => this.snackBar.open('Failed to create objective', 'Close', { duration: 3000 })
    });
  }

  // -- Add child --

  protected startAddChild(parentId: number, event: Event): void {
    event.stopPropagation();
    this.newChild = this.freshChild();
    this.addingChildToId.set(parentId);
    const expanded = new Set(this.expandedIds());
    expanded.add(parentId);
    this.expandedIds.set(expanded);
  }

  protected cancelAddChild(): void {
    this.addingChildToId.set(null);
  }

  protected saveChild(parent: Objective): void {
    if (!this.newChild.name.trim()) return;
    this.objectiveService.create({
      parentId: parent.id,
      scenarioId: parent.scenarioId,
      name: this.newChild.name,
      description: this.newChild.description,
      type: parent.type,
      priority: parent.priority,
      successCriteria: '',
      assigned: this.newChild.assigned
    }).subscribe({
      next: () => {
        this.addingChildToId.set(null);
        this.loadObjectives();
      },
      error: () => this.snackBar.open('Failed to add child', 'Close', { duration: 3000 })
    });
  }

  // -- Inline edit --

  protected startEdit(objective: Objective, event: Event): void {
    event.stopPropagation();
    this.editingId.set(objective.id);
    this.editForm = {
      name: objective.name,
      description: objective.description,
      type: objective.type,
      status: objective.status,
      score: objective.score,
      priority: objective.priority,
      successCriteria: objective.successCriteria,
      assigned: objective.assigned,
      sortOrder: objective.sortOrder
    };
    const expanded = new Set(this.expandedIds());
    expanded.add(objective.id);
    this.expandedIds.set(expanded);
  }

  protected cancelEdit(): void {
    this.editingId.set(null);
  }

  protected saveEdit(id: number): void {
    if (!this.editForm.name?.trim()) return;
    this.objectiveService.update(id, this.editForm).subscribe({
      next: () => {
        this.editingId.set(null);
        this.loadObjectives();
      },
      error: () => this.snackBar.open('Failed to update objective', 'Close', { duration: 3000 })
    });
  }

  protected deleteObjective(objective: Objective, event: Event): void {
    event.stopPropagation();
    const hasChildren = objective.children?.length > 0;
    const msg = hasChildren
      ? `Delete "${objective.name}" and all its children?`
      : `Delete "${objective.name}"?`;
    if (!confirm(msg)) return;

    this.objectiveService.delete(objective.id).subscribe({
      next: () => {
        this.snackBar.open('Objective deleted', 'Close', { duration: 3000 });
        this.loadObjectives();
      },
      error: () => this.snackBar.open('Failed to delete objective', 'Close', { duration: 3000 })
    });
  }

  // -- Status quick-change --

  protected updateObjectiveStatus(objective: Objective, status: string, event: Event): void {
    event.stopPropagation();
    this.objectiveService.update(objective.id, {
      name: objective.name,
      description: objective.description,
      type: objective.type,
      status,
      score: objective.score,
      priority: objective.priority,
      successCriteria: objective.successCriteria,
      assigned: objective.assigned,
      sortOrder: objective.sortOrder
    }).subscribe({
      next: () => this.loadObjectives(),
      error: () => this.snackBar.open('Failed to update status', 'Close', { duration: 3000 })
    });
  }

  // -- Score --

  protected updateScore(objective: Objective, score: string, event: Event): void {
    event.stopPropagation();
    this.objectiveService.update(objective.id, {
      name: objective.name,
      description: objective.description,
      type: objective.type,
      status: objective.status,
      score,
      priority: objective.priority,
      successCriteria: objective.successCriteria,
      assigned: objective.assigned,
      sortOrder: objective.sortOrder
    }).subscribe({
      next: () => this.loadObjectives(),
      error: () => this.snackBar.open('Failed to update score', 'Close', { duration: 3000 })
    });
  }

  // -- Helpers --

  protected getStatusColor(status: string): string {
    return OBJECTIVE_STATUSES.find(s => s.value === status)?.color || '#9e9e9e';
  }

  protected getScoreLabel(score: string): string {
    return TASK_SCORES.find(s => s.value === score)?.label || score;
  }

  protected getTypeLabel(type: string): string {
    return OBJECTIVE_TYPES.find(t => t.value === type)?.label || type;
  }

  protected getTypeDescription(type: string): string {
    return OBJECTIVE_TYPES.find(t => t.value === type)?.description || '';
  }

  protected formatDate(date: string): string {
    return new Date(date).toLocaleDateString('en-US', { year: 'numeric', month: 'short', day: 'numeric' });
  }

  protected getPriorityLabel(priority: number): string {
    const labels: Record<number, string> = { 1: 'Critical', 2: 'High', 3: 'Medium', 4: 'Low', 5: 'Minimal' };
    return labels[priority] || `P${priority}`;
  }

  protected getChildProgress(objective: Objective): number {
    if (!objective.children?.length) return 0;
    const flat = this.flattenChildren(objective);
    if (!flat.length) return 0;
    return Math.round((flat.filter(o => o.score === 'T').length / flat.length) * 100);
  }

  protected getTrainedCount(objective: Objective): number {
    return this.flattenChildren(objective).filter(o => o.score === 'T').length;
  }

  protected getTotalDescendants(objective: Objective): number {
    return this.flattenChildren(objective).length;
  }

  private flattenChildren(objective: Objective): Objective[] {
    const result: Objective[] = [];
    const walk = (children: Objective[]) => {
      for (const child of children) {
        result.push(child);
        if (child.children?.length) walk(child.children);
      }
    };
    if (objective.children?.length) walk(objective.children);
    return result;
  }

  private flattenAll(objectives: Objective[]): Objective[] {
    const result: Objective[] = [];
    const walk = (list: Objective[]) => {
      for (const o of list) {
        result.push(o);
        if (o.children?.length) walk(o.children);
      }
    };
    walk(objectives);
    return result;
  }

  private matchesSearch(o: Objective, search: string): boolean {
    if (o.name.toLowerCase().includes(search) || o.description.toLowerCase().includes(search)) return true;
    return o.children?.some(c => this.matchesSearch(c, search)) || false;
  }

  private matchesType(o: Objective, type: string): boolean {
    if (o.type === type) return true;
    return o.children?.some(c => this.matchesType(c, type)) || false;
  }

  private matchesStatus(o: Objective, status: string): boolean {
    if (o.status === status) return true;
    return o.children?.some(c => this.matchesStatus(c, status)) || false;
  }
}
