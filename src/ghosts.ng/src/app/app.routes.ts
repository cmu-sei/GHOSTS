import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadChildren: () => import('./features/home/home.routes').then(m => m.HOME_ROUTES)
  },
  {
    path: 'machines',
    loadChildren: () => import('./features/machines/machines.routes').then(m => m.MACHINES_ROUTES)
  },
  {
    path: 'machine-groups',
    loadChildren: () => import('./features/machine-groups/machine-groups.routes').then(m => m.MACHINE_GROUPS_ROUTES)
  },
  {
    path: 'timelines',
    loadChildren: () => import('./features/timelines/timelines.routes').then(m => m.TIMELINES_ROUTES)
  },
  {
    path: 'npcs',
    loadChildren: () => import('./features/npcs/npcs.routes').then(m => m.NPCS_ROUTES)
  },
  {
    path: 'animations',
    loadChildren: () => import('./features/animations/animations.routes').then(m => m.ANIMATIONS_ROUTES)
  },
  {
    path: 'activities',
    loadChildren: () => import('./features/activities/activities.routes').then(m => m.ACTIVITIES_ROUTES)
  },
  {
    path: 'social',
    loadChildren: () => import('./features/social/social.routes').then(m => m.SOCIAL_ROUTES)
  },
  {
    path: '**',
    redirectTo: ''
  }
];
