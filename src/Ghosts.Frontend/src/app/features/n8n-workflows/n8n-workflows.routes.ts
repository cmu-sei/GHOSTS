import { Routes } from '@angular/router';

export const N8N_WORKFLOWS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./n8n-workflow-runner/n8n-workflow-runner.component').then(
        m => m.N8nWorkflowRunnerComponent
      )
  }
];
