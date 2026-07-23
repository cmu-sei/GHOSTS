import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadChildren: () => import('./features/dashboard/dashboard.routes').then(m => m.DASHBOARD_ROUTES)
  },
  {
    path: 'leaders',
    loadChildren: () => import('./features/leaders/leaders.routes').then(m => m.LEADERS_ROUTES)
  },
  {
    path: 'populations',
    loadChildren: () => import('./features/populations/populations.routes').then(m => m.POPULATIONS_ROUTES)
  },
  {
    path: 'predictions',
    loadChildren: () => import('./features/predictions/predictions.routes').then(m => m.PREDICTIONS_ROUTES)
  },
  {
    path: 'scenarios',
    loadChildren: () => import('./features/scenarios/scenarios.routes').then(m => m.SCENARIOS_ROUTES)
  },
  {
    path: 'cognitive-lab',
    loadChildren: () => import('./features/cognitive-lab/cognitive-lab.routes').then(m => m.COGNITIVE_LAB_ROUTES)
  },
  { path: '**', redirectTo: '' }
];
