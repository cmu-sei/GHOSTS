// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

import { Component, OnInit } from "@angular/core";
import { Headers, RequestOptions, Response } from '@angular/http';
import { HttpClient, HttpParams, HttpHeaders } from "@angular/common/http";
import { ConfigService } from "../shared/services/config.service";
import { Observable } from "rxjs/Rx";
import { AuthenticationService } from '../shared/auth/auth-service';

@Component({
  selector: "app-machine-groups",
  templateUrl: "./machine-groups.component.html",
  styleUrls: ["./machine-groups.component.css"]
})
export class MachineGroupsComponent extends ConfigService implements OnInit {
  public machinegroups: MachineGroup[];
  baseUrl: string = "";

  constructor(private http: HttpClient, private configService: ConfigService, private authenticationService: AuthenticationService) {
    super();
    this.baseUrl = configService.apiUrl;
    let options = new HttpHeaders({ 'Authorization': 'Bearer ' + this.authenticationService.token });
    
    Observable.interval(300000).startWith(0)
      .switchMap(() => http.get(this.baseUrl + "/machinegroups", { headers: options }))
      .catch((error:any) => {
        if(error.status === 401) {
          localStorage.removeItem('currentUser');
          window.location.href = "/login";
        }
        return Observable.empty(null);
      })
      .subscribe((data) => {
        this.machinegroups = data as MachineGroup[];
        //console.log(data);// see console you get output every 5 sec
      });
  }

  ngOnInit() {
  }

  public removeGroup = function(id) {
    if(window.confirm('Are sure you want to remove this group?')){
      let promise = new Promise((resolve, reject) => {
      let options = new HttpHeaders({ 'Authorization': 'Bearer ' + this.authenticationService.token });
      this.http.delete(this.baseUrl + "/machinegroups/" + id, { headers: options }, {params: {id: id}})
        .toPromise()
        .then(
          res => { // Success
            var index = 0;
            for( var i = 0; i < this.machinegroups.length; i++ ) {
              if( this.machinegroups[i].id === id) {
                index = i;
                break;
              }
            }
            this.machinegroups.splice(index, 1);
            resolve();
          },
          msg => { // Error
            console.log("removeGroup error", msg);
            reject(msg);
          }
        );
      }
    )};      
  }
  
  public removeFromGroup = function(machineid, machinegroupid) {
    if(window.confirm('Are sure you want to remove this machine from the group?')){
      let promise = new Promise((resolve, reject) => {
      let options = new HttpHeaders({ 'Authorization': 'Bearer ' + this.authenticationService.token });
      // this.http.delete(this.baseUrl + "/machines/" + id, { headers: options }, {params: {id: id}})
      //   .toPromise()
      //   .then(
      //     res => { // Success
      //       this.machines.splice(id, 1);
      //       resolve();
      //     },
      //     msg => { // Error
      //       console.log("removeFromGroup error", msg);
      //       reject(msg);
      //     }
      //   );
      }
    )};      
  }

  public deleteMachine = function (id, machinegroupid){
    if(window.confirm('Are sure you want to delete this machine?')){
      let promise = new Promise((resolve, reject) => {
      let options = new HttpHeaders({ 'Authorization': 'Bearer ' + this.authenticationService.token });
      this.http.delete(this.baseUrl + "/machines/" + id, { headers: options }, {params: {id: id}})
        .toPromise()
        .then(
          res => { // Success
            var o = this.machinegroups.find(item => item.id === machinegroupid);

            var index = 0;
            for( var i = 0; i < o.machines.length; i++ ) {
              if( o.machines[i].id === id) {
                index = i;
                break;
              }
            }
            
            o.machines.splice(index, 1);
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

  public addGroup = function (){
    if(window.confirm('Are sure you want to add this group?')){
      let promise = new Promise((resolve, reject) => {
      let options = new HttpHeaders({ 'Authorization': 'Bearer ' + this.authenticationService.token });
      
      var name = $("#newGroupName").val().toString();
      var o = new MachineGroup();
      o.name = name;

      this.http.post(this.baseUrl + "/machinegroups",o, { headers: options })
        .toPromise()
        .then(
          res => { // Success
            window.location.href = "/machine-groups";
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

class MachineGroup {
  id: number;
  name: string;
  status: string;
  machines: Machine[];
}

class Machine {
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
  statusMessage: string;
}