import { Component, ChangeDetectionStrategy, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { PopulationService } from '../../../core/services/population.service';
import { PopulationProfile } from '../../../core/models/population.model';

@Component({
  selector: 'app-population-create-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>Create Population Profile</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="create-form">
        <mat-form-field appearance="outline">
          <mat-label>Country</mat-label>
          <input matInput formControlName="country" placeholder="e.g., Russia" />
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Total Population</mat-label>
          <input matInput type="number" formControlName="total_population" placeholder="e.g., 144000000" />
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button mat-flat-button color="primary" [disabled]="form.invalid" (click)="submit()">
        <mat-icon>add</mat-icon> Create
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .create-form {
      display: flex;
      flex-direction: column;
      gap: 8px;
      min-width: 350px;
      padding-top: 8px;
    }
  `],
})
export class PopulationCreateDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly dialogRef = inject(MatDialogRef<PopulationCreateDialogComponent>);
  private readonly populationService = inject(PopulationService);

  form = this.fb.group({
    country: ['', Validators.required],
    total_population: [0, [Validators.required, Validators.min(1)]],
  });

  submit(): void {
    if (this.form.invalid) return;
    const val = this.form.getRawValue();
    const profile: PopulationProfile = {
      country: val.country!,
      total_population: val.total_population!,
      period: 'current',
      segments: [{
        name: 'General Population',
        percentage: 1.0,
        demographics: { urbanization: 0.5, education_level: 0.5, median_age: 35, economic_class: 'middle' },
        disposition: { government_trust: 0.5, nationalism: 0.5, media_exposure: 0.5, social_media_activity: 0.3, protest_propensity: 0.2, compliance_baseline: 0.7, information_sources: ['state_tv'] },
        response_params: { rally_around_flag_coefficient: 0.3, economic_sensitivity: 0.5, fatigue_rate: 0.1, amplification_factor: 0.3 }
      }],
    };
    this.populationService.create(profile).subscribe({
      next: (created) => this.dialogRef.close(created),
      error: () => this.dialogRef.close(profile),
    });
  }
}
