import { ChangeDetectionStrategy, Component, ElementRef, OnInit, afterRenderEffect, inject, signal, viewChild } from '@angular/core';
import type { QuestionCard } from '@scenario-builder/agent-web';
import { AnswerValue } from '../../../core/models/builder.model';
import { WorkspaceStore } from '../workspace.store';
import { AgentService } from './agent.service';
import { ProposalCardComponent } from './proposal-card/proposal-card.component';
import { QuestionCardComponent } from './question-card/question-card.component';

/** The agent's side panel. It never opens empty: the first thing on it is a way to start. */
@Component({
  selector: 'app-agent-panel',
  standalone: true,
  imports: [QuestionCardComponent, ProposalCardComponent],
  providers: [AgentService],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './agent-panel.component.html',
  styleUrls: ['./agent-panel.component.scss']
})
export class AgentPanelComponent implements OnInit {
  protected readonly agent = inject(AgentService);
  private readonly store = inject(WorkspaceStore);

  protected readonly draft = signal('');
  private readonly log = viewChild<ElementRef<HTMLElement>>('log');

  constructor() {
    // The conversation follows its end.
    afterRenderEffect(() => {
      this.agent.items();
      const el = this.log()?.nativeElement;
      if (el) {
        el.scrollTop = el.scrollHeight;
      }
    });
  }

  ngOnInit(): void {
    this.agent.start(this.store.scenarioId());
  }

  protected send(): void {
    this.agent.send(this.draft());
    this.draft.set('');
  }

  protected start(): void {
    this.agent.send("Let's start.");
  }

  protected answered(card: QuestionCard, value: AnswerValue): void {
    this.agent.answerCard(card, value);
  }

  protected typed(event: Event): void {
    this.draft.set((event.target as HTMLTextAreaElement).value);
  }

  protected picked(event: Event): void {
    this.agent.setModel((event.target as HTMLSelectElement).value);
  }

  protected enter(event: Event): void {
    const key = event as KeyboardEvent;
    if (!key.shiftKey) {
      key.preventDefault();
      this.send();
    }
  }
}
