import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { StepperSelectionEvent } from '@angular/cdk/stepper';
import { MatStepperModule } from '@angular/material/stepper';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ChangeDetectionStrategy } from '@angular/core';
import { BuilderSourcesComponent } from '../builder-sources/builder-sources.component';
import { BuilderConversationComponent } from '../builder-conversation/builder-conversation.component';

@Component({
  selector: 'app-scenario-builder-shell',
  standalone: true,
  imports: [
    CommonModule,
    MatStepperModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    MatProgressSpinnerModule,
    BuilderSourcesComponent,
    BuilderConversationComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './scenario-builder-shell.component.html',
  styleUrls: ['./scenario-builder-shell.component.scss'],
})
export class ScenarioBuilderShellComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly scenarioId = signal<number | null>(null);
  protected readonly loading = signal(true);

  /** The steps in stepper order; the URL's last segment names the current one. */
  private readonly steps = ['sources', 'conversation'];
  protected readonly selectedIndex = signal(0);

  ngOnInit(): void {
    // Get 'id' from parent route since we're using loadChildren
    const id = this.route.parent?.snapshot.paramMap.get('id') ?? this.route.snapshot.paramMap.get('id');
    if (id) {
      this.scenarioId.set(+id);
      this.loading.set(false);
    } else {
      this.router.navigate(['/scenarios']);
    }
    // The URL drives the stepper, so a reload, a pasted link and the back button all land on the step named.
    this.route.paramMap.subscribe((params) => {
      const index = this.steps.indexOf(params.get('step') ?? '');
      if (index >= 0) this.selectedIndex.set(index);
      else this.router.navigate(['/scenarios', id, 'builder', this.steps[0]], { replaceUrl: true });
    });
  }

  /** The stepper drives the URL: a header click or a Next button becomes a navigation to that step's URL. */
  protected onStepChange(event: StepperSelectionEvent): void {
    const step = this.steps[event.selectedIndex];
    if (step && step !== this.route.snapshot.paramMap.get('step')) {
      this.router.navigate(['/scenarios', this.scenarioId(), 'builder', step]);
    }
  }

  protected backToScenarios(): void {
    this.router.navigate(['/scenarios']);
  }
}
