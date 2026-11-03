import { ChangeDetectionStrategy, Component, OnInit, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter, map } from 'rxjs';
import { TopBarComponent } from '../../../shared/components/top-bar/top-bar.component';
import { AgentPanelComponent } from '../agent-panel/agent-panel.component';
import { KindUpdateComponent } from '../kind-update/kind-update.component';
import { StageRailComponent } from '../stage-rail/stage-rail.component';
import { WorkspaceStore } from '../workspace.store';

/** A scenario's workspace: the page, the agent beside it, and the stage rail on the Questions page. */
@Component({
  selector: 'app-scenario-shell',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, TopBarComponent, StageRailComponent, AgentPanelComponent, KindUpdateComponent],
  providers: [WorkspaceStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './scenario-shell.component.html',
  styleUrls: ['./scenario-shell.component.scss']
})
export class ScenarioShellComponent implements OnInit {
  protected readonly store = inject(WorkspaceStore);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  /** The rail picks the stage the Questions page shows, so it shows only there. */
  protected readonly onQuestions = toSignal(
    this.router.events.pipe(filter(e => e instanceof NavigationEnd), map(() => this.questionsOpen())),
    { initialValue: this.questionsOpen() }
  );

  /** Topology shows only for a kind that has one. */
  protected readonly pages = computed(() => [
    { path: 'context', label: 'Context' },
    { path: 'questions', label: 'Questions' },
    { path: 'summary', label: 'Summary' },
    { path: 'log', label: 'Decision Log' },
    ...(this.store.kind()?.topology ? [{ path: 'topology', label: 'Topology' }] : []),
    { path: 'finish', label: 'Finish' }
  ]);

  ngOnInit(): void {
    this.store.load(Number(this.route.snapshot.paramMap.get('id')));
  }

  /** A stage picked on the rail shows on the Questions page. */
  protected pick(stage: number): void {
    this.store.selectedStage.set(stage);
  }

  private questionsOpen(): boolean {
    return this.route.snapshot.firstChild?.routeConfig?.path === 'questions';
  }
}
