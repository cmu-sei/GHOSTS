import { Routes } from '@angular/router';
import { PopulationsListComponent } from './populations-list/populations-list.component';
import { PopulationDetailComponent } from './population-detail/population-detail.component';

export const POPULATIONS_ROUTES: Routes = [
  { path: '', component: PopulationsListComponent },
  { path: ':country', component: PopulationDetailComponent }
];
