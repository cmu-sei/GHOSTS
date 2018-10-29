// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

import { Component, OnInit } from "@angular/core";
import { Headers, RequestOptions, Response } from '@angular/http';
import { HttpClient, HttpParams, HttpHeaders } from "@angular/common/http";
import { ConfigService } from "../shared/services/config.service";
import { Observable } from "rxjs/Rx";
import { AuthenticationService } from '../shared/auth/auth-service';

@Component({
  selector: "app-machines",
  templateUrl: "./machines.component.html",
  styleUrls: ["./machines.component.css"]
})

export class MachinesComponent extends ConfigService implements OnInit {
  public machines: Machine[];
  baseUrl: string = "";
  
  constructor(private http: HttpClient, private configService: ConfigService, private authenticationService: AuthenticationService) {
    super();
    this.baseUrl = configService.apiUrl;
    this.load();
  }

  ngOnInit() {
  }

  load() {
    let options = new HttpHeaders({ 'Authorization': 'Bearer ' + this.authenticationService.token });
    Observable.interval(300000).startWith(0)
      .switchMap(() => this.http.get(this.baseUrl + "/machines", { headers: options }))
      .catch((error:any) => {
        if(error.status === 401) {
          localStorage.removeItem('currentUser');
          window.location.href = "/login";
        }
        return Observable.empty(null);
      })
      .subscribe((data) => {
        this.machines = data as Machine[];
        //console.log(new Date(), data);// see console you get output every 5 sec
      });
  }

  public deleteMachine = function (id){
    if(window.confirm('Are sure you want to delete this machine?')){
      let promise = new Promise((resolve, reject) => {
      let options = new HttpHeaders({ 'Authorization': 'Bearer ' + this.authenticationService.token });
      this.http.delete(this.baseUrl + "/machines/" + id, { headers: options }, {params: {id: id}})
        .toPromise()
        .then(
          res => { // Success
            this.machines.splice(id, 1);
            resolve();
          },
          msg => { // Error
            console.log("delete error", msg);
            reject(msg);
          }
        );
      }
    )};      
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
  lastReportedUtc: Date;
  statusUp: string;
}