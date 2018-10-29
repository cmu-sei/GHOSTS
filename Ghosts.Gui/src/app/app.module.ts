// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

import { BrowserModule } from '@angular/platform-browser';
import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClientModule } from '@angular/common/http';
import { MomentModule } from 'angular2-moment';
import { ChartsModule } from 'ng2-charts';
import { DataTablesModule } from 'angular-datatables';

import { ConfigService } from './shared/services/config.service';

import { AuthGuard } from "./shared/auth/guards";
import { AuthenticationService } from "./shared/auth/auth-service";
import { LoginComponent } from "./login/login.component";

import { AppComponent } from './app.component';
import { MachinesComponent } from './machines/machines.component';
import { MachineDetailComponent } from './machine-detail/machine-detail.component';
import { HomeComponent } from './home/home.component';
import { HealthComponent } from './health/health.component';
import { NavmenuComponent } from './navmenu/navmenu.component';
import { MachineGroupsComponent } from './machine-groups/machine-groups.component';

const appRoutes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: '', redirectTo: 'home', pathMatch: 'full', canActivate: [AuthGuard] },
  { path: 'home', component: HomeComponent, canActivate: [AuthGuard] },
  { path: 'health', component: HealthComponent, canActivate: [AuthGuard] },
  { path: 'machines', component: MachinesComponent, canActivate: [AuthGuard] },
  { path: 'machines/:id', component: MachineDetailComponent, canActivate: [AuthGuard] },
  { path: 'machine-groups', component: MachineGroupsComponent, canActivate: [AuthGuard] },
  { path: '**', redirectTo: 'home', canActivate: [AuthGuard] }
];

@NgModule({
  declarations: [
    AppComponent,
    MachinesComponent,
    MachineDetailComponent,
    HomeComponent,
    HealthComponent,
    NavmenuComponent,
    MachineGroupsComponent,
    LoginComponent
  ],
  imports: [
    BrowserModule,
    FormsModule,
    RouterModule.forRoot(
      appRoutes,
      { enableTracing: false } // <-- debugging purposes only
    ),
    HttpClientModule,
    MomentModule,
    ChartsModule,
    DataTablesModule
  ],
  providers: [
    ConfigService,
    AuthGuard,
    AuthenticationService
  ],
  bootstrap: [AppComponent]
})
export class AppModule { }