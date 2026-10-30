import { Progress } from '../../../core/models/builder.model';

/** "You decide", "decide everything", "up to you": the designer hands every remaining decision to the agent. */
const HAND_OVER = /\b(you decide|decide (?:it all|all|everything|the rest|for me)|up to you)\b/i;

export function delegatesAll(text: string): boolean {
  return HAND_OVER.test(text);
}

/** Whether a reply moved the scenario on: an answer, a draft, an approval, or a stage opened. */
export function madeProgress(before: Progress, after: Progress): boolean {
  const key = (p: Progress) => [p.answered.length, p.stage, p.gate.passes, p.gate.approved, p.draftsDue.join(',')].join('|');
  return key(before) !== key(after);
}

/** The last stage is open and its gate has passed: nothing is left to decide. */
export function finished(p: Progress): boolean {
  return p.lastStage && p.gate.passes;
}
