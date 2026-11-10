import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { ScenarioType } from '../../core/models/builder.model';
import { HelpService, pageOf } from './help.service';

@Component({ template: '' })
class Page {}

const scenarioType: ScenarioType = { id: 'k', version: '1', name: 'K', drafts: [], stages: [{ number: 0, title: 'S', questions: [] }] };

function setup() {
  TestBed.configureTestingModule({ providers: [provideRouter([{ path: '**', component: Page }])] });
  return { help: TestBed.inject(HelpService), router: TestBed.inject(Router) };
}

describe('pageOf', () => {
  it('names the page a URL shows', () => {
    expect(pageOf('/')).toBe('scenarios');
    expect(pageOf('/scenarios/3')).toBe('questions');
    expect(pageOf('/scenarios/3/context')).toBe('context');
    expect(pageOf('/scenarios/3/log?x=1')).toBe('log');
    expect(pageOf('/scenarios/3/topology')).toBe('topology');
    expect(pageOf('/scenarios/3/topology/grayspace')).toBe('grayspace');
    expect(pageOf('/scenarios/3/topology/updraft')).toBe('updraft');
    expect(pageOf('/scenarios/3/json')).toBe('finish');
    expect(pageOf('/admin/scenario-types')).toBe('admin');
  });
});

describe('HelpService', () => {
  beforeEach(() => localStorage.clear());

  it('opens the Guide on a section, and toggles it shut', () => {
    const { help } = setup();

    help.show('glossary', 'inject');
    expect([help.open(), help.section(), help.focus()]).toEqual([true, 'glossary', 'inject']);

    help.toggle();
    expect(help.open()).toBe(false);
  });

  it('remembers the welcome was dismissed, and brings it back on the Scenarios page', async () => {
    const { help, router } = setup();
    help.dismissWelcome();
    expect(localStorage.getItem('sb.help.welcomeDismissed')).toBe('1');

    await router.navigateByUrl('/scenarios/3/questions');
    help.showWelcome();
    await new Promise(r => setTimeout(r));

    expect(help.welcomeDismissed()).toBe(false);
    expect(localStorage.getItem('sb.help.welcomeDismissed')).toBeNull();
    expect(router.url).toBe('/');
  });

  it('starts the tour on the Questions page of the scenario, and marks the offer answered', async () => {
    const { help, router } = setup();
    await router.navigateByUrl('/scenarios/3/context');
    help.scenario.set({ id: 3, scenarioType, stage: 0, openStage: 0 });
    help.show();

    await help.startTour();

    expect(router.url).toBe('/scenarios/3/questions');
    expect(help.tourStep()).toBe(0);
    expect(help.open()).toBe(false);
    expect(help.tourOffered()).toBe(true);
    expect(localStorage.getItem('sb.help.tourOffered')).toBe('1');
  });

  it('follows the page as the designer moves', async () => {
    const { help, router } = setup();

    await router.navigateByUrl('/scenarios/3/finish');

    expect(help.page()).toBe('finish');
  });
});
