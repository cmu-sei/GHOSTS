import { Kind } from '../../core/models/builder.model';

/** Joining words that stay lower case inside a title. */
const MINOR = new Set(['a', 'an', 'and', 'as', 'at', 'but', 'by', 'for', 'in', 'nor', 'of', 'on', 'or', 'the', 'to', 'vs']);

/** "timeSlot" becomes "Time Slot": every word capitalised but the joining ones inside it. */
export function spaced(id: string): string {
  const words = id.replace(/([a-z])([A-Z])/g, '$1 $2').toLowerCase().split(' ');
  return words.map((w, i) => i > 0 && i < words.length - 1 && MINOR.has(w) ? w : w.charAt(0).toUpperCase() + w.slice(1)).join(' ');
}

/**
 * The short name people see for a question or a draft: the kind's label when it has one, the id
 * spaced out when it doesn't. Anything else, such as "stage 0", starts with a capital.
 */
export function labelFor(target: string, kind: Kind | null): string {
  const question = kind?.stages.flatMap(s => s.questions).find(q => q.id === target);
  if (question?.label) {
    return question.label;
  }
  if (question || kind?.drafts.some(d => d.field === target) || (!kind && /^[a-z]+[A-Z]/.test(target))) {
    return spaced(target);
  }
  return target.charAt(0).toUpperCase() + target.slice(1);
}
