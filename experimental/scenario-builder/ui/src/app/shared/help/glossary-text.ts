import { GLOSSARY, Term } from './help-content';

/** A piece of a text: plain, or a glossary term as it is written there. */
export interface Run {
  text: string;
  term?: Term;
}

const escape = (s: string) => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

function pattern(terms: Term[]): { re: RegExp; byWord: Map<string, Term> } | null {
  const byWord = new Map<string, Term>();
  for (const term of terms) {
    for (const word of term.match ?? []) {
      byWord.set(word.toLowerCase(), term);
    }
  }
  if (!byWord.size) {
    return null;
  }
  // Longest first, so "escalation ceiling" wins over a shorter word inside it.
  const words = [...byWord.keys()].sort((a, b) => b.length - a.length).map(escape);
  return { re: new RegExp(`(?<![\\w-])(${words.join('|')})(?![\\w-])`, 'gi'), byWord };
}

const DEFAULT = pattern(GLOSSARY);

/** Splits text into plain runs and term runs: whole words, any case, the first time each term appears. */
export function termRuns(text: string, terms?: Term[]): Run[] {
  const found = terms ? pattern(terms) : DEFAULT;
  if (!found || !text) {
    return [{ text }];
  }
  const runs: Run[] = [];
  const seen = new Set<string>();
  let at = 0;
  for (const m of text.matchAll(found.re)) {
    const term = found.byWord.get(m[0].toLowerCase())!;
    if (seen.has(term.id)) {
      continue;
    }
    seen.add(term.id);
    if (m.index > at) {
      runs.push({ text: text.slice(at, m.index) });
    }
    runs.push({ text: m[0], term });
    at = m.index + m[0].length;
  }
  if (at < text.length) {
    runs.push({ text: text.slice(at) });
  }
  return runs;
}
