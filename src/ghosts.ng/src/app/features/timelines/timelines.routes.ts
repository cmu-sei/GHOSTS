import { Routes } from '@angular/router';

export const TIMELINES_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./timelines-list/timelines-list.component').then(m => m.TimelinesListComponent)
  },
  {
    path: 'new',
    loadComponent: () => import('./timeline-editor/timeline-editor.component').then(m => m.TimelineEditorComponent)
  },
  {
    path: ':id/edit',
    loadComponent: () => import('./timeline-editor/timeline-editor.component').then(m => m.TimelineEditorComponent)
  }
];
