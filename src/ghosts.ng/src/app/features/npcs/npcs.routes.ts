import { Routes } from '@angular/router';

export const NPCS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./npcs-list/npcs-list.component').then(m => m.NpcsListComponent)
  },
  {
    path: ':npc_id',
    loadComponent: () => import('./npcs-detail/npcs-detail').then(m => m.NpcsDetail)
  }
];
