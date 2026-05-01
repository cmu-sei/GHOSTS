import {
  Component,
  OnInit,
  ChangeDetectionStrategy,
  inject,
} from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { ConfigService } from '../../core/services/config.service';

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

  panels: { panel: GrafanaPanel; url: SafeResourceUrl }[] = [];
  grafanaUrl = '';

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
  }

  private buildPanelUrl(panel: GrafanaPanel): SafeResourceUrl {
    const raw = `${this.grafanaUrl}/d-solo/${panel.dashboardUid}/?panelId=${panel.panelId}&theme=light`;
    return this.sanitizer.bypassSecurityTrustResourceUrl(raw);
  }
}
