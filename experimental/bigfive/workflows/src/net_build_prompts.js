// One item per model call of the current phase. The first item also carries the state, for Apply replies.
// Calls that failed are asked again (Apply replies sends the state back with state.pending set), so only
// the ones still without a reply go out. `body` is the request for the provider's API: Bedrock Converse or Ollama chat.
// @include lib/graph.js
// @include lib/sim.js
const state = $input.first().json.state;
const calls = simPrompts(state);
if (!state.pending) state.pending = { replies: calls.map(() => ''), tries: 0 };
// No temperature on the run = the model's own default.
const temperature = (c) => (c.temperature === null || c.temperature === undefined ? {} : { temperature: c.temperature });
const open = calls.map((c, n) => ({ ...c, n })).filter((c) => !state.pending.replies[c.n]);
return open.map((c, k) => ({
  json: {
    ...c,
    body: c.provider === 'ollama'
      ? { model: c.model, stream: false, think: false, messages: [{ role: 'user', content: c.prompt }], options: temperature(c) }
      : { messages: [{ role: 'user', content: [{ text: c.prompt }] }], inferenceConfig: { maxTokens: 4000, ...temperature(c) } },
    ...(k === 0 ? { state } : {}),
  },
}));
