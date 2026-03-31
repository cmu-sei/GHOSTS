import { Component, Input, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatDialogModule } from '@angular/material/dialog';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ChangeDetectionStrategy } from '@angular/core';
import { ScenarioBuilderService } from '../../../core/services/scenario-builder.service';
import { MachineService } from '../../../core/services/machine.service';
import { ScenarioCompilation, NpcForAssignment, DeploymentReadiness } from '../../../core/models/scenario-builder.model';
import { Machine } from '../../../core/models';

@Component({
  selector: 'app-builder-compile',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    FormsModule,
    MatCardModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatCheckboxModule,
    MatIconModule,
    MatChipsModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    MatDialogModule,
    MatTooltipModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './builder-compile.component.html',
  styleUrls: ['./builder-compile.component.scss'],
})
export class BuilderCompileComponent implements OnInit {
  @Input({ required: true }) scenarioId!: number;

  private readonly builderService = inject(ScenarioBuilderService);
  private readonly machineService = inject(MachineService);
  private readonly fb = inject(FormBuilder);
  private readonly snackBar = inject(MatSnackBar);
  private readonly router = inject(Router);

  protected readonly compilations = signal<ScenarioCompilation[]>([]);
  protected readonly loading = signal(true);
  protected readonly compiling = signal(false);
  protected readonly selectedPackage = signal<any>(null);
  protected readonly lastCompileSucceeded = signal(false);

  // Assignment state
  protected readonly expandedAssignmentCompilationId = signal<number | null>(null);
  protected readonly npcsForAssignment = signal<NpcForAssignment[]>([]);
  protected readonly machines = signal<Machine[]>([]);
  protected readonly readiness = signal<DeploymentReadiness | null>(null);
  protected readonly assignmentLoading = signal(false);
  /** tracks selected machineId per npcId for the assignment dropdowns */
  protected readonly pendingSelections = signal<Record<string, string>>({});

  protected compileForm!: FormGroup;

  ngOnInit(): void {
    this.initForm();
    this.loadCompilations();
    this.machineService.getMachines().subscribe({
      next: (machines) => this.machines.set(machines),
      error: () => { /* machines load is best-effort */ }
    });
  }

  private initForm(): void {
    this.compileForm = this.fb.group({
      name: ['', Validators.required],
      generateNpcs: [true],
      generateTimeline: [true],
      mapAttackToInjects: [true],
    });
  }

  private loadCompilations(): void {
    this.loading.set(true);
    this.builderService.getCompilations(this.scenarioId).subscribe({
      next: (compilations) => {
        this.compilations.set(compilations);
        this.loading.set(false);
      },
      error: (error) => {
        console.error('Error loading compilations', error);
        this.snackBar.open('Failed to load compilations', 'Close', { duration: 3000 });
        this.loading.set(false);
      },
    });
  }

  protected compile(): void {
    if (this.compileForm.invalid) {
      this.snackBar.open('Please fill in all required fields', 'Close', { duration: 3000 });
      return;
    }

    this.compiling.set(true);
    this.builderService.compile(this.scenarioId, this.compileForm.value).subscribe({
      next: (compilation) => {
        this.snackBar.open('Compilation completed successfully', 'Close', { duration: 3000 });
        this.compiling.set(false);
        this.lastCompileSucceeded.set(true);
        this.compileForm.patchValue({ name: '' });
        this.loadCompilations();
      },
      error: (error) => {
        console.error('Error compiling scenario:', error);
        const errorMsg = error?.error?.message || error?.message || 'Unknown error';
        this.snackBar.open(`Compilation failed: ${errorMsg}`, 'Close', { duration: 5000 });
        this.compiling.set(false);
      },
    });
  }

  // ── Assignment panel ──────────────────────────────────────────────────────

  protected toggleAssignPanel(compilationId: number): void {
    if (this.expandedAssignmentCompilationId() === compilationId) {
      this.expandedAssignmentCompilationId.set(null);
      return;
    }
    this.expandedAssignmentCompilationId.set(compilationId);
    this.loadNpcsAndReadiness(compilationId);
  }

