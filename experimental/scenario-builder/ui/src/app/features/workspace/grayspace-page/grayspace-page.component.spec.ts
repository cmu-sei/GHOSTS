import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AnsweredView, Kind, KindQuestion } from '../../../core/models/builder.model';
import { WorkspaceStore } from '../workspace.store';
import { GraySpacePageComponent } from './grayspace-page.component';

const question = (id: string, label: string): KindQuestion => ({ id, label, text: `${label}?`, why: 'w', answer: 'list', path: `/network/${id}` });

const kind: Kind = {
  id: 'small-unit-team-exercise', version: '1', name: 'Small Unit Team Exercise', drafts: [], topology: true,
  stages: [{ number: 0, title: 'Network and Range', gate: {}, questions: [question('segments', 'Network Segments'), question('connectivity', 'Connectivity')] }]
};

const answered: AnsweredView = {
  id: 'segments', stage: 0, value: ['IT Network: Corporate services', 'DMZ: Jump hosts'], who: 'stated', by: 'dev', via: 'person', time: '2026-11-02T00:00:00Z'
};

async function render() {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: WorkspaceStore, useValue: { kind: signal(kind), name: signal('Cyber Antigen'), answer: (id: string) => id === 'segments' ? answered : undefined } }
    ]
  });
  const fixture = TestBed.createComponent(GraySpacePageComponent);
  await fixture.whenStable();
  return fixture.nativeElement as HTMLElement;
}

const section = (root: HTMLElement, title: string) =>
  Array.from(root.querySelectorAll<HTMLElement>('section.decision')).find(s => s.querySelector('h2')?.textContent?.trim() === title)!;

describe('GraySpacePageComponent', () => {
  it('lays out every GraySpace decision in order', async () => {
    const root = await render();

    expect(Array.from(root.querySelectorAll('section.decision h2')).map(h => h.textContent?.trim())).toEqual([
      'Connection', 'Base', 'Enclave', 'Sites and Segments', 'Hosts', 'Role Settings', 'DNS and Certificates', 'Network Conditions', 'Deploy'
    ]);
  });

  it('puts the network answers in the section they feed', async () => {
    const root = await render();
    const sites = section(root, 'Sites and Segments').querySelector('.answers')!;

    expect(sites.textContent).toContain('Network Segments');
    expect(sites.textContent).toContain('IT Network: Corporate services');
    expect(sites.textContent).toContain('DMZ: Jump hosts');
    expect(sites.textContent).toContain('Connectivity');
    expect(sites.textContent).toContain('No answer yet');
    expect(section(root, 'Connection').querySelector('.answers')).toBeNull();
  });

  it('shows GraySpace sample choices, and every control is read-only', async () => {
    const root = await render();

    expect(root.querySelector('.sample')?.textContent).toContain('Not connected to GraySpace');
    expect(section(root, 'Sites and Segments').textContent).toContain('lum-chi');
    expect(section(root, 'Hosts').textContent).toContain('ubuntu-24.04-vm');
    expect(section(root, 'Role Settings').textContent).toContain('admin_password');
    const controls = Array.from(root.querySelectorAll<HTMLElement>('section.decision :is(button, input, select)'));
    expect(controls.length).toBeGreaterThan(0);
    expect(controls.filter(c => !c.matches(':disabled')).map(c => c.outerHTML)).toEqual([]);
  });
});
