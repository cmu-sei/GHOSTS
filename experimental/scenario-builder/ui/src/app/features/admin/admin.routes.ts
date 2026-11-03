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
    loadComponent: () => import('./scenario-types-page/scenario-types-page.component').then(m => m.ScenarioTypesPageComponent)
  },
  {
    path: 'scenario-types/:id',
    loadComponent: () => import('./scenario-type-page/scenario-type-page.component').then(m => m.ScenarioTypePageComponent)
  },
  {
    path: 'libraries/:id/templates/:templateId',
    loadComponent: () => import('./template-editor/template-editor.component').then(m => m.TemplateEditorComponent)
  }
];
