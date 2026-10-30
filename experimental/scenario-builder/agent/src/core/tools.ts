// The agent's nine tools. They hold the rules, so a small model can't get around them: a
// refusal comes back as a result with a code (which the evaluation counts) and a message that
// says what to do instead. API refusals pass through the same way.

import { Type } from "@earendil-works/pi-ai";
import type { AgentTool, AgentToolResult } from "@earendil-works/pi-agent-core";
import { ApiError } from "./api.ts";
import type { BuilderApi, PassageView, Progress, ProposalView, QuestionView } from "./api.ts";
import { formatProgress } from "./progress-text.ts";

/** What the tools need from the API; the tests pass an in-memory stand-in. */
export type ToolApi = Pick<BuilderApi, "progress" | "kind" | "putDecision" | "propose" | "putDraft" | "advance" | "approve" | "searchSources">;

export interface QuestionCard {
  questionId: string;
  question: string;
  recommended?: { answer: string; why: string };
  alternatives: string[];
  answer: QuestionView["answer"];
  options?: string[];
}

export interface ToolContext {
  api: ToolApi;
  scenarioId: number;
  /** Reset by the session at each prompt: one question per turn. */
  turn: { asked: boolean };
  /** Everything the designer typed this session: a stated answer's quote must come from here. */
  said: string[];
  onQuestion(card: QuestionCard): void;
  onProposal(proposal: ProposalView): void;
}

export interface ToolDetails {
  summary: string;
  code?: string;
  card?: QuestionCard;
}

type Result = AgentToolResult<ToolDetails>;

const ok = (text: string, summary: string, extra: Partial<ToolDetails> = {}): Result =>
  ({ content: [{ type: "text", text }], details: { summary, ...extra } });

const refuse = (code: string, text: string): Result =>
  ({ content: [{ type: "text", text }], details: { summary: code, code }, isError: true });

function tool(def: Omit<AgentTool<any, ToolDetails>, "execute"> & { execute: (params: any) => Promise<Result> }): AgentTool<any, ToolDetails> {
  return {
    ...def,
    execute: async (_id, params) => {
      try {
        return await def.execute(params);
      } catch (e) {
        if (e instanceof ApiError) return refuse(e.code, e.message);
        throw e;
      }
    },
  };
}

