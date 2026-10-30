import { TestBed } from '@angular/core/testing';
import { Subject, of } from 'rxjs';
import type { WebAgent, WebAgentEvent } from '@scenario-builder/agent-web';
import { BuilderApiService } from '../../../core/services/builder-api.service';
import { Progress } from '../../../core/models/builder.model';
import { WorkspaceStore } from '../workspace.store';
import { AgentService } from './agent.service';

const progress = (answeredCount: number, delegated: boolean, overrides: Partial<Progress> = {}): Progress => ({
  kindId: 'k', stage: 0, stageTitle: 'S', lastStage: false,
  gate: { passes: false, approved: false, findings: [] }, open: [], draftsDue: [], delegated,
  answered: Array.from({ length: answeredCount }, (_, i) => ({ id: `q${i}`, stage: 0, value: 'x', who: 'proposed' as const, by: 'dev', via: 'agent' as const, time: 't' })),
  ...overrides
});

function setup(initial: Progress) {
  const listeners: ((e: WebAgentEvent) => void)[] = [];
  const agent = {
    models: [{ id: 'm', contextWindow: 1 }], model: { id: 'm', contextWindow: 1 },
    send: vi.fn(() => Promise.resolve()), answered: vi.fn(), delegated: vi.fn(), approved: vi.fn(), abort: vi.fn(), setModel: vi.fn(),
    subscribe: (l: (e: WebAgentEvent) => void) => { listeners.push(l); return () => {}; }
  } as unknown as WebAgent;
  let current = initial;
  const api = {
    setDelegation: vi.fn((_id: number, on: boolean) => { current = { ...current, delegated: on }; return of({ delegated: on }); }),
    getProgress: vi.fn(() => of(current)),
    getProposals: vi.fn(() => of([]))
  };
  TestBed.configureTestingModule({
    providers: [
      AgentService,
      WorkspaceStore,
      { provide: BuilderApiService, useValue: api }
    ]
  });
  const store = TestBed.inject(WorkspaceStore);
  store.scenarioId.set(1);
  store.apply(initial);
  const service = TestBed.inject(AgentService);
  service.attach(agent);
  const emit = (e: WebAgentEvent) => listeners.forEach(l => l(e));
  const settle = () => new Promise(r => setTimeout(r, 0));
  return { service, agent, api, store, emit, settle, setProgress: (p: Progress) => (current = p) };
}

describe('AgentService delegation', () => {
  it('hands the decisions over as the person before the agent hears "you decide"', async () => {
    const { service, agent, api, store } = setup(progress(0, false));

    await service.send('you decide');

    expect(api.setDelegation).toHaveBeenCalledWith(1, true);
    expect(store.progress()?.delegated).toBe(true);
    expect(agent.send).toHaveBeenCalledWith('you decide');
  });

  it('leaves delegation alone for an ordinary message', async () => {
    const { service, api } = setup(progress(0, false));

    await service.send('the audience is civilian');

    expect(api.setDelegation).not.toHaveBeenCalled();
  });

  it('carries on by itself after a reply that made progress', async () => {
    const { agent, emit, settle, setProgress } = setup(progress(2, true));

    emit({ type: 'busy', busy: true });
    setProgress(progress(5, true));
    emit({ type: 'busy', busy: false });
    await settle();

    expect(agent.send).toHaveBeenCalledWith('[continue]');
  });

  it('stops and says so after a reply that made no progress', async () => {
    const { service, agent, emit, settle } = setup(progress(2, true));

    emit({ type: 'busy', busy: true });
    emit({ type: 'busy', busy: false });
    await settle();

    expect(agent.send).not.toHaveBeenCalled();
    expect(service.items().at(-1)).toMatchObject({ kind: 'error', text: expect.stringContaining('stopped making progress') });
  });

  it('stops once the scenario is finished', async () => {
    const { agent, emit, settle, setProgress } = setup(progress(2, true));

    emit({ type: 'busy', busy: true });
    setProgress(progress(3, true, { lastStage: true, gate: { passes: true, approved: true, findings: [] } }));
    emit({ type: 'busy', busy: false });
    await settle();

    expect(agent.send).not.toHaveBeenCalled();
  });

  it('takes the decisions back on request, and then stops carrying on', async () => {
    const { service, agent, api, emit, settle, setProgress } = setup(progress(2, true));

    await service.takeBack();
    emit({ type: 'busy', busy: true });
    setProgress(progress(4, false));
    emit({ type: 'busy', busy: false });
    await settle();

    expect(api.setDelegation).toHaveBeenCalledWith(1, false);
    expect(agent.send).not.toHaveBeenCalled();
  });

  it('picks a delegated scenario back up when the panel opens, after a reload or a closed tab', async () => {
    const { service, agent } = setup(progress(4, true));

    await service.resumeIfDelegated();

    expect(agent.send).toHaveBeenCalledWith('[continue]');
    expect(service.items().at(-1)).toMatchObject({ kind: 'tool', summary: expect.stringContaining('Picking up') });
  });

  it('leaves an undelegated scenario alone when the panel opens', async () => {
    const plain = setup(progress(4, false));
    await plain.service.resumeIfDelegated();
    expect(plain.agent.send).not.toHaveBeenCalled();
  });

  it('leaves a finished delegated scenario alone when the panel opens', async () => {
    const done = setup(progress(4, true, { lastStage: true, gate: { passes: true, approved: true, findings: [] } }));
    await done.service.resumeIfDelegated();
    expect(done.agent.send).not.toHaveBeenCalled();
  });

  it('says it is carrying on, not "send a message", when a delegated reply hits the call limit', async () => {
    const { service, emit } = setup(progress(4, true));

    emit({ type: 'error', message: 'The agent stopped after 12 model calls without asking anything. Send a message to carry on.' });

    expect(service.items().at(-1)).toMatchObject({ kind: 'tool', summary: 'Carrying on' });
  });
});
