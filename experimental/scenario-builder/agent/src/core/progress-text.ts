// What get_progress tells the model, in a fixed format: the fake model in evals/fake-model.ts
// reads its `- id [` lines, `Drafts due:` and `Gate: passes. Call`.

import type { Progress } from "./api.ts";

export function formatProgress(p: Progress): string {
  const lines = [`Stage ${p.stage}: ${p.stageTitle}`];
  if (p.delegated)
    lines.push("Delegation: on. The designer handed you every decision: don't ask. Fill all open questions with record_answers " +
      "(each proposed, with a one-sentence reason), write any draft that's due, approve_stage, then advance_stage. Keep going to the end.");

  if (p.sources?.length)
    lines.push(`Sources: ${p.sources.map(s => `${s.name} (${s.characters.toLocaleString("en-US")} characters)`).join(", ")}. ` +
      "Search them with search_sources before you recommend an answer, and name the source in your reason.");

  if (p.open.length) {
    lines.push("Open questions, in order:");
    for (const q of p.open) {
      const type = q.answer === "choice" && q.options ? `choice: ${q.options.join(", ")}` : q.answer;
      lines.push(`- ${q.id} [${type}]: ${q.text} | why: ${q.why}${q.recommended ? ` | recommended: ${q.recommended}` : ""}`);
    }
  } else {
    lines.push("Open questions: none");
  }

  lines.push(p.answered.length ? `Answered: ${p.answered.map(a => `${a.id} (${a.who} by ${a.by})`).join(", ")}` : "Answered: none");

  if (p.draftsDue.length) {
    lines.push(`Drafts due: ${p.draftsDue.join(", ")}`);
    // A draft may use only what was decided, so it gets the values; other turns stay short.
    lines.push("Decided so far:", ...p.answered.map(a => `  ${a.id}: ${Array.isArray(a.value) ? a.value.join("; ") : String(a.value)}`));
  }

  lines.push(gateLine(p));
  return lines.join("\n");
}

function gateLine(p: Progress): string {
  if (p.gate.passes) return p.lastStage ? "Gate: passes. This is the last stage; the work is done." : "Gate: passes. Call advance_stage.";
  const findings = p.gate.findings;
  // The findings say which approval: the open stage's, or a closed stage's that went stale.
  if (findings.length && findings.every(f => f.code.startsWith("APPROVAL_")))
    return `Gate: waiting for the owner. ${findings.map(f => f.message).join(" ")}`;
  return `Gate: closed. ${findings.map(f => f.message).join(" ")}`;
}
