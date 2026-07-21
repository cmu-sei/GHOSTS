import { Routes } from '@angular/router';
import { LeadersListComponent } from './leaders-list/leaders-list.component';
import { LeaderDetailComponent } from './leader-detail/leader-detail.component';

export const LEADERS_ROUTES: Routes = [
  { path: '', component: LeadersListComponent },
  { path: ':id', component: LeaderDetailComponent }
];
