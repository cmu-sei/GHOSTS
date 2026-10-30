import { ComponentFixture, TestBed } from '@angular/core/testing';
import type { QuestionCard } from '@scenario-builder/agent-web';
import { AnswerValue } from '../../../../core/models/builder.model';
import { QuestionCardComponent } from './question-card.component';

const textCard: QuestionCard = {
  questionId: 'participants',
  question: 'Who are the participants?',
  recommended: { answer: 'Leaders and senior coordinators', why: 'They make the decisions.' },
  alternatives: ['CERT analysts', 'Ministers'],
  answer: 'text'
};

async function render(card: QuestionCard) {
  const fixture = TestBed.createComponent(QuestionCardComponent);
  fixture.componentRef.setInput('card', card);
  const answers: AnswerValue[] = [];
  let delegated = 0;
  fixture.componentInstance.answer.subscribe(v => answers.push(v));
  fixture.componentInstance.delegate.subscribe(() => delegated++);
  await fixture.whenStable();
  return { fixture, answers, delegated: () => delegated };
}

const buttons = (fixture: ComponentFixture<unknown>) =>
  Array.from((fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('button'));

describe('QuestionCardComponent', () => {
  it('puts the recommended answer first, with its reason', async () => {
    const { fixture } = await render(textCard);

    expect(buttons(fixture)[0].textContent?.trim()).toBe('Leaders and senior coordinators');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('They make the decisions.');
  });

  it('answers with the button picked', async () => {
    const { fixture, answers } = await render(textCard);

    buttons(fixture)[1].click();

    expect(answers).toEqual(['CERT analysts']);
  });

  it('emits delegate on You decide', async () => {
    const { fixture, delegated, answers } = await render(textCard);

    buttons(fixture).find(b => b.textContent?.includes('You decide'))!.click();

    expect(delegated()).toBe(1);
    expect(answers).toEqual([]);
  });

  it('takes no answer while the agent is busy', async () => {
    const { fixture, answers, delegated } = await render(textCard);
    fixture.componentRef.setInput('disabled', true);
    await fixture.whenStable();

    expect(buttons(fixture).every(b => b.disabled)).toBe(true);
    buttons(fixture)[0].click();
    buttons(fixture).find(b => b.textContent?.includes('You decide'))!.click();
    expect(answers).toEqual([]);
    expect(delegated()).toBe(0);
  });

  it('disables itself after an answer', async () => {
    const { fixture } = await render(textCard);

    buttons(fixture)[0].click();
    await fixture.whenStable();

    expect(buttons(fixture).every(b => b.disabled)).toBe(true);
  });

  it('offers a choice question its own options, the recommended one first', async () => {
    const { fixture, answers } = await render({
      questionId: 'audienceType', question: 'Civilian or military?', recommended: { answer: 'Civilian', why: 'Most are.' },
      alternatives: ['Police'], answer: 'choice', options: ['Military', 'Civilian', 'Mixed']
    });

    const labels = buttons(fixture).map(b => b.textContent?.trim());
    expect(labels.slice(0, 3)).toEqual(['Civilian', 'Military', 'Mixed']);
    expect(labels).not.toContain('Police');
    buttons(fixture)[2].click();
    expect(answers).toEqual(['Mixed']);
  });

  it('turns a list suggestion into items, split on semicolons', async () => {
    const { fixture, answers } = await render({
      questionId: 'objectives', question: 'What should teams practise?',
      recommended: { answer: 'Assess under uncertainty; decide who leads', why: 'Core skills.' },
      alternatives: [], answer: 'list'
    });

    buttons(fixture)[0].click();

    expect(answers).toEqual([['Assess under uncertainty', 'decide who leads']]);
  });

  it('sends typed text as the answer, one list item per line', async () => {
    const { fixture, answers } = await render({ ...textCard, answer: 'list', recommended: undefined, alternatives: [] });
    const area = (fixture.nativeElement as HTMLElement).querySelector('textarea')!;

    area.value = 'One\n\nTwo';
    area.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    buttons(fixture).find(b => b.textContent?.includes('Use mine'))!.click();

    expect(answers).toEqual([['One', 'Two']]);
  });
});
