import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TopBarComponent } from '../../../shared/components/top-bar/top-bar.component';
import { AgentPanelComponent } from '../agent-panel/agent-panel.component';
import { StageRailComponent } from '../stage-rail/stage-rail.component';
import { WorkspaceStore } from '../workspace.store';

/** A scenario's workspace: the stage rail, the page, and the agent beside it. */
@Component({
  selector: 'app-scenario-shell',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, TopBarComponent, StageRailComponent, AgentPanelComponent],
  providers: [WorkspaceStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './scenario-shell.component.html',
  styleUrls: ['./scenario-shell.component.scss']
})
export class ScenarioShellComponent implements OnInit {
  protected readonly store = inject(WorkspaceStore);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly pages = [
    { path: 'context', label: 'Context' },
    { path: 'questions', label: 'Questions' },
    { path: 'summary', label: 'Summary' },
    { path: 'log', label: 'Decision Log' },
    { path: 'finish', label: 'Finish' }
  ];

  ngOnInit(): void {
    this.store.load(Number(this.route.snapshot.paramMap.get('id')));
  }

  /** A stage picked on the rail shows on the Questions page, from wherever the rail was clicked. */
  protected pick(stage: number): void {
    this.store.selectedStage.set(stage);
    if (!this.router.url.endsWith('/questions')) {
      this.router.navigate(['questions'], { relativeTo: this.route });
    }
  }
}
