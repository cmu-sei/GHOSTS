import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import type { ModelInfo, QuestionCard, WebAgent, WebAgentEvent } from '@scenario-builder/agent-web';
import { BuilderApiService, errorMessage } from '../../../core/services/builder-api.service';
import { AnswerValue, Progress, ProposalView } from '../../../core/models/builder.model';
import { WorkspaceStore } from '../workspace.store';
import { delegatesAll, finished, madeProgress } from './delegation';

export type AgentItem =
  | { kind: 'user'; text: string }
  | { kind: 'assistant'; id: string; text: string }
  | { kind: 'tool'; name: string; summary: string; error?: string }
  | { kind: 'question'; card: QuestionCard }
  | { kind: 'proposal'; proposal: ProposalView }
  | { kind: 'error'; text: string };

/**
 * The agent beside the workspace: the browser bundle's WebAgent for this scenario, and its
 * conversation as items. A person's answer on a card is recorded here, as the person, before the
 * agent hears of it; so is applying a proposal. The agent itself only writes through its tools.
 */
@Injectable()
export class AgentService {
  private readonly api = inject(BuilderApiService);
  private readonly store = inject(WorkspaceStore);

  readonly items = signal<AgentItem[]>([]);
  readonly busy = signal(false);
  readonly models = signal<ModelInfo[]>([]);
  readonly model = signal('');
  /** Why the agent can't start or answer. The pages still work by hand. */
  readonly error = signal<string | null>(null);
  /** The owner handed every remaining decision over: the agent runs to the end on its own. */
  readonly delegated = computed(() => this.store.progress()?.delegated ?? false);

  private agent: WebAgent | null = null;
  /** The scenario as the current reply found it, to tell whether the reply moved it on. */
  private before: Progress | null = null;

  constructor() {
    const approvals = this.store.approvals.subscribe(stage => {
      if (this.agent && !this.busy()) {
        this.run(() => this.agent!.approved(stage));
      }
    });
    inject(DestroyRef).onDestroy(() => {
      approvals.unsubscribe();
      this.agent?.abort();
    });
  }

  async start(scenarioId: number): Promise<void> {
    try {
      const { createWebAgent } = await import('@scenario-builder/agent-web');
      this.attach(await createWebAgent({ apiUrl: location.origin, scenarioId }));
    } catch (e) {
      this.error.set(`The agent can't start: ${errorMessage(e)}`);
    }
    // Proposals outlive a page load; show the ones still waiting.
    const pending = await firstValueFrom(this.api.getProposals(scenarioId, 'pending')).catch(() => []);
    this.items.update(items => [...items, ...pending.map(proposal => ({ kind: 'proposal' as const, proposal }))]);
    await this.resumeIfDelegated();
  }

  /**
   * The agent runs in this tab, so a reload or a closed tab ends its reply. A scenario still handed over
   * when the panel opens is picked back up: the designer said "you decide" once, and that still holds.
   */
  async resumeIfDelegated(): Promise<void> {
    const now = await this.reload().catch(() => null);
    if (!this.agent || !now?.delegated || finished(now)) {
      return;
    }
    this.push({ kind: 'tool', name: 'agent', summary: 'Picking up where it left off' });
    this.run(() => this.agent!.send('[continue]'));
  }

  /** Stops the agent's current reply. */
  stop(): void {
    this.agent?.abort();
  }

  /** Uses this agent for the panel; tests pass a stand-in. */
  attach(agent: WebAgent): void {
    this.agent = agent;
    agent.subscribe(event => this.on(event));
    this.models.set(agent.models);
    this.model.set(agent.model.id);
    this.error.set(null);
  }

  /** As the owner: take the decisions back. The agent stops carrying on after its current step. */
  async takeBack(): Promise<void> {
    try {
      await firstValueFrom(this.api.setDelegation(this.store.scenarioId(), false));
      await this.reload();
    } catch (e) {
      this.push({ kind: 'error', text: errorMessage(e) });
    }
  }

  setModel(id: string): void {
    this.agent?.setModel(id);
    this.model.set(id);
  }

