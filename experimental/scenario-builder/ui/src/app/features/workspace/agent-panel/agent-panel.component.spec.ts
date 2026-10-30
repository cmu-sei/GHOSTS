import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { WorkspaceStore } from '../workspace.store';
import { AgentPanelComponent } from './agent-panel.component';
import { AgentService } from './agent.service';

function stubAgent(busy: boolean, delegated = false) {
  return {
    items: signal([]),
    busy: signal(busy),
    delegated: signal(delegated),
    takeBack: vi.fn(() => Promise.resolve()),
    models: signal([{ id: 'fake', contextWindow: 32768 }]),
    model: signal('fake'),
    error: signal<string | null>(null),
    start: vi.fn(() => Promise.resolve()),
    stop: vi.fn(),
    send: vi.fn(),
    setModel: vi.fn()
  };
}

async function render(busy: boolean, delegated = false) {
  const agent = stubAgent(busy, delegated);
  TestBed.overrideComponent(AgentPanelComponent, { set: { providers: [{ provide: AgentService, useValue: agent }] } });
  TestBed.configureTestingModule({ providers: [{ provide: WorkspaceStore, useValue: { scenarioId: () => 1 } }] });
  const fixture = TestBed.createComponent(AgentPanelComponent);
  await fixture.whenStable();
  return { fixture, agent };
}

const button = (root: HTMLElement, label: string) =>
  Array.from(root.querySelectorAll('button')).find(b => b.textContent?.trim() === label);
const stopButton = (root: HTMLElement) => button(root, 'Stop');

describe('AgentPanelComponent', () => {
  it('offers Stop while the agent is answering, and stops it', async () => {
    const { fixture, agent } = await render(true);

    stopButton(fixture.nativeElement)!.click();

    expect(agent.stop).toHaveBeenCalledTimes(1);
  });

  it('offers no Stop while the agent is idle', async () => {
    const { fixture } = await render(false);

    expect(stopButton(fixture.nativeElement)).toBeUndefined();
  });

  it('shows the handover while the agent decides, and takes control back', async () => {
    const { fixture, agent } = await render(false, true);
    const root = fixture.nativeElement as HTMLElement;

    expect(root.textContent).toContain('The agent is deciding the rest of this scenario.');
    button(root, 'Take back control')!.click();

    expect(agent.takeBack).toHaveBeenCalledTimes(1);
  });

  it('shows no handover strip while the designer decides', async () => {
    const { fixture } = await render(false, false);

    expect(button(fixture.nativeElement, 'Take back control')).toBeUndefined();
  });
});
