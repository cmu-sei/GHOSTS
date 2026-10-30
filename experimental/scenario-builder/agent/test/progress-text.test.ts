import { describe, expect, it } from "vitest";
import type { Progress } from "../src/core/api.ts";
import { formatProgress } from "../src/core/progress-text.ts";

const base: Progress = {
  kindId: "small",
  stage: 0,
  stageTitle: "First",
  lastStage: false,
  gate: { passes: false, approved: false, findings: [{ severity: "stop", code: "QUESTION_OPEN", message: "'b' is not answered." }] },
  open: [
    { id: "a", stage: 0, text: "What is A?", why: "w", recommended: "A1", answer: "text" },
    { id: "b", stage: 0, text: "Pick one", why: "w", answer: "choice", options: ["x", "y"] },
  ],
  answered: [],
  draftsDue: [],
};

describe("formatProgress", () => {
  it("lists open questions with their type, choices, why and recommendation", () => {
    const text = formatProgress(base);

    expect(text).toContain("Stage 0: First");
    expect(text).toContain("- a [text]: What is A? | why: w | recommended: A1");
    expect(text).toContain("- b [choice: x, y]: Pick one | why: w");
    expect(text).not.toContain("- b [choice: x, y]: Pick one | why: w | recommended");
    expect(text).toContain("Answered: none");
    expect(text).toContain("Gate: closed. 'b' is not answered.");
  });

  it("names who answered what, and the drafts due", () => {
    const text = formatProgress({
      ...base,
      open: [],
      answered: [{ id: "a", stage: 0, value: "A1", who: "stated", by: "dev", via: "person", time: "t" }],
      draftsDue: ["executiveSummary"],
      gate: { passes: false, approved: false, findings: [{ severity: "stop", code: "DRAFT_MISSING", message: "The executiveSummary draft isn't written yet." }] },
    });

    expect(text).toContain("Answered: a (stated by dev)");
    expect(text).toContain("Drafts due: executiveSummary");
  });

  it("lists every decided value when a draft is due, and not otherwise", () => {
    const answered = [
      { id: "a", stage: 0, value: "A1", who: "stated" as const, by: "dev", via: "person" as const, time: "t" },
      { id: "c", stage: 1, value: ["one", "two"], who: "proposed" as const, by: "dev", via: "agent" as const, time: "t" },
    ];

    const due = formatProgress({ ...base, open: [], answered, draftsDue: ["executiveSummary"] });
    const notDue = formatProgress({ ...base, open: [], answered });

    expect(due).toContain("Decided so far:\n  a: A1\n  c: one; two");
    expect(notDue).not.toContain("Decided so far");
  });

  it("tells the model to decide everything while delegation is on", () => {
    const text = formatProgress({ ...base, delegated: true });

    expect(text.split("\n")[1]).toMatch(/^Delegation: on\./);
    expect(text).toContain("record_answers");
    expect(formatProgress(base)).not.toContain("Delegation");
  });

  it("says when the gate waits only for the owner", () => {
    const text = formatProgress({
      ...base,
      open: [],
      gate: { passes: false, approval: "Approve it", approved: false, findings: [{ severity: "stop", code: "APPROVAL_MISSING", message: "Waiting for the owner: Approve it." }] },
    });

    expect(text).toContain("Gate: waiting for the owner. Waiting for the owner: Approve it.");
  });

  it("names a closed stage whose approval went stale", () => {
    const text = formatProgress({
      ...base,
      stage: 1,
      open: [],
      gate: { passes: false, approval: "Approve it", approved: false, findings: [{ severity: "stop", code: "APPROVAL_STALE", message: "Stage 0: Confirm: what was approved has changed since.", stage: 0 }] },
    });

    expect(text).toContain("Gate: waiting for the owner. Stage 0: Confirm: what was approved has changed since.");
  });

  it("tells the model to advance, or that the work is done", () => {
    const passes = { ...base, open: [], gate: { passes: true, approved: false, findings: [] } };

    expect(formatProgress(passes)).toContain("Gate: passes. Call advance_stage.");
    expect(formatProgress({ ...passes, lastStage: true })).toContain("Gate: passes. This is the last stage; the work is done.");
  });

  it("lists the sources and says to search them before recommending", () => {
    const text = formatProgress({ ...base, sources: [{ id: 3, name: "Port notice", kind: "url", characters: 12400 }, { id: 4, name: "Log", kind: "file", characters: 800 }] });

    expect(text).toContain("Sources: Port notice (12,400 characters), Log (800 characters). Search them with search_sources before you recommend an answer, and name the source in your reason.");
    expect(formatProgress(base)).not.toContain("Sources:");
  });
});
