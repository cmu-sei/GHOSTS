import { Routes } from '@angular/router';

export const SCENARIOS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./scenarios-list/scenarios-list.component').then(m => m.ScenariosListComponent)
  },
  {
    path: ':id',
    loadComponent: () => import('./scenarios-planner/scenarios-planner.component').then(m => m.ScenariosPlannerComponent)
  }
];