  private loadNpcsAndReadiness(compilationId: number): void {
    this.assignmentLoading.set(true);
    this.npcsForAssignment.set([]);
    this.readiness.set(null);
    this.pendingSelections.set({});

    this.builderService.getNpcsForAssignment(this.scenarioId, compilationId).subscribe({
      next: (npcs) => {
        this.npcsForAssignment.set(npcs);
        // pre-populate pending selections from current assignments
        const sel: Record<string, string> = {};
        npcs.forEach(n => { if (n.assignedMachineId) sel[n.npcId] = n.assignedMachineId; });
        this.pendingSelections.set(sel);
        this.assignmentLoading.set(false);
        this.refreshReadiness(compilationId);
      },
      error: (err) => {
        console.error('Error loading NPCs for assignment', err);
        this.snackBar.open('Failed to load NPCs', 'Close', { duration: 3000 });
        this.assignmentLoading.set(false);
      }
    });
  }

  private refreshReadiness(compilationId: number): void {
    this.builderService.getDeploymentReadiness(this.scenarioId, compilationId).subscribe({
      next: (r) => this.readiness.set(r),
      error: () => { /* non-critical */ }
    });
  }

  protected onMachineSelected(compilationId: number, npcId: string, machineId: string | null): void {
    if (machineId === null) {
      // User selected "-- unassigned --": find and delete the existing assignment
      const npc = this.npcsForAssignment().find(n => n.npcId === npcId);
      if (!npc?.assignmentId) {
        // Nothing assigned yet — just clear the pending selection
        const sel = { ...this.pendingSelections() };
        delete sel[npcId];
        this.pendingSelections.set(sel);
        return;
      }
      this.builderService.deleteAssignment(this.scenarioId, compilationId, npc.assignmentId).subscribe({
        next: () => {
          this.snackBar.open('Assignment removed', 'Close', { duration: 2000 });
          const sel = { ...this.pendingSelections() };
          delete sel[npcId];
          this.pendingSelections.set(sel);
          this.loadNpcsAndReadiness(compilationId);
        },
        error: (err) => {
          console.error('Error removing assignment', err);
          this.snackBar.open('Failed to remove assignment', 'Close', { duration: 3000 });
        }
      });
      return;
    }

    this.builderService.createAssignment(this.scenarioId, compilationId, npcId, machineId).subscribe({
      next: () => {
        this.snackBar.open('Assignment saved', 'Close', { duration: 2000 });
        this.loadNpcsAndReadiness(compilationId);
      },
      error: (err) => {
        console.error('Error saving assignment', err);
        this.snackBar.open('Failed to save assignment', 'Close', { duration: 3000 });
      }
    });
  }

  // ── Other ─────────────────────────────────────────────────────────────────

  protected viewPackage(compilationId: number): void {
    this.builderService.getPackage(this.scenarioId, compilationId).subscribe({
      next: (pkg) => {
        this.selectedPackage.set(pkg);
      },
      error: (error) => {
        console.error('Error loading package', error);
        this.snackBar.open('Failed to load package', 'Close', { duration: 3000 });
      },
    });
  }

  protected closePackageView(): void {
    this.selectedPackage.set(null);
  }

  protected reviewScenarioForm(): void {
    this.router.navigate(['/scenarios', this.scenarioId]);
  }

  protected downloadPackage(compilationId: number): void {
    this.builderService.getPackage(this.scenarioId, compilationId).subscribe({
      next: (pkg) => {
        const json = JSON.stringify(pkg, null, 2);
        const blob = new Blob([json], { type: 'application/json' });
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `scenario-${this.scenarioId}-compilation-${compilationId}.json`;
        link.click();
        URL.revokeObjectURL(url);
        link.remove();
        this.snackBar.open('Package downloaded', 'Close', { duration: 2000 });
      },
      error: (error) => {
        console.error('Error downloading package', error);
        this.snackBar.open('Failed to download package', 'Close', { duration: 3000 });
      },
    });
  }

  protected deleteCompilation(compilationId: number): void {
    if (!confirm('Are you sure you want to delete this compilation?')) return;

    this.builderService.deleteCompilation(this.scenarioId, compilationId).subscribe({
      next: () => {
        this.snackBar.open('Compilation deleted', 'Close', { duration: 2000 });
        this.loadCompilations();
      },
      error: (error) => {
        console.error('Error deleting compilation', error);
        this.snackBar.open('Failed to delete compilation', 'Close', { duration: 3000 });
      },
    });
  }

  protected getStatusColor(status: string): string {
    switch (status?.toLowerCase()) {
      case 'completed':
        return 'primary';
      case 'processing':
        return 'accent';
      case 'failed':
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

  protected formatJson(obj: any): string {
    return JSON.stringify(obj, null, 2);
  }
}
