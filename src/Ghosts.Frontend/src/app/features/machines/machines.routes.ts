import { Routes } from '@angular/router';

export const MACHINES_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./machines-list/machines-list.component').then(m => m.MachinesListComponent)
  }
];
