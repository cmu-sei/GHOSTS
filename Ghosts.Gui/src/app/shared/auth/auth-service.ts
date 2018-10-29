// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Response } from '@angular/http';
import { Observable } from 'rxjs';
import { ConfigService } from "../../shared/services/config.service";
import 'rxjs/add/operator/map'
 
@Injectable()
export class AuthenticationService extends ConfigService {
    public token: string;
    baseUrl: string = "";
    
    constructor(private http: HttpClient, private configService: ConfigService) {
        super();
        this.baseUrl = this.configService.apiUrl;
        var currentUser = JSON.parse(localStorage.getItem('currentUser'));
        this.token = currentUser && currentUser.token;
    }

    login(username: string, password: string): Observable<boolean> {
        return this.http.post(this.baseUrl + "/auth/login", { UserName: username, Password: password }, {
            headers: new HttpHeaders().set("Content-Type", "application/json"),
          })
            .map((response: Token) => {

                let token = response.auth_token;

                if (token) {
                    // set token property
                    this.token = token;
 
                    // store username and jwt token in local storage to keep user logged in between page refreshes
                    localStorage.setItem('currentUser', JSON.stringify({ userName: username, token: token }));
 
                    window.location.href = "/home";

                    // return true to indicate successful login
                    return true;
                } else {
                    // return false to indicate failed login
                    return false;
                }
            });
    }
 
    logout(): void {
        // clear token remove user from local storage to log user out
        this.token = null;
        localStorage.removeItem('currentUser');
    }

    isLoggedIn(): boolean {
        var currentUser = JSON.parse(localStorage.getItem('currentUser'));
        var r = currentUser != null && currentUser.token != null;
        //console.log("isloggedin", r);
        return r;
    }
}

interface Token {
    id: string;
    auth_token: string;
    expires_in: number;
} 