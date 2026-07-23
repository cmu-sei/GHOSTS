import { Routes } from '@angular/router';
import { PredictionListComponent } from './prediction-list/prediction-list.component';
import { PredictionsShellComponent } from './predictions-shell/predictions-shell.component';
import { PredictionDetailComponent } from './prediction-detail/prediction-detail.component';

export const PREDICTIONS_ROUTES: Routes = [
  { path: '', component: PredictionListComponent },
  { path: 'new', component: PredictionsShellComponent },
  { path: ':id', component: PredictionDetailComponent },
];
