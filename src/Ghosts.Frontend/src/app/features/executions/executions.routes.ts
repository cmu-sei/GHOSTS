import { Routes } from '@angular/router';

export const EXECUTIONS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./executions-list.component').then(
        (m) => m.ExecutionsListComponent
      ),
  },
  {
    path: 'new',
    loadComponent: () =>
      import('./execution-config.component').then(
        (m) => m.ExecutionConfigComponent
      ),
  },
  {
    path: ':id/map',
    loadComponent: () =>
      import('./execution-map.component').then(
        (m) => m.ExecutionMapComponent
      ),
  },
  {
    path: ':id',
    loadComponent: () =>
      import('./execution-details.component').then(
        (m) => m.ExecutionDetailsComponent
      ),
  },
];
