import { Routes } from '@angular/router';

export const ADMIN_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'scenario-types' },
  {
    path: 'libraries',
    loadComponent: () => import('./libraries-page/libraries-page.component').then(m => m.LibrariesPageComponent)
  },
  {
    path: 'libraries/:id',
    loadComponent: () => import('./library-page/library-page.component').then(m => m.LibraryPageComponent)
  },
  {
    path: 'scenario-types',
    loadComponent: () => import('./kinds-page/kinds-page.component').then(m => m.KindsPageComponent)
  },
  {
    path: 'scenario-types/:id',
    loadComponent: () => import('./kind-page/kind-page.component').then(m => m.KindPageComponent)
  },
  {
    path: 'libraries/:id/templates/:templateId',
    loadComponent: () => import('./template-editor/template-editor.component').then(m => m.TemplateEditorComponent)
  }
];
