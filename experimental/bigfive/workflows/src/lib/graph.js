// Graph helpers for the network experiments (spliced into Code nodes via `// @include lib/graph.js`).
// A graph is n nodes (0..n-1) and a list of undirected edges [a, b] with a < b.

// Seeded random numbers (mulberry32): the same seed always gives the same sequence.
function rng(seed) {
  let a = seed >>> 0;
  return () => {
    a = (a + 0x6D2B79F5) >>> 0;
    let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}
const pick = (list, rand) => list[Math.floor(rand() * list.length)];
const edgeKey = (a, b) => (a < b ? `${a}-${b}` : `${b}-${a}`);

function adjacency(n, edges) {
  const adj = Array.from({ length: n }, () => new Set());
  for (const [a, b] of edges) { adj[a].add(b); adj[b].add(a); }
  return adj;
}

// Hop distance from `start` to every node (Infinity when unreachable).
function distances(n, edges, start) {
  const adj = adjacency(n, edges);
  const dist = Array(n).fill(Infinity);
  dist[start] = 0;
  const queue = [start];
  for (let q = 0; q < queue.length; q++) for (const nb of adj[queue[q]]) if (dist[nb] === Infinity) { dist[nb] = dist[queue[q]] + 1; queue.push(nb); }
  return dist;
}
const isConnected = (n, edges) => distances(n, edges, 0).every((d) => d < Infinity);

// Watts-Strogatz small world: a ring where everyone knows k nearest neighbours (k even), each edge
// rewired with probability beta. Redrawn until connected.
function smallWorld(n, k, beta, rand) {
  for (;;) {
    const keys = new Set();
    for (let i = 0; i < n; i++) for (let j = 1; j <= k / 2; j++) keys.add(edgeKey(i, (i + j) % n));
    for (let i = 0; i < n; i++) {
      for (let j = 1; j <= k / 2; j++) {
        if (rand() >= beta) continue;
        const free = [];
        for (let c = 0; c < n; c++) if (c !== i && !keys.has(edgeKey(i, c))) free.push(c);
        if (!free.length) continue;
        keys.delete(edgeKey(i, (i + j) % n));
        keys.add(edgeKey(i, pick(free, rand)));
      }
    }
    const edges = [...keys].map((s) => s.split('-').map(Number)).sort((x, y) => x[0] - y[0] || x[1] - y[1]);
    if (isConnected(n, edges)) return edges;
  }
}

// Barabasi-Albert preferential attachment: each new node links to m existing nodes, favouring the
// well connected. Gives a few hubs and many peripheral nodes.
function prefAttach(n, m, rand) {
  const edges = [];
  const ends = [];
  for (let i = 0; i <= m; i++) for (let j = i + 1; j <= m; j++) { edges.push([i, j]); ends.push(i, j); }
  for (let v = m + 1; v < n; v++) {
    const targets = new Set();
    while (targets.size < m) targets.add(pick(ends, rand));
    for (const t of targets) { edges.push([t, v]); ends.push(t, v); }
  }
  return edges.sort((x, y) => x[0] - y[0] || x[1] - y[1]);
}

const density = (n, edges) => (2 * edges.length) / (n * (n - 1));

// Share of connected triples that are closed (global clustering coefficient).
function clustering(n, edges) {
  const adj = adjacency(n, edges);
  let triples = 0;
  let closed = 0;
  for (let v = 0; v < n; v++) {
    const nb = [...adj[v]];
    for (let i = 0; i < nb.length; i++) for (let j = i + 1; j < nb.length; j++) { triples++; if (adj[nb[i]].has(nb[j])) closed++; }
  }
  return triples ? closed / triples : 0;
}

function degreeSd(n, edges) {
  const deg = adjacency(n, edges).map((s) => s.size);
  const m = deg.reduce((a, b) => a + b, 0) / n;
  return Math.sqrt(deg.reduce((a, d) => a + (d - m) ** 2, 0) / n);
}
