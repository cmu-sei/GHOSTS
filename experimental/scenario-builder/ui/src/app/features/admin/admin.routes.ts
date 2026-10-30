import { Routes } from '@angular/router';

export const ADMIN_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./libraries-page/libraries-page.component').then(m => m.LibrariesPageComponent)
  },
  {
    path: 'libraries/:id',
    loadComponent: () => import('./library-page/library-page.component').then(m => m.LibraryPageComponent)
  },
  {
    path: 'libraries/:id/templates/:templateId',
    loadComponent: () => import('./template-editor/template-editor.component').then(m => m.TemplateEditorComponent)
  }
];
