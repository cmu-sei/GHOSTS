import { Routes } from '@angular/router';

export const MACHINE_GROUPS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./machine-groups-list/machine-groups-list.component').then(m => m.MachineGroupsListComponent)
  }
];
