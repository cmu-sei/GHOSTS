import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { ScenarioType } from '../../../core/models/builder.model';
import { TOUR } from '../help-content';
import { HelpService } from '../help.service';
import { TourComponent, place } from './tour.component';

@Component({ template: '' })
class Page {}

const scenarioType: ScenarioType = { id: 'k', version: '1', name: 'K', drafts: [], stages: [{ number: 0, title: 'S', questions: [] }] };
const view = { width: 1440, height: 900 };
const card = { width: 340, height: 190 };

describe('place', () => {
  it('puts the card below a target with room under it', () => {
    expect(place({ top: 0, left: 900, width: 400, height: 48 }, card, view)).toEqual({ top: 62, left: 900 });
  });

  it('keeps the card inside the window', () => {
    expect(place({ top: 0, left: 1300, width: 100, height: 40 }, card, view).left).toBe(1440 - 340 - 16);
  });

  it('puts the card beside a target as tall as the window', () => {
    expect(place({ top: 48, left: 0, width: 236, height: 830 }, card, view)).toEqual({ top: 48, left: 250 });
    expect(place({ top: 48, left: 1040, width: 400, height: 830 }, card, view)).toEqual({ top: 48, left: 1040 - 14 - 340 });
  });
});

describe('TourComponent', () => {
  let targets: HTMLElement[] = [];

  async function render(url = '/scenarios/4/questions') {
    localStorage.clear();
    TestBed.configureTestingModule({ providers: [provideRouter([{ path: '**', component: Page }])] });
    await TestBed.inject(Router).navigateByUrl(url);
    const help = TestBed.inject(HelpService);
    const fixture = TestBed.createComponent(TourComponent);
    await fixture.whenStable();
    return { fixture, help, root: fixture.nativeElement as HTMLElement };
  }

  async function settle(fixture: ComponentFixture<unknown>) {
    fixture.detectChanges();
    await fixture.whenStable();
  }

  function target(name: string) {
    const el = document.createElement('div');
    el.dataset['tour'] = name;
    document.body.appendChild(el);
    targets.push(el);
  }

  // jsdom lays nothing out, so it has no scrollIntoView.
  beforeEach(() => {
    Element.prototype.scrollIntoView = () => {};
  });

  afterEach(() => {
    targets.forEach(t => t.remove());
    targets = [];
  });

  it('offers the tour once inside a scenario, and stops offering once answered', async () => {
    const { fixture, help, root } = await render();
    expect(root.querySelector('.offer')).toBeNull();

    help.scenario.set({ id: 4, scenarioType, stage: 0, openStage: 0 });
    await settle(fixture);
    expect(root.querySelector('.offer')).not.toBeNull();

    (root.querySelector('.offer .ghost') as HTMLButtonElement).click();
    await settle(fixture);
    expect(root.querySelector('.offer')).toBeNull();
    expect(help.tourOffered()).toBe(true);
  });

  it('steps through its stops with the buttons and the arrow keys, and Esc leaves', async () => {
    TOUR.forEach(s => target(s.target));
    const { fixture, help, root } = await render();
    help.tourStep.set(0);
    await settle(fixture);

    expect(root.querySelector('.title')?.textContent).toBe(TOUR[0].title);
    expect(root.querySelector('.count')?.textContent).toBe(`1 of ${TOUR.length}`);

    (Array.from(root.querySelectorAll('button')).find(b => b.textContent?.trim() === 'Next') as HTMLButtonElement).click();
    await settle(fixture);
    expect(help.tourStep()).toBe(1);

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowLeft' }));
    expect(help.tourStep()).toBe(0);

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    expect(help.tourStep()).toBeNull();
  });

  it('passes over a stop whose target isn’t on the page', async () => {
    TOUR.filter(s => s.target !== 'rail').forEach(s => target(s.target));
    const { fixture, help } = await render();
    help.tourStep.set(0);
    await settle(fixture);

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight' }));
    await settle(fixture);
    await new Promise(r => setTimeout(r, 1100));

    expect(help.tourStep()).toBe(2);
  });

  it('ends on Done at the last stop', async () => {
    TOUR.forEach(s => target(s.target));
    const { fixture, help, root } = await render();
    help.tourStep.set(TOUR.length - 1);
    await settle(fixture);

    const done = Array.from(root.querySelectorAll('button')).find(b => b.textContent?.trim() === 'Done') as HTMLButtonElement;
    done.click();

    expect(help.tourStep()).toBeNull();
  });
});
