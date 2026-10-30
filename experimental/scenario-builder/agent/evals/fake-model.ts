// A rule-driven stand-in for a model server, speaking the OpenAI-compatible streaming protocol.
// It checks plumbing only (the pass-through, the tools, the harness, the panel): it reads the
// progress text's fixed lines and asks every open question in order, so it asks the opening's
// questions too and fails check 3 by design (jeffort's evals/fake-openai.ts).
//
//   node evals/fake-model.ts [port]      # default 5299, which the API's "fake" model points at

import { createServer } from "node:http";
import type { IncomingMessage, ServerResponse } from "node:http";
import { pathToFileURL } from "node:url";

type Reply = { text: string } | { tool: string; args: Record<string, unknown> };

const textOf = (content: unknown): string =>
  typeof content === "string" ? content : Array.isArray(content) ? content.map((p: any) => p?.text ?? "").join("") : "";

const QUESTION = /^- (\S+) \[([^\]]*)\]: (.*?) \| why: .*?(?: \| recommended: (.*))?$/;

/** The latest get_progress result, for what a "you decide" should pick. */
function lastProgress(messages: any[]): string {
  for (let i = messages.length - 1; i >= 0; i--) {
    const m = messages[i];
    if (m.role === "tool" && textOf(m.content).startsWith("Stage ")) return textOf(m.content);
  }
  return "";
}

function toolNameOf(messages: any[], toolCallId: string): string | undefined {
  for (const m of messages)
    for (const call of m.tool_calls ?? []) if (call.id === toolCallId) return call.function?.name;
  return undefined;
}

export function decide(messages: any[]): Reply {
  const last = messages.at(-1);
  if (!last) return { tool: "get_progress", args: {} };

  if (last.role === "user") {
    const delegated = textOf(last.content).match(/\[delegated (\S+)\]/);
    if (!delegated) return { tool: "get_progress", args: {} };
    const q = delegated[1];
    const line = lastProgress(messages).split("\n").map(l => l.match(QUESTION)).find(m => m?.[1] === q);
    const type = line?.[2] ?? "text";
    const value = type.startsWith("choice: ") ? type.slice(8).split(", ")[0] : type === "list" ? ["fake value"] : "fake value";
    return { tool: "record_answer", args: { questionId: q, value, who: "proposed", reason: "Delegated; a placeholder value." } };
  }

  if (last.role === "tool") {
    const name = toolNameOf(messages, last.tool_call_id);
    const text = textOf(last.content);
    if (name === "get_progress") {
      if (text.includes("Drafts due:"))
        return { tool: "draft", args: { field: "executiveSummary", text: "A fake summary about the port and its power in the heat." } };
      const open = text.split("\n").map(l => l.match(QUESTION)).find(Boolean);
      if (open) {
        const [, questionId, , question, recommended] = open;
        return {
          tool: "ask_user",
          args: { questionId, question, alternatives: [], ...(recommended ? { recommended: { answer: recommended, why: "The kind recommends it." } } : {}) },
        };
      }
      if (text.includes("Gate: passes. Call")) return { tool: "advance_stage", args: {} };
      return { text: "Please approve on the Questions page." };
    }
    if (name === "advance_stage" || name === "draft" || name === "record_answer") return { tool: "get_progress", args: {} };
  }
  return { text: "Done." };
}

let calls = 0;

function chunk(delta: Record<string, unknown>, finish: string | null): string {
  return `data: ${JSON.stringify({ id: "fake", object: "chat.completion.chunk", created: 0, model: "fake", choices: [{ index: 0, delta, finish_reason: finish }] })}\n\n`;
}

function respond(res: ServerResponse, reply: Reply, stream: boolean): void {
  const tool = "tool" in reply ? { id: `call_${++calls}`, type: "function", function: { name: reply.tool, arguments: JSON.stringify(reply.args) } } : null;
  const usage = { prompt_tokens: 10, completion_tokens: 5, total_tokens: 15 };
  if (!stream) {
    res.writeHead(200, { "content-type": "application/json" });
    res.end(JSON.stringify({
      id: "fake", object: "chat.completion", created: 0, model: "fake", usage,
      choices: [{ index: 0, finish_reason: tool ? "tool_calls" : "stop", message: { role: "assistant", content: tool ? null : (reply as { text: string }).text, ...(tool ? { tool_calls: [tool] } : {}) } }],
    }));
    return;
  }
  res.writeHead(200, { "content-type": "text/event-stream", "cache-control": "no-cache" });
  res.write(tool
    ? chunk({ role: "assistant", tool_calls: [{ index: 0, ...tool }] }, null)
    : chunk({ role: "assistant", content: (reply as { text: string }).text }, null));
  res.write(chunk({}, tool ? "tool_calls" : "stop"));
  res.write(`data: ${JSON.stringify({ id: "fake", object: "chat.completion.chunk", created: 0, model: "fake", choices: [], usage })}\n\n`);
  res.end("data: [DONE]\n\n");
}

async function body(req: IncomingMessage): Promise<any> {
  const parts: Buffer[] = [];
  for await (const part of req) parts.push(part as Buffer);
  return JSON.parse(Buffer.concat(parts).toString("utf8") || "{}");
}

export function startFakeModel(port = 5299): Promise<{ port: number; close(): Promise<void> }> {
  const server = createServer(async (req, res) => {
    try {
      if (req.method === "GET" && req.url?.endsWith("/models")) {
        res.writeHead(200, { "content-type": "application/json" });
        res.end(JSON.stringify({ object: "list", data: [{ id: "fake", object: "model" }] }));
        return;
      }
      if (req.method === "POST" && req.url?.endsWith("/chat/completions")) {
        const request = await body(req);
        respond(res, decide(request.messages ?? []), request.stream === true);
        return;
      }
      res.writeHead(404).end();
    } catch (e) {
      res.writeHead(500, { "content-type": "application/json" }).end(JSON.stringify({ error: { message: (e as Error).message } }));
    }
  });
  return new Promise((resolve, reject) => {
    server.once("error", reject);
    server.listen(port, () => resolve({ port, close: () => new Promise<void>(r => server.close(() => r())) }));
  });
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const port = Number(process.argv[2] ?? 5299);
  await startFakeModel(port);
  console.log(`fake model on http://localhost:${port}/v1`);
}
