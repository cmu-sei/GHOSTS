// An in-memory stand-in for the builder API, over a small kind: stage 0 asks `a` (text,
// recommended "A1") and `b` (choice x/y), stage 1 asks `c` (a list). It keeps the API's
// one authority rule the tools depend on: the agent can't overwrite a stated answer.

import { ApiError } from "../src/core/api.ts";
import type { AnsweredView, Finding, PassageView, Progress, ProposalView, QuestionView } from "../src/core/api.ts";
import type { ToolApi } from "../src/core/tools.ts";

const QUESTIONS: QuestionView[] = [
  { id: "a", stage: 0, text: "What is A?", why: "w", recommended: "A1", answer: "text" },
  { id: "b", stage: 0, text: "Pick one", why: "w", answer: "choice", options: ["x", "y"] },
  { id: "c", stage: 1, text: "List some", why: "w", answer: "list" },
];

export const SMALL_KIND = {
  id: "small",
  version: "0.0.1",
  name: "Small",
  stages: [
    { number: 0, title: "First", gate: {}, questions: QUESTIONS.filter(q => q.stage === 0) },
    { number: 1, title: "Second", gate: {}, questions: QUESTIONS.filter(q => q.stage === 1) },
  ],
  drafts: [],
};

export class FakeApi implements ToolApi {
  stage = 0;
  delegated = false;
  readonly approved: number[] = [];
  readonly answers = new Map<string, AnsweredView>();
  readonly proposals: ProposalView[] = [];
  readonly drafts = new Map<string, string>();
  /** The context sources; a search returns the sources that contain any of the query's longer words. */
  readonly sources: { id: number; name: string; text: string }[] = [];
  /** The next call throws this, once. */
  fail?: ApiError;

  private check(): void {
    const fail = this.fail;
    this.fail = undefined;
    if (fail) throw fail;
  }

  /** The next kind fetch throws this, once. */
  failKind?: ApiError;

  async kind(_id: string): Promise<any> {
    this.check();
    const fail = this.failKind;
    this.failKind = undefined;
    if (fail) throw fail;
    return SMALL_KIND;
  }

  async progress(_scenarioId: number): Promise<Progress> {
    this.check();
    const open = QUESTIONS.filter(q => q.stage === this.stage && !this.answers.has(q.id));
    return {
      kindId: "small",
      stage: this.stage,
      stageTitle: SMALL_KIND.stages[this.stage].title,
      lastStage: this.stage === 1,
      gate: {
        passes: open.length === 0,
        approved: false,
        findings: open.map((q): Finding => ({ severity: "stop", code: "QUESTION_OPEN", message: `'${q.id}' is not answered.`, stage: this.stage })),
      },
      open,
      answered: [...this.answers.values()],
      draftsDue: [],
      delegated: this.delegated,
      sources: this.sources.map(s => ({ id: s.id, name: s.name, kind: "text" as const, characters: s.text.length })),
    };
  }

  async putDecision(_scenarioId: number, questionId: string, body: { value: unknown; who?: "stated" | "proposed"; reason?: string; quote?: string }): Promise<AnsweredView> {
    this.check();
    if (this.answers.get(questionId)?.who === "stated")
      throw new ApiError(409, "STATED", `'${questionId}' was stated by dev. Use propose_change to suggest a different value.`);
    const answer: AnsweredView = {
      id: questionId, stage: 0, value: body.value, who: body.who ?? "proposed", by: "dev", via: "agent", reason: body.reason, time: "2026-10-29T12:00:00Z",
    };
    this.answers.set(questionId, answer);
    return answer;
  }

  async propose(_scenarioId: number, body: { questionId: string; value: unknown; reason: string }): Promise<ProposalView> {
    this.check();
    const proposal: ProposalView = {
      id: this.proposals.length + 1, questionId: body.questionId, before: this.answers.get(body.questionId)?.value ?? null,
      after: body.value, reason: body.reason, proposedBy: "dev", status: "pending",
    };
    this.proposals.push(proposal);
    return proposal;
  }

  async searchSources(_scenarioId: number, query: string): Promise<PassageView[]> {
    this.check();
    const words = query.toLowerCase().split(/[^a-z0-9]+/).filter(w => w.length >= 4);
    return this.sources
      .filter(s => words.some(w => s.text.toLowerCase().includes(w)))
      .map(s => ({ source: { id: s.id, name: s.name }, passage: 1, of: 1, text: s.text }));
  }

  async putDraft(_scenarioId: number, field: string, text: string): Promise<void> {
    this.check();
    this.drafts.set(field, text);
  }

  async approve(_scenarioId: number, stage: number): Promise<void> {
    this.check();
    if (!this.delegated) throw new ApiError(403, "AGENT_CANNOT_DECIDE", "Only the owner can approve, unless the owner hands you the decisions.");
    this.approved.push(stage);
  }

  async advance(_scenarioId: number): Promise<{ stage: number }> {
    this.check();
    this.stage++;
    return { stage: this.stage };
  }
}
