// The run is over: turn the state into rows for sim_messages, sim_turns and sim_ties, and the final sim_runs update.
// @include lib/graph.js
// @include lib/sim.js
const { state } = $input.first().json;
const iso = new Date().toISOString().replace(/\.\d{3}Z$/, '+00:00');
return [{
  json: {
    ...simRows(state, $execution.id),
    run: { id: state.run.id, status: 'done', progress: 'done', execution: $execution.id, calls: state.calls, failures: state.failures, messages: state.messages.length, finished_at: iso, locked_at: null, locked_by: '' },
  },
}];
