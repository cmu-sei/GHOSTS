import { Progress } from '../../../core/models/builder.model';
import { delegatesAll, finished, madeProgress } from './delegation';

describe('delegatesAll', () => {
  it.each(['you decide', 'You decide on all', 'ok, decide everything', 'just decide the rest', "it's up to you", 'decide for me'])(
    'hands everything over on "%s"', text => expect(delegatesAll(text)).toBe(true));

  it.each(["I'll decide later", 'who decides the gate?', 'let me think about it', 'decided: civilian'])(
    'keeps the decisions on "%s"', text => expect(delegatesAll(text)).toBe(false));
});

const progress = (overrides: Partial<Progress>): Progress => ({
  kindId: 'k', stage: 0, stageTitle: 'S', lastStage: false,
  gate: { passes: false, approved: false, findings: [] },
  open: [], answered: [], draftsDue: [], delegated: true, ...overrides
});
const answered = (n: number) => Array.from({ length: n }, (_, i) =>
  ({ id: `q${i}`, stage: 0, value: 'x', who: 'proposed' as const, by: 'dev', via: 'agent' as const, time: 't' }));

describe('madeProgress', () => {
  it('counts a new answer, an approval, a draft written or a stage opened', () => {
    const before = progress({ answered: answered(2), draftsDue: ['executiveSummary'] });

    expect(madeProgress(before, progress({ answered: answered(3), draftsDue: ['executiveSummary'] }))).toBe(true);
    expect(madeProgress(before, { ...before, gate: { ...before.gate, approved: true } })).toBe(true);
    expect(madeProgress(before, { ...before, draftsDue: [] })).toBe(true);
    expect(madeProgress(before, { ...before, stage: 1 })).toBe(true);
  });

  it('sees no progress when nothing changed', () => {
    const before = progress({ answered: answered(2) });

    expect(madeProgress(before, progress({ answered: answered(2) }))).toBe(false);
  });
});

describe('finished', () => {
  it('is the last stage with its gate passed', () => {
    expect(finished(progress({ lastStage: true, gate: { passes: true, approved: true, findings: [] } }))).toBe(true);
    expect(finished(progress({ lastStage: true }))).toBe(false);
    expect(finished(progress({ gate: { passes: true, approved: true, findings: [] } }))).toBe(false);
  });
});
