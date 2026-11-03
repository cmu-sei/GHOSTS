import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AnsweredView, ScenarioType, ScenarioTypeQuestion } from '../../../core/models/builder.model';
import { WorkspaceStore } from '../workspace.store';
import { UpdraftPageComponent } from './updraft-page.component';

const question = (id: string, label: string): ScenarioTypeQuestion => ({ id, label, text: `${label}?`, why: 'w', answer: 'list', path: `/network/${id}` });

const scenarioType: ScenarioType = {
  id: 'small-unit-team-exercise', version: '1', name: 'Small Unit Team Exercise', drafts: [], topology: true,
  stages: [{
    number: 3, title: 'Network and Range', gate: {},
    questions: [question('segments', 'Network Segments'), question('services', 'Services'), question('platforms', 'Platforms')]
  }]
};

const answered: AnsweredView = {
  id: 'services', stage: 3, value: ['Email: mail01', 'Web: www01'], who: 'stated', by: 'dev', via: 'person', time: '2026-11-03T00:00:00Z'
};

async function render() {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: WorkspaceStore, useValue: { scenarioType: signal(scenarioType), name: signal('Cyber Antigen'), answer: (id: string) => id === 'services' ? answered : undefined } }
    ]
  });
  const fixture = TestBed.createComponent(UpdraftPageComponent);
  await fixture.whenStable();
  return fixture.nativeElement as HTMLElement;
}

const section = (root: HTMLElement, title: string) =>
  Array.from(root.querySelectorAll<HTMLElement>('section.decision')).find(s => s.querySelector('h2')?.textContent?.trim() === title)!;

describe('UpdraftPageComponent', () => {
  it('lays out every Updraft decision in order, with the commands behind it', async () => {
    const root = await render();

    expect(Array.from(root.querySelectorAll('section.decision h2')).map(h => h.textContent?.trim())).toEqual([
      'Connection', 'Lab', 'Images', 'Hosts', 'Services and Zones', 'Deploy', 'Players', 'Publish'
    ]);
    const commands = Array.from(section(root, 'Deploy').querySelectorAll('.api code')).map(c => c.textContent?.trim());
    expect(commands).toEqual(['updraft deploy', 'updraft status', 'updraft down']);
  });

  it('puts the network answers in the section they feed, and has no place for segments', async () => {
    const root = await render();
    const services = section(root, 'Services and Zones').querySelector('.answers')!;

    expect(services.textContent).toContain('Services');
    expect(services.textContent).toContain('Email: mail01');
    expect(services.textContent).toContain('Web: www01');
    expect(services.textContent).toContain('Platforms');
    expect(services.textContent).toContain('No answer yet');
    expect(section(root, 'Connection').querySelector('.answers')).toBeNull();
    expect(root.textContent).not.toContain('Network Segments');
  });

  it('shows what the Updraft CLI offers, and every control is read-only', async () => {
    const root = await render();

    expect(root.querySelector('.sample')?.textContent).toContain('Not connected to Updraft');
    expect(section(root, 'Connection').querySelector<HTMLInputElement>('input[type=url]')?.value).toBe('https://git.updraft.clouddev-sei.org');
    expect(section(root, 'Lab').querySelector<HTMLInputElement>('input[name=lab-name]')?.value).toBe('cyber-antigen');
    expect(section(root, 'Images').textContent).toContain('ubuntu2404');
    expect(section(root, 'Images').textContent).toContain('win2025');
    expect(section(root, 'Deploy').textContent).toContain('Slot still in use');
    const controls = Array.from(root.querySelectorAll<HTMLElement>('section.decision :is(button, input, select)'));
    expect(controls.length).toBeGreaterThan(0);
    expect(controls.filter(c => !c.matches(':disabled')).map(c => c.outerHTML)).toEqual([]);
  });
});
