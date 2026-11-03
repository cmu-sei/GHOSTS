import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./features/scenarios/scenarios-list/scenarios-list.component').then(m => m.ScenariosListComponent)
  },
  {
    path: 'scenarios/:id',
    loadChildren: () => import('./features/workspace/workspace.routes').then(m => m.WORKSPACE_ROUTES)
  },
  {
    path: 'admin',
    loadChildren: () => import('./features/admin/admin.routes').then(m => m.ADMIN_ROUTES)
  },
  { path: '**', redirectTo: '' }
];
