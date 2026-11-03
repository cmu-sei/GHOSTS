// Keeps a long interview inside the window: above 60% of it, old tool results are elided,
// oldest first, never the last few messages (jeffort's session.ts prune).

import type { AgentMessage } from "@earendil-works/pi-agent-core";

export const PRUNE_AT = 0.6;
const ELIDED = "[older result elided]";

/** Characters / 4: rough, and only used to decide when to prune. */
function estimateTokens(messages: AgentMessage[]): number {
  return Math.ceil(JSON.stringify(messages).length / 4);
}

export function prune(messages: AgentMessage[], contextWindow: number, keepLast = 6): AgentMessage[] {
  const budget = contextWindow * PRUNE_AT;
  if (estimateTokens(messages) <= budget) return messages;
  const out = [...messages];
  for (let i = 0; i < out.length - keepLast && estimateTokens(out) > budget; i++) {
    const m = out[i] as any;
    if (m.role === "toolResult" && !(m.content.length === 1 && m.content[0].text === ELIDED))
      out[i] = { ...m, content: [{ type: "text", text: ELIDED }] } as AgentMessage;
  }
  return out;
}
