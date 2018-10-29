// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

import { Component, OnInit } from '@angular/core';
import { Observable } from 'rxjs/Observable';
import { AuthenticationService } from "../shared/auth/auth-service";

@Component({
  selector: 'app-navmenu',
  templateUrl: './navmenu.component.html',
  styleUrls: ['./navmenu.component.css']
})
export class NavmenuComponent implements OnInit {
  isLoggedIn$: Observable<boolean>;

  constructor(private authenticationService: AuthenticationService) { 
  }

  ngOnInit() {
    this.isLoggedIn$ = Observable.of(this.authenticationService.isLoggedIn());
   }
}
