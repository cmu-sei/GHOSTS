import { Kind } from '../../core/models/builder.model';
import { labelFor } from './question-label';

const kind: Kind = {
  id: 'k', version: '1', name: 'K',
  drafts: [{ field: 'executiveSummary', dueAfterStage: 0, path: '/s' }],
  stages: [{ number: 0, title: 'S', gate: {}, questions: [
    { id: 'plainEnglish', label: 'Plain English', text: 'Is English…?', why: 'w', answer: 'choice', options: ['Yes'], path: '/p' },
    { id: 'timeSlot', text: 'When?', why: 'w', answer: 'text', path: '/t' }
  ] }]
};

describe('labelFor', () => {
  it("uses a question's own label", () => expect(labelFor('plainEnglish', kind)).toBe('Plain English'));
  it('spaces out an id that has no label, in title case', () => expect(labelFor('timeSlot', kind)).toBe('Time Slot'));
  it('names a draft the same way', () => expect(labelFor('executiveSummary', kind)).toBe('Executive Summary'));
  it('keeps short joining words lower case inside a label', () => expect(labelFor('realVsFictional', null)).toBe('Real vs Fictional'));
  it('starts any other target, such as a stage, with a capital', () => expect(labelFor('stage 0', kind)).toBe('Stage 0'));
  it('copes without a kind', () => expect(labelFor('timeSlot', null)).toBe('Time Slot'));
});
