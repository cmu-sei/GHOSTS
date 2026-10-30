import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AnsweredView, AnswerValue, KindQuestion } from '../../../../core/models/builder.model';
import { QuestionRowComponent } from './question-row.component';

const audience: KindQuestion = {
  id: 'audienceType', label: 'Civilian or military', text: 'Civilian or military audience?', why: 'Sets vocabulary.', recommended: 'Civilian',
  answer: 'choice', options: ['Civilian', 'Military', 'Mixed'], path: '/engagement/audienceType'
};
const objectives: KindQuestion = {
  id: 'objectives', text: 'What should teams practise?', why: 'IDs.', answer: 'list', path: '/objectives'
};

function answer(overrides: Partial<AnsweredView>): AnsweredView {
  return { id: 'audienceType', stage: 0, value: 'Civilian', who: 'stated', by: 'dev', via: 'person', time: '2026-10-29T12:00:00Z', ...overrides };
}

async function render(question: KindQuestion, given?: AnsweredView): Promise<{ fixture: ComponentFixture<QuestionRowComponent>; saved: AnswerValue[] }> {
  const fixture = TestBed.createComponent(QuestionRowComponent);
  fixture.componentRef.setInput('question', question);
  fixture.componentRef.setInput('answer', given);
  const saved: AnswerValue[] = [];
  fixture.componentInstance.save.subscribe(v => saved.push(v));
  await fixture.whenStable();
  return { fixture, saved };
}

function el<T extends HTMLElement>(fixture: ComponentFixture<unknown>, selector: string): T {
  return (fixture.nativeElement as HTMLElement).querySelector(selector) as T;
}

describe('QuestionRowComponent', () => {
  it('names the question by its short label, with the full question beneath', async () => {
    const { fixture } = await render(audience);

    expect(el(fixture, '.label').textContent?.trim()).toBe('Civilian or military');
    expect(el(fixture, '.question').textContent?.trim()).toBe('Civilian or military audience?');
  });

  it('falls back to the id, spaced out, when a question has no label', async () => {
    const { fixture } = await render(objectives);

    expect(el(fixture, '.label').textContent?.trim()).toBe('Objectives');
  });

  it('offers the recommended answer as one click while editing', async () => {
    const { fixture, saved } = await render(audience);
    el<HTMLButtonElement>(fixture, 'button.edit').click();
    await fixture.whenStable();

    el<HTMLButtonElement>(fixture, 'button.use-recommended').click();

    expect(saved).toEqual(['Civilian']);
  });

  it('shows "open" for a question with no answer', async () => {
    const { fixture } = await render(audience);

    expect(el(fixture, '.status').textContent?.trim()).toBe('open');
  });

  it('shows "proposed by agent" for an agent-proposed answer', async () => {
    const { fixture } = await render(audience, answer({ who: 'proposed', via: 'agent', reason: 'Most likely.' }));

    expect(el(fixture, '.status').textContent?.trim()).toBe('proposed by agent');
    expect(el(fixture, '.reason').textContent).toContain('Most likely.');
  });

  it('shows "answered" for a stated answer, with who gave it', async () => {
    const { fixture } = await render(audience, answer({}));

    expect(el(fixture, '.status').textContent?.trim()).toBe('answered');
    expect(el(fixture, '.meta').textContent).toContain('dev');
  });

  it('saves a choice through the select', async () => {
    const { fixture, saved } = await render(audience, answer({}));
    el<HTMLButtonElement>(fixture, 'button.edit').click();
    await fixture.whenStable();

    const select = el<HTMLSelectElement>(fixture, 'select');
    select.value = 'Mixed';
    select.dispatchEvent(new Event('change'));
    el<HTMLButtonElement>(fixture, 'button.save').click();

    expect(saved).toEqual(['Mixed']);
  });

  it('opens the choice editor on the current answer, even when it is not the first option', async () => {
    const { fixture, saved } = await render(audience, answer({ value: 'Mixed' }));
    el<HTMLButtonElement>(fixture, 'button.edit').click();
    await fixture.whenStable();

    expect(el<HTMLSelectElement>(fixture, 'select').value).toBe('Mixed');
    el<HTMLButtonElement>(fixture, 'button.save').click();
    expect(saved).toEqual(['Mixed']);
  });

  it('can pick the first option when another one is the answer', async () => {
    const { fixture, saved } = await render(audience, answer({ value: 'Mixed' }));
    el<HTMLButtonElement>(fixture, 'button.edit').click();
    await fixture.whenStable();

    const select = el<HTMLSelectElement>(fixture, 'select');
    select.value = 'Civilian';
    select.dispatchEvent(new Event('change'));
    el<HTMLButtonElement>(fixture, 'button.save').click();

    expect(saved).toEqual(['Civilian']);
  });

  it('edits a list one item per line, dropping blank lines', async () => {
    const { fixture, saved } = await render(objectives);
    el<HTMLButtonElement>(fixture, 'button.edit').click();
    await fixture.whenStable();

    const area = el<HTMLTextAreaElement>(fixture, 'textarea');
    area.value = 'Assess under uncertainty\n\n  Decide who leads ';
    area.dispatchEvent(new Event('input'));
    el<HTMLButtonElement>(fixture, 'button.save').click();

    expect(saved).toEqual([['Assess under uncertainty', 'Decide who leads']]);
  });

  it('offers no edit when the stage is not open', async () => {
    const { fixture } = await render(audience);
    fixture.componentRef.setInput('editable', false);
    await fixture.whenStable();

    expect(el(fixture, 'button.edit')).toBeNull();
  });
});
