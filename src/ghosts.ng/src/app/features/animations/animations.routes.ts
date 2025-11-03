import { Routes } from '@angular/router';

export const ANIMATIONS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./animations-list/animations-list.component').then(m => m.AnimationsListComponent)
  }
];
