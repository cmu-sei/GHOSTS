import { Routes } from '@angular/router';

// One URL per step (…/builder/sources, …/builder/conversation), so a reload or a pasted link lands on the
// step it names. One route for both, so switching steps keeps the shell and its state.
export const SCENARIO_BUILDER_ROUTES: Routes = [
  { path: '', redirectTo: 'sources', pathMatch: 'full' },
  {
    path: ':step',
    loadComponent: () =>
      import('./scenario-builder-shell/scenario-builder-shell.component').then(
        (m) => m.ScenarioBuilderShellComponent
      ),
  },
];
