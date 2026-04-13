import { Routes } from '@angular/router';

export const SCENARIO_BUILDER_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./scenario-builder-shell/scenario-builder-shell.component').then(
        (m) => m.ScenarioBuilderShellComponent
      ),
  },
];
