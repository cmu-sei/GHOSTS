import {
  Component,
  OnInit,
  ChangeDetectionStrategy,
  inject,
  signal,
  computed,
} from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { ConfigService } from '../../core/services/config.service';
import { ExecutionService } from '../../core/services/execution.service';
import { ExecutionSummary } from '../../core/models/execution.model';

interface GrafanaPanel {
  title: string;
  dashboardUid: string;
  panelId: number;
  height: string;
}

@Component({
  selector: 'app-assessment',
  standalone: true,
  imports: [],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './assessment.component.html',
  styleUrls: ['./assessment.component.scss'],
})
export class AssessmentComponent implements OnInit {
  private readonly sanitizer = inject(DomSanitizer);
  private readonly configService = inject(ConfigService);
  private readonly executionService = inject(ExecutionService);

  panels: { panel: GrafanaPanel; url: SafeResourceUrl }[] = [];
  grafanaUrl = '';
  executions = signal<ExecutionSummary[]>([]);
  selectedExecution = signal<number | null>(null);

  /** The run dashboard's panels, scoped to the chosen run through its execution variable. */
  runPanels = computed(() => {
    const id = this.selectedExecution();
    return id ? this.runPanelDefs.map(panel => ({ panel, url: this.buildPanelUrl(panel, id) })) : [];
  });

  private readonly runPanelDefs: GrafanaPanel[] = [
    { title: 'Status', dashboardUid: 'ghosts-execution-run', panelId: 1, height: '120px' },
    { title: 'Exercise clock', dashboardUid: 'ghosts-execution-run', panelId: 2, height: '120px' },
    { title: 'Objectives met', dashboardUid: 'ghosts-execution-run', panelId: 3, height: '120px' },
    { title: 'Responses met', dashboardUid: 'ghosts-execution-run', panelId: 4, height: '120px' },
    { title: 'Responses missed', dashboardUid: 'ghosts-execution-run', panelId: 5, height: '120px' },
    { title: 'Items resolved', dashboardUid: 'ghosts-execution-run', panelId: 6, height: '120px' },
    { title: 'Expected responses', dashboardUid: 'ghosts-execution-run', panelId: 7, height: '350px' },
    { title: 'Objectives', dashboardUid: 'ghosts-execution-run', panelId: 8, height: '350px' },
    { title: 'Timeline', dashboardUid: 'ghosts-execution-run', panelId: 10, height: '400px' },
    { title: 'Run events', dashboardUid: 'ghosts-execution-run', panelId: 13, height: '400px' },
  ];

  private readonly panelDefs: GrafanaPanel[] = [
    { title: 'Total Machines', dashboardUid: 'ghosts-simulation-operations', panelId: 20, height: '120px' },
    { title: 'Machines Up', dashboardUid: 'ghosts-simulation-operations', panelId: 21, height: '120px' },
    { title: 'Machines Down', dashboardUid: 'ghosts-simulation-operations', panelId: 22, height: '120px' },
    { title: 'Running Executions', dashboardUid: 'ghosts-simulation-operations', panelId: 2, height: '120px' },
    { title: 'Completed', dashboardUid: 'ghosts-simulation-operations', panelId: 4, height: '120px' },
    { title: 'Failed / Cancelled', dashboardUid: 'ghosts-simulation-operations', panelId: 5, height: '120px' },
    { title: 'Handler Activity Over Time', dashboardUid: 'ghosts-simulation-operations', panelId: 35, height: '350px' },
    { title: 'Machine Registrations Over Time', dashboardUid: 'ghosts-simulation-operations', panelId: 25, height: '350px' },
    { title: 'Handler Breakdown', dashboardUid: 'ghosts-simulation-operations', panelId: 36, height: '400px' },
    { title: 'Machine Inventory', dashboardUid: 'ghosts-simulation-operations', panelId: 26, height: '400px' },
    { title: 'Recent Executions', dashboardUid: 'ghosts-simulation-operations', panelId: 10, height: '400px' },
  ];

  ngOnInit(): void {
    this.grafanaUrl = this.configService.grafanaUrl;
    this.panels = this.panelDefs.map(panel => ({
      panel,
      url: this.buildPanelUrl(panel),
    }));
    this.executionService.getExecutions().subscribe({
      next: (executions) => this.executions.set(executions),
    });
  }

  selectExecution(value: string): void {
    this.selectedExecution.set(value ? Number(value) : null);
  }

  private buildPanelUrl(panel: GrafanaPanel, executionId?: number): SafeResourceUrl {
    const execution = executionId ? `&var-execution=${executionId}` : '';
    const raw = `${this.grafanaUrl}/d-solo/${panel.dashboardUid}/?panelId=${panel.panelId}&theme=light${execution}`;
    return this.sanitizer.bypassSecurityTrustResourceUrl(raw);
  }
}