/** Lower case, single spaces, no quote marks or trailing stop: how a quote is matched to what was said. */
const plain = (text: string) => text.toLowerCase().replace(/["“”'‘’]/g, "").replace(/\s+/g, " ").replace(/[.!?]+$/, "").trim();

const passageText = (p: PassageView) => `[${p.source.name}, passage ${p.passage} of ${p.of}]\n${p.text}`;

interface KindQuestion {
  id: string;
  recommended?: string;
  answer: QuestionView["answer"];
  options?: string[];
}

export function createTools(ctx: ToolContext): AgentTool<any, ToolDetails>[] {
  let kind: Promise<Map<string, { question: KindQuestion; stage: number }>> | undefined;
  const questions = (kindId: string) =>
    (kind ??= ctx.api.kind(kindId).then(
      k => new Map((k.stages as { number: number; questions: KindQuestion[] }[]).flatMap(s => s.questions.map(q => [q.id, { question: q, stage: s.number }] as const))),
      // A failed fetch isn't kept: the next ask tries again.
      e => {
        kind = undefined;
        throw e;
      }));

  const getProgress = tool({
    name: "get_progress",
    label: "Progress",
    description: "Returns the open stage, its open questions in order (with why and the recommended answer), what is answered, any draft that is due, and the gate.",
    parameters: Type.Object({}),
    execute: async () => {
      const p = await ctx.api.progress(ctx.scenarioId);
      return ok(formatProgress(p) + await passagesForOpen(p), `stage ${p.stage}`);
    },
  });

  // A small model doesn't reliably search before it recommends, and copies the kind's suggestion when it
  // doesn't, so the open questions arrive with their passages: each question's best match, tied to the
  // questions it answers, once each, the first question's first.
  const passagesForOpen = async (p: Progress): Promise<string> => {
    if (!p.sources?.length || !p.open.length) return "";
    const found = new Map<string, { passage: PassageView; ids: string[] }>();
    for (const q of p.open) {
      const [best] = await ctx.api.searchSources(ctx.scenarioId, q.text);
      if (!best) continue;
      const key = `${best.source.id}:${best.passage}`;
      if (!found.has(key) && found.size === 4) continue;
      const entry = found.get(key) ?? { passage: best, ids: [] };
      entry.ids.push(q.id);
      found.set(key, entry);
    }
    if (!found.size) return "";
    return "\n\nFrom the sources. When a passage answers a question, recommend what it says, not the kind's suggestion, and name the source in your reason:\n" +
      [...found.values()].map(f => `For ${f.ids.join(", ")}: ${passageText(f.passage)}`).join("\n\n");
  };

  const askUser = tool({
    name: "ask_user",
    label: "Ask",
    description: "Shows the designer one open question as a card and ends your turn. Give the recommended answer (required when the question has one) and two or three alternatives.",
    parameters: Type.Object({
      questionId: Type.String({ description: "The id of an open question, from get_progress" }),
      question: Type.String({ description: "The question, in your words" }),
      recommended: Type.Optional(Type.Object({
        answer: Type.String({ description: "The recommended answer" }),
        why: Type.String({ description: "Why, in one sentence" }),
      })),
      alternatives: Type.Array(Type.String(), { description: "Two or three other answers" }),
    }),
    execute: async (params: { questionId: string; question: string; recommended?: { answer: string; why: string }; alternatives: string[] }) => {
      if (ctx.turn.asked) return refuse("ALREADY_ASKED", "You already asked a question this turn. Wait for the answer.");
      const progress = await ctx.api.progress(ctx.scenarioId);
      if (progress.delegated)
        return refuse("DELEGATED", "The designer handed you every decision: don't ask. Decide the open questions yourself with record_answers.");
      const openIds = progress.open.map(q => q.id).join(", ") || "none";
      const found = (await questions(progress.kindId)).get(params.questionId);
      if (!found) return refuse("UNKNOWN_QUESTION", `There is no question '${params.questionId}'. Open now: ${openIds}.`);
      const answered = progress.answered.find(a => a.id === params.questionId);
      if (answered)
        return refuse("ANSWERED", `'${params.questionId}' is already answered (${JSON.stringify(answered.value)}). Don't ask it again.`);
      if (found.stage > progress.stage)
        return refuse("FUTURE_STAGE", `'${params.questionId}' belongs to stage ${found.stage}, which isn't open. Open now: ${openIds}.`);
      if (found.question.recommended && !params.recommended)
        return refuse("NO_RECOMMENDED", progress.sources?.length
          ? `'${params.questionId}' needs a recommended answer. Recommend what the sources say if they answer it; otherwise the kind's suggestion, ${found.question.recommended}.`
          : `'${params.questionId}' has a recommended answer: ${found.question.recommended}. Include it as recommended.`);

      const card: QuestionCard = {
        questionId: params.questionId,
        question: params.question,
        ...(params.recommended ? { recommended: params.recommended } : {}),
        alternatives: params.alternatives,
        answer: found.question.answer,
        ...(found.question.options ? { options: found.question.options } : {}),
      };
      ctx.turn.asked = true;
      ctx.onQuestion(card);
      return ok("Question shown. Wait for the user's answer.", `asked ${params.questionId}`, { card });
    },
  });

  const recordAnswer = tool({
    name: "record_answer",
    label: "Record",
    description: "Records an answer. who \"stated\": the designer said it; quote their exact words. who \"proposed\": you chose it (after \"you decide\", or from earlier answers); give a one-sentence reason.",
    parameters: Type.Object({
      questionId: Type.String(),
      value: Type.Union([Type.String(), Type.Array(Type.String())], { description: "Text, one of a choice's options, or a list of strings" }),
      who: Type.Union([Type.Literal("stated"), Type.Literal("proposed")]),
      reason: Type.Optional(Type.String()),
      quote: Type.Optional(Type.String({ description: "The designer's own words, for a stated answer" })),
    }),
    execute: async (params: { questionId: string; value: string | string[]; who: "stated" | "proposed"; reason?: string; quote?: string }) => {
      if (params.who === "stated" && !params.quote?.trim())
        return refuse("QUOTE_REQUIRED", "A stated answer needs the designer's own words as quote. If they didn't say it, record it as proposed with a reason.");
      if (params.who === "proposed" && !params.reason?.trim())
        return refuse("REASON_REQUIRED", "A proposed answer needs a one-sentence reason.");
      // A small model quotes a source, or itself, as if the designer had said it; only their own words are stated.
      if (params.who === "stated" && !ctx.said.some(text => plain(text).includes(plain(params.quote!))))
        return refuse("QUOTE_NOT_THEIRS",
          "That quote isn't in anything the designer wrote to you. Words from a source aren't theirs: record it as proposed, with the source in the reason.");
      await ctx.api.putDecision(ctx.scenarioId, params.questionId, {
        value: params.value, who: params.who, reason: params.reason, quote: params.quote,
      });
      return ok(`Recorded ${params.questionId} as ${params.who}: ${JSON.stringify(params.value)}.`, `recorded ${params.questionId}`);
    },
  });

  const recordAnswers = tool({
    name: "record_answers",
    label: "Record all",
    description: "Records several of your own answers at once, each as proposed with a one-sentence reason. Use it to fill a stage when the designer has handed you the decisions.",
    parameters: Type.Object({
      answers: Type.Array(Type.Object({
        questionId: Type.String(),
        value: Type.Union([Type.String(), Type.Array(Type.String())], { description: "Text, one of a choice's options, or a list of strings" }),
        reason: Type.String({ description: "Why, in one sentence" }),
      }), { minItems: 1 }),
    }),
    execute: async (params: { answers: { questionId: string; value: string | string[]; reason: string }[] }) => {
      const reasonless = params.answers.filter(a => !a.reason?.trim()).map(a => a.questionId);
      if (reasonless.length) return refuse("REASON_REQUIRED", `Every answer needs a one-sentence reason; these have none: ${reasonless.join(", ")}.`);
      const recorded: string[] = [];
      const refused: string[] = [];
      for (const a of params.answers) {
        try {
          await ctx.api.putDecision(ctx.scenarioId, a.questionId, { value: a.value, who: "proposed", reason: a.reason });
          recorded.push(a.questionId);
        } catch (e) {
          if (!(e instanceof ApiError)) throw e;
          refused.push(`${a.questionId}: ${e.code}. ${e.message}`);
        }
      }
      if (!recorded.length) return refuse("NONE_RECORDED", `Nothing was recorded.\n${refused.join("\n")}`);
      return ok(`Recorded ${recorded.length}: ${recorded.join(", ")}.${refused.length ? `\nRefused:\n${refused.join("\n")}` : ""}`,
        `recorded ${recorded.length}${refused.length ? `, ${refused.length} refused` : ""}`);
    },
  });

  const approveStage = tool({
    name: "approve_stage",
    label: "Approve",
    description: "Approves the open stage's gate. Only works while the designer has handed you the decisions; otherwise ask them to approve.",
    parameters: Type.Object({}),
    execute: async () => {
      const { stage } = await ctx.api.progress(ctx.scenarioId);
      await ctx.api.approve(ctx.scenarioId, stage);
      return ok(`Stage ${stage} approved.`, `approved stage ${stage}`);
    },
  });

  const proposeChange = tool({
    name: "propose_change",
    label: "Propose",
    description: "Proposes a change to an answer the designer stated. The owner applies or discards it; nothing changes until then.",
    parameters: Type.Object({
      questionId: Type.String(),
      value: Type.Union([Type.String(), Type.Array(Type.String())]),
      reason: Type.String({ description: "Why, in one sentence" }),
    }),
    execute: async (params: { questionId: string; value: string | string[]; reason: string }) => {
      const proposal = await ctx.api.propose(ctx.scenarioId, params);
      ctx.onProposal(proposal);
      return ok(`Proposal P-${proposal.id} is waiting for the owner. Don't treat it as decided.`, `proposed P-${proposal.id}`);
    },
  });

  const draft = tool({
    name: "draft",
    label: "Draft",
    description: "Writes a draft that get_progress lists as due, such as executiveSummary, from what has been decided.",
    parameters: Type.Object({
      field: Type.String({ description: "The draft's name, from get_progress" }),
      text: Type.String(),
    }),
    execute: async (params: { field: string; text: string }) => {
      await ctx.api.putDraft(ctx.scenarioId, params.field, params.text);
      return ok(`Saved the ${params.field} draft.`, `drafted ${params.field}`);
    },
  });

  const advanceStage = tool({
    name: "advance_stage",
    label: "Advance",
    description: "Opens the next stage once the open stage's gate passes. If it can't, says what is in the way.",
    parameters: Type.Object({}),
    execute: async () => {
      const { stage } = await ctx.api.advance(ctx.scenarioId);
      return ok(`Stage ${stage} is open. Call get_progress for its questions.`, `stage ${stage}`);
    },
  });

  const searchSources = tool({
    name: "search_sources",
    label: "Search",
    description: "Searches the context the designer gave (pasted text, pages, documents) and returns the passages that share the most words with the query, each under its source's name.",
    parameters: Type.Object({
      query: Type.String({ description: "A few words about what you need, such as \"generator failure cause\"" }),
    }),
    execute: async (params: { query: string }) => {
      if (!params.query.trim()) return refuse("QUERY_EMPTY", "Give a few words to search for.");
      const passages = await ctx.api.searchSources(ctx.scenarioId, params.query);
      if (!passages.length)
        return ok(`No passage in the sources matches "${params.query}". Don't cite a source for it; try other words or decide without one.`, "no match");
      return ok(passages.map(passageText).join("\n\n"), `${passages.length} passages`);
    },
  });

  return [getProgress, askUser, recordAnswer, recordAnswers, proposeChange, draft, approveStage, advanceStage, searchSources];
}
