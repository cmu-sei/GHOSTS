import { Routes } from '@angular/router';

export const RELATIONSHIPS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./relationships-list/relationships-list.component').then(m => m.RelationshipsListComponent)
  }
];
