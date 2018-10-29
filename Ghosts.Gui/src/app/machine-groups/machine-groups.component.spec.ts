// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

import { async, ComponentFixture, TestBed } from '@angular/core/testing';

import { MachineGroupsComponent } from './machine-groups.component';

describe('MachineGroupsComponent', () => {
  let component: MachineGroupsComponent;
  let fixture: ComponentFixture<MachineGroupsComponent>;

  beforeEach(async(() => {
    TestBed.configureTestingModule({
      declarations: [ MachineGroupsComponent ]
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(MachineGroupsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
