// Build the starting state of the run: the cast with their personas, the saved network, the seeded notes.
// @include lib/graph.js
// @include lib/sim.js
const run = $('Pick run').first().json.run;
const rows = (node) => $(node).all().map((i) => i.json).filter((r) => r && r.id !== undefined);
const persona = new Map(rows('Read: participants').map((p) => [Number(p.participant_id), p.persona]));
const cast = rows('Read: sim_cast').map((c) => ({ agent: Number(c.agent), name: c.name, participant_id: Number(c.participant_id), persona: persona.get(Number(c.participant_id)) }))
  .sort((a, b) => a.agent - b.agent);
const saved = rows('Read: sim_networks').find((w) => w.key === run.experiment);
if (!cast.length || !saved) throw new Error(`No cast or no "${run.experiment}" network; run "Network 1 - Set Up Study" first`);
if (cast.some((c) => !c.persona)) throw new Error('A cast member has no persona in the participants table');
return [{ json: { state: simStart(run, cast, { edges: JSON.parse(saved.edges), roles: JSON.parse(saved.roles) }) } }];
