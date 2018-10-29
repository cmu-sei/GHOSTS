// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

import { Component, OnInit } from "@angular/core";
import { Router } from "@angular/router";

import { AuthenticationService } from "../shared/auth/auth-service";

@Component({
    moduleId: module.id,
    templateUrl: "login.component.html"
})

export class LoginComponent implements OnInit {
    model: any = {};
    loading = false;
    error = '';

    constructor(
        private router: Router,
        private authenticationService: AuthenticationService) { }

    ngOnInit() {    
        // reset login status
        this.authenticationService.logout();
    }

    login() {
        this.loading = true;
        this.authenticationService.login(this.model.username, this.model.password)
            .subscribe(result => {
                if (result === true) {
                    // login successful
                    this.router.navigate(["/"]);
                } else {
                    // login failed
                    this.error = "Username or password is incorrect";
                    this.loading = false;
                }
            });
    }
}