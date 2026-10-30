import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import type { QuestionCard } from '@scenario-builder/agent-web';
import { AnswerValue } from '../../../../core/models/builder.model';

/**
 * A question the agent asked: the recommended answer first with its reason, the other answers,
 * a box for the designer's own, and "You decide". A choice offers its own options, never the
 * model's wording, so whatever is picked is a value the API accepts.
 */
@Component({
  selector: 'app-question-card',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './question-card.component.html',
  styleUrls: ['./question-card.component.scss']
})
export class QuestionCardComponent {
  readonly card = input.required<QuestionCard>();
  /** True while the agent is answering: an answer now would reach it mid-turn and be lost. */
  readonly disabled = input(false);
  readonly answer = output<AnswerValue>();
  readonly delegate = output<void>();

  protected readonly done = signal(false);
  protected readonly locked = computed(() => this.done() || this.disabled());
  protected readonly own = signal('');

  /** The buttons, recommended first. */
  protected readonly picks = computed(() => {
    const card = this.card();
    const recommended = card.recommended?.answer;
    if (card.answer === 'choice') {
      const options = card.options ?? [];
      return recommended && options.includes(recommended) ? [recommended, ...options.filter(o => o !== recommended)] : options;
    }
    return [...(recommended ? [recommended] : []), ...card.alternatives.filter(a => a !== recommended)];
  });

  protected pick(text: string): void {
    this.submit(this.card().answer === 'list' ? text.split(';') : text);
  }

  protected useOwn(): void {
    this.submit(this.card().answer === 'list' ? this.own().split('\n') : this.own());
  }

  protected youDecide(): void {
    if (this.locked()) {
      return;
    }
    this.done.set(true);
    this.delegate.emit();
  }

  protected typed(event: Event): void {
    this.own.set((event.target as HTMLTextAreaElement).value);
  }

  private submit(raw: string | string[]): void {
    const value: AnswerValue = Array.isArray(raw) ? raw.map(s => s.trim()).filter(s => s.length > 0) : raw.trim();
    if (value.length === 0 || this.locked()) {
      return;
    }
    this.done.set(true);
    this.answer.emit(value);
  }
}
