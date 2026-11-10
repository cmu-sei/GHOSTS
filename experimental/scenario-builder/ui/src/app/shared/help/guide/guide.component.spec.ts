import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { ScenarioType } from '../../../core/models/builder.model';
import { HelpService } from '../help.service';
import { GuideComponent } from './guide.component';

@Component({ template: '' })
class Page {}

const q = (id: string) => ({ id, text: `${id}?`, why: 'w', answer: 'text' as const, path: `/${id}` });
const scenarioType: ScenarioType = {
  id: 'k', version: '1', name: 'K', drafts: [],
  stages: [
    { number: 0, title: 'Engagement Context', questions: [q('a')] },
    { number: 1, title: 'Premise, Ground Truth and Escalation Ceiling', questions: [q('b')], purpose: 'Settles what really happens.' },
    { number: 2, title: 'Names', questions: [q('c')] }
  ]
};

async function render(url: string) {
  TestBed.configureTestingModule({ providers: [provideRouter([{ path: '**', component: Page }])] });
  await TestBed.inject(Router).navigateByUrl(url);
  const help = TestBed.inject(HelpService);
  const fixture = TestBed.createComponent(GuideComponent);
  await fixture.whenStable();
  return { fixture, help, root: fixture.nativeElement as HTMLElement };
}

async function settle(fixture: ComponentFixture<unknown>) {
  fixture.detectChanges();
  await fixture.whenStable();
}

const text = (root: HTMLElement, selector: string) => root.querySelector(selector)?.textContent?.replace(/\s+/g, ' ').trim();

describe('GuideComponent', () => {
  it('is closed until opened', async () => {
    const { root } = await render('/');

    expect(root.querySelector('.guide')).toBeNull();
  });

  it('opens on what this page is for, what to do, and what done looks like', async () => {
    const { fixture, help, root } = await render('/');
    help.show();
    await settle(fixture);

    expect(text(root, '.display')).toBe('Scenarios');
    expect(text(root, '.eyebrow:not(#guide-title)')).toBe('Start Here');
    expect(root.querySelectorAll('.steps li').length).toBe(3);
    expect(text(root, '.done')).toContain('Context page');
    expect(root.querySelector('.next')).toBeNull();
  });

  it('on Questions, adds the stage shown, its purpose and where it stands, and points to the next page', async () => {
    const { fixture, help, root } = await render('/scenarios/4/questions');
    help.scenario.set({ id: 4, scenarioType, stage: 1, openStage: 1 });
    help.show();
    await settle(fixture);

    expect(text(root, '.eyebrow:not(#guide-title)')).toBe('Step 2 of 5 · Stage 1');
    expect(text(root, '.stage-title')).toBe('Premise, Ground Truth and Escalation Ceiling');
    expect(root.querySelectorAll('.stage-title .sb-term').length).toBe(2);
    expect(text(root, '.stage-purpose')).toBe('Settles what really happens.');
    expect(text(root, '.stage-state')).toBe('Open now');
    expect(root.querySelector('.next')?.getAttribute('href')).toBe('/scenarios/4/summary');
  });

  it('says when a stage shown is still locked', async () => {
    const { fixture, help, root } = await render('/scenarios/4/questions');
    help.scenario.set({ id: 4, scenarioType, stage: 2, openStage: 1 });
    help.show();
    await settle(fixture);

    expect(text(root, '.stage-state')).toBe('Locked');
    expect(text(root, '.stage-line')).toBe('It opens once stage 1 is answered.');
  });

  it('maps the journey with the designer’s place, leaving out Topology for a type without one', async () => {
    const { fixture, help, root } = await render('/scenarios/4/log');
    help.scenario.set({ id: 4, scenarioType, stage: 0, openStage: 0 });
    help.show('how');
    await settle(fixture);

    const steps = Array.from(root.querySelectorAll('.journey > li .j-title')).map(e => e.textContent?.trim());
    expect(steps).toEqual(['Context', 'Questions', 'Summary', 'Decision Log', 'Finish']);
    expect(text(root, '.journey > li.here .j-title')).toBe('Decision Log');
    expect(root.querySelectorAll('.j-stages li').length).toBe(3);
  });

  it('searches the glossary, the concepts and the pages at once', async () => {
    const { fixture, help, root } = await render('/');
    help.show();
    help.query.set('ground truth');
    await settle(fixture);

    expect(root.querySelector('[role="tablist"]')).toBeNull();
    expect(Array.from(root.querySelectorAll('.results dt')).map(d => d.textContent)).toContain('Ground Truth');
    expect(text(root, '.results > .eyebrow')).toMatch(/^\d+ Results? for “ground truth”$/);

    help.query.set('zzzz');
    await settle(fixture);
    expect(text(root, '.nothing')).toContain('Nothing matches “zzzz”');
  });

  it('opens with ? from anywhere but a text field, and closes with Esc', async () => {
    const { fixture, help } = await render('/');
    const press = (key: string, target: EventTarget = document.body) =>
      target.dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true }));

    press('?');
    expect(help.open()).toBe(true);

    press('Escape');
    expect(help.open()).toBe(false);

    const input = document.createElement('input');
    document.body.appendChild(input);
    press('?', input);
    expect(help.open()).toBe(false);
    input.remove();
    fixture.destroy();
  });

  it('offers the tour only inside a scenario', async () => {
    const { fixture, help, root } = await render('/');
    help.show();
    await settle(fixture);
    const labels = () => Array.from(root.querySelectorAll('.foot button')).map(b => b.textContent?.trim());

    expect(labels()).toEqual(['Show the welcome again']);

    help.scenario.set({ id: 4, scenarioType, stage: 0, openStage: 0 });
    await settle(fixture);
    expect(labels()).toEqual(['Take the tour', 'Show the welcome again']);
  });
});