  /**
   * What the designer typed. "You decide" and its kin first hand the decisions over, as the person, so the
   * agent can approve gates and run to the end: the model can't grant itself that.
   */
  async send(text: string): Promise<void> {
    const trimmed = text.trim();
    if (!trimmed || !this.agent) {
      return;
    }
    this.items.update(items => [...items, { kind: 'user', text: trimmed }]);
    if (delegatesAll(trimmed) && !this.delegated()) {
      try {
        await firstValueFrom(this.api.setDelegation(this.store.scenarioId(), true));
        await this.reload();
      } catch (e) {
        this.push({ kind: 'error', text: `Couldn't hand the decisions over: ${errorMessage(e)}` });
        return;
      }
    }
    this.run(() => this.agent!.send(trimmed));
  }

  async answerCard(card: QuestionCard, value: AnswerValue): Promise<void> {
    if (this.busy()) {
      this.push({ kind: 'error', text: 'The agent is still answering. Wait for it, then answer the card.' });
      return;
    }
    try {
      await firstValueFrom(this.api.answer(this.store.scenarioId(), card.questionId, value));
    } catch (e) {
      this.push({ kind: 'error', text: `Couldn't save that answer: ${errorMessage(e)} Answer it on the Questions page.` });
      return;
    }
    this.store.refresh();
    this.run(() => this.agent!.answered(card.questionId, value));
  }

  delegateCard(card: QuestionCard): void {
    if (this.busy()) {
      this.push({ kind: 'error', text: 'The agent is still answering. Wait for it, then press You decide.' });
      return;
    }
    this.run(() => this.agent!.delegated(card.questionId));
  }

  async decide(proposal: ProposalView, action: 'apply' | 'discard'): Promise<void> {
    const id = this.store.scenarioId();
    try {
      const decided = await firstValueFrom(action === 'apply' ? this.api.applyProposal(id, proposal.id) : this.api.discardProposal(id, proposal.id));
      this.items.update(items => items.map(i => (i.kind === 'proposal' && i.proposal.id === proposal.id ? { ...i, proposal: decided } : i)));
    } catch (e) {
      this.push({ kind: 'error', text: errorMessage(e) });
      return;
    }
    this.store.refresh();
    if (this.agent && !this.busy()) {
      this.run(() => this.agent!.send(`[${action === 'apply' ? 'applied' : 'discarded'} P-${proposal.id}]`));
    }
  }

  private run(call: () => Promise<void>): void {
    if (!this.agent) {
      return;
    }
    call().catch(e => this.push({ kind: 'error', text: errorMessage(e) }));
  }

  private on(event: WebAgentEvent): void {
    switch (event.type) {
      case 'busy':
        this.busy.set(event.busy);
        if (event.busy) {
          this.before = this.store.progress();
        } else {
          void this.afterReply();
        }
        return;
      case 'text':
        this.items.update(items => {
          const at = items.findIndex(i => i.kind === 'assistant' && i.id === event.id);
          const item: AgentItem = { kind: 'assistant', id: event.id, text: event.text };
          return at < 0 ? [...items, item] : items.map((i, n) => (n === at ? item : i));
        });
        return;
      case 'tool':
        this.push({ kind: 'tool', name: event.name, summary: event.summary, error: event.error });
        this.store.refresh();
        return;
      case 'question':
        this.push({ kind: 'question', card: event.card });
        return;
      case 'proposal':
        this.push({ kind: 'proposal', proposal: event.proposal as ProposalView });
        return;
      case 'error':
        // A delegated run carries on by itself after the call limit; asking the designer to send a message would mislead.
        if (this.delegated() && /stopped after \d+ model calls/.test(event.message)) {
          this.push({ kind: 'tool', name: 'agent', summary: 'Carrying on' });
          return;
        }
        this.push({ kind: 'error', text: event.message });
        return;
    }
  }

  /** While delegated, carry on after each reply that moved the scenario on; stop at the end or when one didn't. */
  private async afterReply(): Promise<void> {
    const now = await this.reload().catch(() => null);
    if (!now?.delegated || !this.agent || finished(now)) {
      return;
    }
    if (this.before && !madeProgress(this.before, now)) {
      this.push({ kind: 'error', text: 'The agent stopped making progress. Tell it what to do next, or take back control.' });
      return;
    }
    this.run(() => this.agent!.send('[continue]'));
  }

  private async reload(): Promise<Progress> {
    const progress = await firstValueFrom(this.api.getProgress(this.store.scenarioId()));
    this.store.apply(progress);
    return progress;
  }

  private push(item: AgentItem): void {
    this.items.update(items => [...items, item]);
  }
}
