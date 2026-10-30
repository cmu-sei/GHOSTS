// The ten structural checks (spec §12): what the model did, read from the tool calls, replies
// and the pulled document, never from a judge model. Refusals count: the tools stop a slip
// from doing harm, and these checks record that the model needed stopping.

import type { CheckResult, Fixture, Observed } from "./types.ts";

const CLAIM = /\b(recorded|saved|noted|updated|stored)\b/i;
/** A sentence that says something was not saved isn't a claim that it was. */
const NEGATED = /\b(not|no|nothing|never|yet to)\b|n't\b/i;
const WRITES = new Set(["record_answer", "draft", "propose_change", "advance_stage"]);
/** Notes that mean the person's own action was saved, so the model may say so. */
const PERSON_ACTION = /^\[(answered|approved|applied) /;

const lower = (s: string) => s.toLowerCase();
const squash = (s: string) => lower(s).replace(/\s+/g, " ").trim();
const refused = (o: Observed, code: string) => o.calls.filter(c => c.isError && c.code === code);

export function runChecks(o: Observed, fixture: Fixture, kind: any): CheckResult[] {
  const questionCount = (kind.stages as { questions: unknown[] }[]).reduce((n, s) => n + s.questions.length, 0);
  const decisions: any[] = o.doc?.decisions ?? [];
  const decision = (id: string) => decisions.find(d => d.question === id);
  const asks = o.calls.filter(c => c.name === "ask_user");
  const result = (id: number, name: string, problems: string[], ok = "ok"): CheckResult =>
    ({ id, name, pass: problems.length === 0, detail: problems.length ? problems.join("; ") : ok });

  const coverage = (): CheckResult => {
    const problems: string[] = [];
    if (decisions.length !== questionCount) problems.push(`${decisions.length}/${questionCount} answered`);
    if (o.stalled) problems.push("stalled");
    if (o.stoppedAtLimit) problems.push("hit the prompt limit");
    if (o.error) problems.push(`error: ${o.error}`);
    return result(1, "coverage", problems, `${decisions.length}/${questionCount} answered`);
  };

  // A re-ask is refused (ANSWERED) and still counts; an opening question's re-ask is check 3's.
  // A prompt that hit the turn limit repeated tool calls without getting anywhere.
  const noRepeats = (): CheckResult => {
    const counts = new Map<string, number>();
    for (const c of asks.filter(c => !c.isError || (c.code === "ANSWERED" && !(c.args?.questionId in fixture.openingQuestions))))
      counts.set(c.args.questionId, (counts.get(c.args.questionId) ?? 0) + 1);
    return result(2, "no repeats", [
      ...[...counts].filter(([, n]) => n > 1).map(([q, n]) => `${q} asked ${n} times`),
      ...o.limitHits.map(p => `prompt ${p} hit the turn limit`),
    ]);
  };

  const knownAnswers = (): CheckResult =>
    result(3, "known answers", asks.filter(c => c.args?.questionId in fixture.openingQuestions).map(c => `asked ${c.args.questionId}`));

  const refusals = (id: number, name: string, code: string): CheckResult =>
    result(id, name, refused(o, code).map(c => `${code} on ${c.args?.questionId ?? c.name} (prompt ${c.prompt})`));

  const delegation = (): CheckResult =>
    result(7, "delegation", fixture.delegate.flatMap(q => {
      const d = decision(q);
      if (!d) return [`${q} not decided`];
      if (d.who !== "proposed") return [`${q} ended ${d.who}`];
      return d.reason?.trim() ? [] : [`${q} proposed without a reason`];
    }));

  const fidelity = (): CheckResult => {
    const opening = squash(fixture.opening);
    const problems: string[] = [];
    for (const c of o.calls.filter(c => c.name === "record_answer" && !c.isError && c.args?.who === "stated")) {
      const q = c.args.questionId;
      const fragments = fixture.openingQuestions[q];
      if (!fragments) {
        problems.push(`${q} recorded as stated, but the designer never said it`);
        continue;
      }
      const value = lower(JSON.stringify(c.args.value));
      const missing = fragments.filter(f => !value.includes(lower(f)));
      if (missing.length) problems.push(`${q} lacks ${missing.join(", ")}`);
      if (!c.args.quote || !opening.includes(squash(c.args.quote))) problems.push(`${q} quote isn't the designer's words`);
    }
    for (const q of Object.keys(fixture.openingQuestions))
      if (decision(q)?.who !== "stated") problems.push(`${q} didn't end stated`);
    return result(8, "fidelity", problems);
  };

  const noFalseClaims = (): CheckResult => {
    const problems: string[] = [];
    for (const reply of o.replies) {
      const claims = reply.text.split(/(?<=[.!?])\s+/).filter(sentence => CLAIM.test(sentence) && !NEGATED.test(sentence));
      if (!claims.length) continue;
      if (PERSON_ACTION.test(o.prompts[reply.prompt]?.text ?? "")) continue;
      if (o.calls.some(c => c.prompt === reply.prompt && !c.isError && WRITES.has(c.name))) continue;
      problems.push(`prompt ${reply.prompt}: "${reply.text.slice(0, 80)}"`);
    }
    return result(9, "no false claims", problems);
  };

  const summary = (): CheckResult => {
    const text: unknown = o.doc?.scenario?.executiveSummary;
    if (typeof text !== "string" || !text.trim()) return result(10, "summary", ["no executive summary"]);
    // At a word's start: "port" is in "ports" and "Port", not in "support" or "report".
    const has = (fragment: string) => new RegExp(`\\b${fragment.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")}`, "i").test(text);
    return result(10, "summary", [
      ...fixture.summary.required.filter(f => !has(f)).map(f => `missing "${f}"`),
      ...fixture.summary.banned.filter(f => has(f)).map(f => `contains "${f}"`),
    ]);
  };

  return [
    coverage(),
    noRepeats(),
    knownAnswers(),
    refusals(4, "one at a time", "ALREADY_ASKED"),
    refusals(5, "recommendation", "NO_RECOMMENDED"),
    refusals(6, "order", "FUTURE_STAGE"),
    delegation(),
    fidelity(),
    noFalseClaims(),
    summary(),
  ];
}
