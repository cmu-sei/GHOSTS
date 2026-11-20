// Fold the replies of this phase into the state and move on. Failed calls get two more tries before they
// count as an agent staying quiet. A phase with nothing to ask (a judge with no messages) is skipped here.
// Output: the state, plus the progress fields for the sim_runs row.
// @include lib/graph.js
// @include lib/sim.js
const built = $('Build prompts').all();
const state = built[0].json.state;
const answers = $input.all().map((i) => i.json);
if (answers.length !== built.length) throw new Error(`${built.length} calls went out, ${answers.length} replies came back`);
// Ollama replies in message.content; Bedrock Converse in output.message.content, where only the text parts count.
const textOf = (a) => (a.message && typeof a.message.content === 'string' ? a.message.content : ((a.output && a.output.message && a.output.message.content) || []).map((part) => part.text || '').join(''));
answers.forEach((a, k) => { state.pending.replies[built[k].json.n] = textOf(a).trim(); });
const { replies, tries } = state.pending;
const failed = replies.filter((r) => !r).length;
// Every call failing is a broken model or credential, not agents staying quiet: stop instead of recording an empty run.
if (failed === replies.length) throw new Error(`Every model call of this phase failed: ${JSON.stringify(answers[0].error || answers[0]).slice(0, 400)}`);
if (failed && tries < 2) {
  state.pending.tries++;
} else {
  delete state.pending;
  simApply(state, replies);
  while (!simDone(state) && simPrompts(state).length === 0) simApply(state, []);
}
return [{
  json: {
    state, done: simDone(state), id: state.run.id, progress: simProgress(state), calls: state.calls, failures: state.failures, messages: state.messages.length,
    locked_at: new Date().toISOString().replace(/\.\d{3}Z$/, '+00:00'), locked_by: $execution.id,
  },
}];
