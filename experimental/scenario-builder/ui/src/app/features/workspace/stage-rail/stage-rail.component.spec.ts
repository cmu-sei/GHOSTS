import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AnsweredView, Kind, Progress } from '../../../core/models/builder.model';
import { StageRailComponent } from './stage-rail.component';

const q = (id: string) => ({ id, text: `${id}?`, why: 'w', answer: 'text' as const, path: `/${id}` });
const kind: Kind = {
  id: 'k', version: '1', name: 'K', drafts: [],
  stages: [
    { number: 0, title: 'Engagement context', gate: { approval: 'Confirm' }, questions: [q('a'), q('b')] },
    { number: 1, title: 'Objectives', gate: {}, questions: [q('c'), q('d'), q('e')] },
    { number: 2, title: 'Premise', gate: {}, questions: [q('f')] }
  ]
};
const answered = (id: string, stage: number): AnsweredView =>
  ({ id, stage, value: 'x', who: 'stated', by: 'dev', via: 'person', time: '2026-10-30T09:00:00Z' });

function progress(overrides: Partial<Progress>): Progress {
  return {
    kindId: 'k', stage: 1, stageTitle: 'Objectives', lastStage: false,
    gate: { passes: false, approved: false, findings: [] },
    open: [], answered: [answered('a', 0), answered('b', 0), answered('c', 1)], draftsDue: [], delegated: false, ...overrides
  };
}

async function render(p: Progress, selected = 1): Promise<ComponentFixture<StageRailComponent>> {
  const fixture = TestBed.createComponent(StageRailComponent);
  fixture.componentRef.setInput('kind', kind);
  fixture.componentRef.setInput('progress', p);
  fixture.componentRef.setInput('selected', selected);
  await fixture.whenStable();
  return fixture;
}

const stages = (f: ComponentFixture<unknown>) =>
  Array.from((f.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('button.stage'));

describe('StageRailComponent', () => {
  it('shows each stage with its answered count and its state', async () => {
    const rows = stages(await render(progress({})));

    expect(rows.map(r => r.dataset['state'])).toEqual(['done', 'open', 'locked']);
    expect(rows[0].textContent).toContain('2/2');
    expect(rows[1].textContent).toContain('1/3');
    expect(rows[2].textContent).toContain('Opens after stage 1');
  });

  it('marks a closed stage whose approval went stale', async () => {
    const stale = progress({
      gate: { passes: false, approved: false, findings: [{ severity: 'stop', code: 'APPROVAL_STALE', message: 'Stage 0: …', stage: 0 }] }
    });

    expect(stages(await render(stale))[0].dataset['state']).toBe('attention');
  });

  it('marks the selected stage and emits the one clicked', async () => {
    const fixture = await render(progress({}), 0);
    const picked: number[] = [];
    fixture.componentInstance.select.subscribe(n => picked.push(n));

    expect(stages(fixture)[0].getAttribute('aria-current')).toBe('step');
    stages(fixture)[2].click();

    expect(picked).toEqual([2]);
  });
});
