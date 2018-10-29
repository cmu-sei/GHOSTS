// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

import { Component, OnInit } from '@angular/core';
import { Headers, RequestOptions, Response } from '@angular/http';
import { HttpClient, HttpParams, HttpHeaders } from "@angular/common/http";
import { ConfigService } from "../shared/services/config.service";
import { Observable } from "rxjs/Rx";
import { AuthenticationService } from '../shared/auth/auth-service';

@Component({
  selector: 'app-home',
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.css']
})

export class HomeComponent extends ConfigService implements OnInit {
  
  constructor(private http: HttpClient, private configService: ConfigService, private authenticationService: AuthenticationService) {
    super();
    this.baseUrl = configService.apiUrl;
    let options = new HttpHeaders({ 'Authorization': 'Bearer ' + this.authenticationService.token });
    
    Observable.interval(300000).startWith(0)
      .switchMap(() => http.get(this.baseUrl + "/dashboard", { headers: options }))
      .catch((error:any) => {
        if(error.status === 401) {
          localStorage.removeItem('currentUser');
          window.location.href = "/login";
        }
        return Observable.empty(null);
      })
      .subscribe((data) => {
        if(data === null || data.toString().length < 1){
          this.nodata = true;
          return Observable.empty(null);
        }
        this.dashboard = data as Dashboard;
        this.lineChartData = this.dashboard.chartItems;
        this.lineChartLabels = this.dashboard.chartLabels;
      })
  };
  
  ngOnInit() {
  }

  public nodata:boolean = false;
  public dashboard: Dashboard;
  baseUrl: string = "";

  public lineChartData:Array<any>;
  public lineChartLabels:Array<any>;
  public lineChartOptions:any = {
    responsive: true
  };
  public lineChartColors:Array<any> = [];
  public lineChartLegend:boolean = true;
  public lineChartType:string = 'line';
 
  // events
  public chartClicked(e:any):void {
    console.log(e);
  }
 
  public chartHovered(e:any):void {
    console.log(e);
  }
}

interface Dashboard {
  machinesTracked: number;
  clientOperations: number;
  hoursManaged: number;
  machinesWithHealthIssues: number;
  chartLabels: string[];
  chartItems: ChartItem[];
}

interface ChartItem {
  Label: string;
  Data: number[];
}
