import { Routes } from '@angular/router';

export const WORKSPACE_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./scenario-shell/scenario-shell.component').then(m => m.ScenarioShellComponent),
    children: [
      { path: '', redirectTo: 'questions', pathMatch: 'full' },
      {
        path: 'context',
        loadComponent: () => import('./context-page/context-page.component').then(m => m.ContextPageComponent)
      },
      {
        path: 'questions',
        loadComponent: () => import('./questions-page/questions-page.component').then(m => m.QuestionsPageComponent)
      },
      {
        path: 'summary',
        loadComponent: () => import('./summary-page/summary-page.component').then(m => m.SummaryPageComponent)
      },
      {
        path: 'log',
        loadComponent: () => import('./log-page/log-page.component').then(m => m.LogPageComponent)
      },
      {
        path: 'topology',
        loadComponent: () => import('./topology-page/topology-page.component').then(m => m.TopologyPageComponent)
      },
      {
        path: 'topology/grayspace',
        loadComponent: () => import('./grayspace-page/grayspace-page.component').then(m => m.GraySpacePageComponent)
      },
      {
        path: 'topology/updraft',
        loadComponent: () => import('./updraft-page/updraft-page.component').then(m => m.UpdraftPageComponent)
      },
      {
        path: 'finish',
        loadComponent: () => import('./finish-page/finish-page.component').then(m => m.FinishPageComponent)
      },
      { path: 'json', redirectTo: 'finish' }
    ]
  }
];
