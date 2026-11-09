import { Term } from './help-content';
import { termRuns } from './glossary-text';

const terms: Term[] = [
  { id: 'gt', term: 'Ground Truth', definition: 'd', where: 'w', match: ['ground truth', 'ground-truth'] },
  { id: 'ec', term: 'Escalation Ceiling', definition: 'd', where: 'w', match: ['escalation ceiling'] },
  { id: 'esc', term: 'Escalation', definition: 'd', where: 'w', match: ['escalation'] },
  { id: 'inject', term: 'Inject', definition: 'd', where: 'w', match: ['injects', 'inject'] },
  { id: 'attack', term: 'ATT&CK', definition: 'd', where: 'w', match: ['ATT&CK'] }
];

const marked = (text: string) => termRuns(text, terms).filter(r => r.term).map(r => r.text);

describe('termRuns', () => {
  it('splits text into plain runs and term runs that join back into the text', () => {
    const text = 'List the ground-truth facts, then the injects.';
    const runs = termRuns(text, terms);

    expect(runs.map(r => r.text).join('')).toBe(text);
    expect(runs.filter(r => r.term).map(r => r.term!.id)).toEqual(['gt', 'inject']);
  });

  it('matches any case, the longest phrase first, and each term only the first time', () => {
    expect(marked('Premise, Ground Truth and Escalation Ceiling; ground truth again')).toEqual(['Ground Truth', 'Escalation Ceiling']);
  });

  it('matches whole words only', () => {
    expect(marked('Reinjects and injection')).toEqual([]);
    expect(marked('Which ATT&CK techniques?')).toEqual(['ATT&CK']);
  });

  it('leaves text with no terms as one plain run', () => {
    expect(termRuns('What is the exercise’s name?', terms)).toEqual([{ text: 'What is the exercise’s name?' }]);
  });
});
