import { Routes } from '@angular/router';

export const ACTIVITIES_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./activities-list/activities-list.component').then(m => m.ActivitiesListComponent)
  },
  {
    path: 'dynamic',
    loadComponent: () => import('./activities-dynamic/activities-dynamic').then(m => m.ActivitiesDynamicComponent)
  }
];
