import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { AnsweredView, AnswerValue, KindQuestion } from '../../../../core/models/builder.model';
import { spaced } from '../../../../shared/labels/question-label';

/**
 * One design question as a ledger row: its short label and full text, its answer, and who gave it.
 * The mark in the margin is the provenance: filled cobalt for stated, violet for proposed by the
 * agent, a hollow ring for open. Editing opens in place, with the recommended answer one click away.
 */
@Component({
  selector: 'app-question-row',
  standalone: true,
  imports: [DatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './question-row.component.html',
  styleUrls: ['./question-row.component.scss']
})
export class QuestionRowComponent {
  readonly question = input.required<KindQuestion>();
  readonly answer = input<AnsweredView | undefined>();
  /** False for a stage that isn't open: its questions can't be answered yet. */
  readonly editable = input(true);
  readonly save = output<AnswerValue>();

  protected readonly editing = signal(false);
  protected readonly draft = signal('');

  protected readonly status = computed(() => {
    const a = this.answer();
    if (!a) {
      return 'open';
    }
    return a.who === 'proposed' && a.via === 'agent' ? 'proposed by agent' : 'answered';
  });

  protected readonly label = computed(() => this.question().label ?? spaced(this.question().id));

  protected readonly shown = computed(() => {
    const value = this.answer()?.value;
    return Array.isArray(value) ? value : value === undefined ? [] : [value];
  });

  protected edit(): void {
    const value = this.answer()?.value;
    const options = this.question().options ?? [];
    this.draft.set(Array.isArray(value) ? value.join('\n') : value ?? (this.question().answer === 'choice' ? options[0] ?? '' : ''));
    this.editing.set(true);
  }

  protected submit(): void {
    this.emit(this.question().answer === 'list' ? this.draft().split('\n') : this.draft());
  }

  protected useRecommended(): void {
    const recommended = this.question().recommended ?? '';
    this.emit(this.question().answer === 'list' ? recommended.split(';') : recommended);
  }

  protected cancel(): void {
    this.editing.set(false);
  }

  protected typed(event: Event): void {
    this.draft.set((event.target as HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement).value);
  }

  private emit(raw: string | string[]): void {
    const value: AnswerValue = Array.isArray(raw) ? raw.map(s => s.trim()).filter(s => s.length > 0) : raw.trim();
    if (value.length === 0) {
      return;
    }
    this.save.emit(value);
    this.editing.set(false);
  }
}
