import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
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

  // Not used by this template. Left for Dustin's Q1: whether a side panel belongs here too.
  protected readonly assistantOpen = signal(false);

  ngOnInit(): void {
    // Get 'id' from parent route since we're using loadChildren
    const id = this.route.parent?.snapshot.paramMap.get('id') ?? this.route.snapshot.paramMap.get('id');
    if (id) {
      this.scenarioId.set(+id);
      this.loading.set(false);
    } else {
      this.router.navigate(['/scenarios']);
    }
  }

  protected toggleAssistant(): void {
    this.assistantOpen.update((open) => !open);
  }

  protected backToScenarios(): void {
    this.router.navigate(['/scenarios']);
  }
}
