// Set Up Study: choose the 25 participants the agents are built from and generate the three networks.
// Everything follows from the seed, so running this again with the same seed changes nothing.
// Output: one item { summary, sim_cast: [...], sim_networks: [...] } for the upsert blocks.
// @include lib/scoring.js
// @include lib/graph.js
const N = 25;
const NAMES = ['Alex', 'Avery', 'Blake', 'Casey', 'Charlie', 'Dakota', 'Devin', 'Elliot', 'Emerson', 'Finley', 'Harper', 'Hayden', 'Jamie',
  'Jordan', 'Kai', 'Logan', 'Morgan', 'Parker', 'Quinn', 'Reese', 'Robin', 'Rowan', 'Shay', 'Skyler', 'Taylor'];
const seed = Math.floor(Number($('Set up form').first().json.Seed)) || 1;

const eligible = new Set($('Read: participants').all().map((i) => i.json).filter((p) => p && p.persona && isTrue(p.has_mini_markers)).map((p) => Number(p.participant_id)));
const people = $('Read: scores').all().map((i) => i.json)
  .filter((s) => s && s.subject === 'human_bfi2' && eligible.has(Number(s.participant_id)) && FACETS.every((f) => toNumber(s[f]) !== null))
  .map((s) => ({ participant_id: Number(s.participant_id), ...Object.fromEntries(FACETS.map((f) => [f, toNumber(s[f])])) }))
  .sort((a, b) => a.participant_id - b.participant_id);
if (people.length < N) throw new Error(`Need ${N} participants with a persona, Mini-Markers and BFI-2 scores; found ${people.length}. Run "Big Five 2 - Import Human Data" first.`);

// Span the trait range: start from the most average person, then keep adding whoever is farthest
// (in standardized trait space) from everyone already chosen.
const z = FACETS.map((f) => {
  const xs = people.map((p) => p[f]);
  const m = xs.reduce((a, b) => a + b, 0) / xs.length;
  const s = Math.sqrt(xs.reduce((a, x) => a + (x - m) ** 2, 0) / xs.length) || 1;
  return xs.map((x) => (x - m) / s);
});
const dist = (a, b) => Math.sqrt(FACETS.reduce((acc, _, k) => acc + (z[k][a] - z[k][b]) ** 2, 0));
const norm = (a) => Math.sqrt(FACETS.reduce((acc, _, k) => acc + z[k][a] ** 2, 0));
const chosen = [people.map((_, i) => i).reduce((best, i) => (norm(i) < norm(best) ? i : best), 0)];
while (chosen.length < N) {
  let best = -1;
  let bestD = -1;
  for (let i = 0; i < people.length; i++) {
    if (chosen.includes(i)) continue;
    const d = Math.min(...chosen.map((c) => dist(i, c)));
    if (d > bestD) { best = i; bestD = d; }
  }
  chosen.push(best);
}
// Shuffle, so network position does not follow the order people were picked in.
const rand = rng(seed);
for (let i = chosen.length - 1; i > 0; i--) { const j = Math.floor(rand() * (i + 1)); [chosen[i], chosen[j]] = [chosen[j], chosen[i]]; }
const sim_cast = chosen.map((c, i) => ({ key: `a${String(i + 1).padStart(2, '0')}`, agent: i + 1, name: NAMES[i], ...people[c] }));

const degrees = (edges) => adjacency(N, edges).map((s) => s.size);
const farthest = (edges, from, among) => { const d = distances(N, edges, from); return among.reduce((best, i) => (d[i] > d[best] ? i : best), among[0]); };
const everyone = Array.from({ length: N }, (_, i) => i);
const network = (key, generator, edges, roles, seedNo) => ({ key, generator, nodes: N, seed: seedNo, edges: JSON.stringify(edges), roles: JSON.stringify(roles) });

const spread = smallWorld(N, 4, 0.1, rng(seed + 1));
const host = Math.floor(rng(seed + 11)() * N);
const sparse = smallWorld(N, 2, 0.1, rng(seed + 2));
const sparseHost = Math.floor(rng(seed + 12)() * N);
const hubby = prefAttach(N, 2, rng(seed + 3));
const deg = degrees(hubby);
const hub = deg.indexOf(Math.max(...deg));
const leaves = everyone.filter((i) => deg[i] === Math.min(...deg));
const sim_networks = [
  network('propagation', 'small world: 25 nodes, 4 neighbours, 10% rewired', spread, { seed: host, decoy: farthest(spread, host, everyone) }, seed + 1),
  network('structure', 'sparse small world: 25 nodes, 2 neighbours, 10% rewired', sparse, { seed: sparseHost }, seed + 2),
  network('dynamics', 'preferential attachment: 25 nodes, 2 links per new node', hubby, { hub, peripheral: farthest(hubby, hub, leaves) }, seed + 3),
];
return [{ json: { summary: `${sim_cast.length} agents, ${sim_networks.map((w) => `${w.key}: ${JSON.parse(w.edges).length} ties`).join(', ')}`, sim_cast, sim_networks } }];
