import { Routes } from '@angular/router';

export const SOCIAL_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./social-list/social-list.component').then(m => m.SocialListComponent)
  },
  {
    path: 'npc/:npc_id',
    loadComponent: () => import('./social-list/social-list.component').then(m => m.SocialListComponent)
  }
];
