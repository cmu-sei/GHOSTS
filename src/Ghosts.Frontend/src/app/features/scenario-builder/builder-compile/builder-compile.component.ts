import { Component, Input, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { ChangeDetectionStrategy } from '@angular/core';
import { ScenarioBuilderService } from '../../../core/services/scenario-builder.service';
import { ScenarioCompilation } from '../../../core/models/scenario-builder.model';

@Component({
  selector: 'app-builder-compile',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatCardModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatCheckboxModule,
    MatIconModule,
    MatChipsModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    MatDialogModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './builder-compile.component.html',
  styleUrls: ['./builder-compile.component.scss'],
})
export class BuilderCompileComponent implements OnInit {
  @Input({ required: true }) scenarioId!: number;

  private readonly builderService = inject(ScenarioBuilderService);
  private readonly fb = inject(FormBuilder);
  private readonly snackBar = inject(MatSnackBar);
  private readonly router = inject(Router);

  protected readonly compilations = signal<ScenarioCompilation[]>([]);
  protected readonly loading = signal(true);
  protected readonly compiling = signal(false);
  protected readonly selectedPackage = signal<any>(null);
  protected readonly lastCompileSucceeded = signal(false);

  protected compileForm!: FormGroup;

  ngOnInit(): void {
    this.initForm();
    this.loadCompilations();
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
      console.warn('Compile form is invalid', this.compileForm.errors);
      this.snackBar.open('Please fill in all required fields', 'Close', { duration: 3000 });
      return;
    }

    console.log('Starting compilation with data:', this.compileForm.value);
    this.compiling.set(true);
    this.builderService.compile(this.scenarioId, this.compileForm.value).subscribe({
      next: (compilation) => {
        console.log('Compilation completed:', compilation);
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
