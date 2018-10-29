// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

import { Component, OnInit } from "@angular/core";
import { Headers, RequestOptions, Response } from '@angular/http';
import { HttpClient, HttpParams, HttpHeaders } from "@angular/common/http";
import { ActivatedRoute } from "@angular/router";
import { ConfigService } from "../shared/services/config.service";
import { Observable } from "rxjs/Rx";
import { AuthenticationService } from '../shared/auth/auth-service';

@Component({
  selector: "app-machine-detail",
  templateUrl: "./machine-detail.component.html",
  styleUrls: ["./machine-detail.component.css"]
})

export class MachineDetailComponent extends ConfigService implements OnInit {
  public id: string;
  public machine: Machine;
  baseUrl: string = "";
  dtOptions: DataTables.Settings = {};

  constructor(private http: HttpClient, private configService: ConfigService, private route: ActivatedRoute, private authenticationService: AuthenticationService) {
    super();
    this.baseUrl = configService.apiUrl;
    this.id = route.snapshot.params["id"];
    let options = new HttpHeaders({ 'Authorization': 'Bearer ' + this.authenticationService.token });

    Observable.interval(300000).startWith(0)
      .switchMap(() => http.get(this.baseUrl + "/machines/" + this.id, { headers: options }))
      .catch((error:any) => {
        if(error.status === 401) {
          localStorage.removeItem('currentUser');
          window.location.href = "/login";
        }
        return Observable.empty(null);
      })
      .subscribe((data) => {
        this.machine = data as Machine;
        //console.log(data); // see console you get output every 5 sec
      });
  }


  ngOnInit() {
    this.dtOptions = {
      "paging": false,
      "scrollY": "200px",
      "scrollCollapse": true,
    };
  }
}

interface Machine {
  id: string;
  name: string;
  fqdn: string;
  hostIp: string;
  ipAddress: string;
  currentUsername: string;
  status: string;
  createdUtc: Date;
  history: HistoryItem[];
  lastReportedUtc: Date;
  statusUp: string;
  statusMessage: string;
}

interface HistoryItem {
  id: number;
  createdUtc:Date;
  type: string;
  object: string;
}
