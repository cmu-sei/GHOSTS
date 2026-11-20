// The network-experiments report (spliced in via `// @include lib/net_report.js`; needs lib/report.js for
// the page shell and SVG helpers, and the output of netAnalyze). Self-contained HTML, inline SVG, no JavaScript;
// every mark has a native hover title. Colour says which arm: coral = the model's agents, blue and aqua = the
// two rule arms, gray = reference.
const ARM = { llm: CORAL, ruleA: '#2A78D6', ruleB: '#1BAF7A', ref: '#94A3B8' };
const pct = (x, d = 0) => (x === null || x === undefined || Number.isNaN(x) ? '—' : `${(100 * x).toFixed(d)}%`);
const tip = (s) => `<title>${esc(s)}</title>`;
const NET_CSS = `
.legend{display:flex;flex-wrap:wrap;gap:6px 22px;margin:22px 0 -10px;font-size:13px;color:var(--slate)}
.legend i{display:inline-block;width:18px;height:3px;border-radius:2px;margin:0 7px 3px 0;vertical-align:middle}
.legend i.box{height:10px;width:10px;border-radius:2px;margin-bottom:1px}
.kicker{font:500 11px/1 "JetBrains Mono",ui-monospace,monospace;letter-spacing:.12em;text-transform:uppercase;color:var(--slate);margin:64px 0 -52px}
.cards{display:grid;grid-template-columns:repeat(3,1fr);gap:0 32px;margin:8px 0 0}
.card{border-top:1px solid var(--ink);padding:14px 0 0}
.card h3{font:400 21px/1.25 Newsreader,Georgia,serif;margin:8px 0 6px}
.card p{font-size:14px;color:var(--slate);margin:0}
.notes li{margin:6px 0;max-width:720px;color:var(--slate);font-size:15px}
.notes b{color:var(--ink);font-weight:600}
td small{color:var(--slate)}
td:first-child b{white-space:nowrap}
.card p b{color:var(--ink);font-weight:600}
.card p{line-height:1.7}
figure svg{width:auto;max-width:100%}
@media (max-width:900px){.grid{grid-template-columns:1fr}.cards{grid-template-columns:1fr}}
`;
const legend = (entries) => `<div class="legend">${entries.map(([color, label, box]) => `<span><i${box ? ' class="box"' : ''} style="background:${color}"></i>${esc(label)}</span>`).join('')}</div>`;

// Lay small charts out in a grid inside one SVG. draw(item, x, y, w, h) returns SVG for one cell.
function cells(items, cols, cellW, cellH, draw, gap = 28) {
  const rows = Math.ceil(items.length / cols);
  const inner = items.map((item, k) => draw(item, (k % cols) * (cellW + gap), Math.floor(k / cols) * (cellH + gap), cellW, cellH)).join('');
  return sv(cols * cellW + (cols - 1) * gap, rows * cellH + (rows - 1) * gap, inner);
}

// A line chart over ticks 0..ticks. series: [{ values, color, width, opacity, label, name }];
// bandFill: { low, high, color }. Labels sit at the line ends and are nudged apart.
function tickChart(x, y, w, h, { title, ticks, max, yFormat, series, bandFill, mid }) {
  const pad = { l: 44, r: 92, t: 30, b: 26 };
  const iw = w - pad.l - pad.r;
  const ih = h - pad.t - pad.b;
  const px = (t) => x + pad.l + (t / ticks) * iw;
  const py = (v) => y + pad.t + (1 - v / max) * ih;
  let out = T(x, y + 12, title, 'svg-mono');
  for (const g of [0, 0.25, 0.5, 0.75, 1]) {
    out += L(px(0), py(g * max), px(ticks), py(g * max), g === mid ? '#94A3B8' : RULE);
    out += T(px(0) - 8, py(g * max) + 4, yFormat(g * max), 'svg-label', 'end');
  }
  for (let t = 0; t <= ticks; t += 8) out += T(px(t), y + h - 6, t === 0 ? 'start' : `day ${t / 8}`, 'svg-label', t === 0 ? 'start' : t >= ticks ? 'end' : 'middle');
  const path = (values) => values.map((v, t) => (v === null ? '' : `${t && values[t - 1] !== null ? 'L' : 'M'}${px(t).toFixed(1)},${py(v).toFixed(1)}`)).join('');
  if (bandFill) {
    const top = bandFill.high.map((v, t) => `${t ? 'L' : 'M'}${px(t).toFixed(1)},${py(v).toFixed(1)}`).join('');
    const bottom = bandFill.low.map((v, t) => `L${px(t).toFixed(1)},${py(v).toFixed(1)}`).reverse().join('');
    out += `<path d="${top}${bottom}Z" fill="${bandFill.color}" fill-opacity="0.12"/>`;
  }
  for (const s of series) {
    out += `<path d="${path(s.values)}" fill="none" stroke="${s.color}" stroke-width="${s.width || 2}" stroke-opacity="${s.opacity || 1}" stroke-linejoin="round" stroke-linecap="round">${s.name ? tip(s.name) : ''}</path>`;
  }
  const ends = series.filter((s) => s.label).map((s) => ({ s, v: [...s.values].reverse().find((v) => v !== null) })).filter((e) => e.v !== undefined)
    .map((e) => ({ ...e, at: py(e.v) })).sort((a, b) => a.at - b.at);
  for (let k = 1; k < ends.length; k++) ends[k].at = Math.max(ends[k].at, ends[k - 1].at + 14);
  for (const e of ends) {
    out += `<circle cx="${px(ticks).toFixed(1)}" cy="${py(e.v).toFixed(1)}" r="4" fill="${e.s.color}" stroke="#fff" stroke-width="2">${tip(`${e.s.label}: ${yFormat(e.v)}`)}</circle>`;
    out += T(px(ticks) + 10, e.at + 4, `${e.s.label} ${yFormat(e.v)}`, 'svg-label');
  }
  return out;
}

// The 25 agents on a circle. edges: [{ a, b, color, width, title }]; nodes: [{ fill, stroke, text, title }].
function ring(x, y, size, { title, names, edges, nodes }) {
  const n = names.length;
  const cx = x + size / 2;
  const cy = y + size / 2 + 10;
  const R = size / 2 - 72;
  const at = (i, r = R) => [cx + r * Math.sin((2 * Math.PI * i) / n), cy - r * Math.cos((2 * Math.PI * i) / n)];
  let out = T(x, y + 12, title, 'svg-mono');
  for (const e of [...edges].sort((p, q) => (p.width || 1) - (q.width || 1))) {
    const [x1, y1] = at(e.a);
    const [x2, y2] = at(e.b);
    out += `<line x1="${x1.toFixed(1)}" y1="${y1.toFixed(1)}" x2="${x2.toFixed(1)}" y2="${y2.toFixed(1)}" stroke="${e.color}" stroke-width="${e.width || 1}" stroke-linecap="round">${e.title ? tip(e.title) : ''}</line>`;
  }
  nodes.forEach((node, i) => {
    const [nx, ny] = at(i);
    const side = Math.sin((2 * Math.PI * i) / n);
    // Near the top and bottom the names sit side by side: push every other one further out.
    const [lx, ly] = at(i, R + 16 + (Math.abs(side) < 0.5 && i % 2 ? 13 : 0));
    out += `<circle cx="${nx.toFixed(1)}" cy="${ny.toFixed(1)}" r="8.5" fill="${node.fill}" stroke="${node.stroke || '#fff'}" stroke-width="${node.stroke ? 1.2 : 2}">${tip(node.title)}</circle>`;
    if (node.text !== undefined) out += `<text x="${nx.toFixed(1)}" y="${(ny + 3.2).toFixed(1)}" text-anchor="middle" style="font:600 9px Inter,system-ui,sans-serif;fill:${node.textFill || '#fff'};pointer-events:none">${esc(node.text)}</text>`;
    out += T(lx, ly + 4, names[i], 'svg-label', Math.abs(side) < 0.01 ? 'middle' : side > 0 ? 'start' : 'end');
  });
  return out;
}

// One horizontal row per entry: a dot, an optional 5-95% interval, the value. Shared scale 0..max.
function dotRows(x, y, w, { head, title, rows, max, format, min = 0, zero }) {
  const pad = { l: 0, r: 44, t: head === undefined ? 30 : 50 };
  const iw = w - pad.l - pad.r;
  const px = (v) => x + pad.l + ((v - min) / (max - min)) * iw;
  let out = (head ? T(x, y + 12, head, 'svg-value') : '') + T(x, y + (head === undefined ? 12 : 32), title, 'svg-mono');
  if (zero !== undefined) out += L(px(zero), y + pad.t - 8, px(zero), y + pad.t + rows.length * 26 - 8, '#94A3B8');
  rows.forEach((r, k) => {
    const cy = y + pad.t + k * 26 + 4;
    out += L(px(min), cy, px(max), cy, RULE);
    if (r.value === null || r.value === undefined) { out += T(px(max) + 8, cy + 4, '—', 'svg-label'); return; }
    if (r.low !== null && r.low !== undefined) out += `<line x1="${px(r.low).toFixed(1)}" y1="${cy}" x2="${px(r.high).toFixed(1)}" y2="${cy}" stroke="${r.color}" stroke-width="2" stroke-linecap="round" stroke-opacity="0.55"/>`;
    out += `<circle cx="${px(r.value).toFixed(1)}" cy="${cy}" r="5" fill="${r.color}" stroke="#fff" stroke-width="2">${tip(`${r.name}: ${format(r.value)}${r.low !== null && r.low !== undefined ? ` (5–95%: ${format(r.low)} to ${format(r.high)})` : ''}`)}</circle>`;
    out += T(px(max) + 8, cy + 4, format(r.value), 'svg-label');
  });
  return out;
}

// One stacked horizontal bar per row, 2px gaps between segments, counts inside when they fit.
function stackedBars(rows, segments) {
  const W = 760;
  const labelW = 150;
  const rowH = 44;
  let out = '';
  rows.forEach((r, k) => {
    const y = k * rowH + 6;
    const total = Math.max(1, segments.reduce((a, s) => a + r.values[s.key], 0));
    out += T(0, y + 16, r.label, 'svg-label');
    let x = labelW;
    for (const s of segments) {
      const w = (r.values[s.key] / total) * (W - labelW);
      if (w <= 0) continue;
      out += `<rect x="${x.toFixed(1)}" y="${y}" width="${Math.max(0, w - 2).toFixed(1)}" height="22" rx="2" fill="${s.color}">${tip(`${r.label} · ${s.label}: ${r.values[s.key]} of ${total} (${pct(r.values[s.key] / total)})`)}</rect>`;
      if (w > 44) out += T(x + 8, y + 15, `${r.values[s.key]}`, 'svg-value', 'start', `style="fill:${s.ink || '#fff'}"`);
      x += w;
    }
  });
  return sv(W, rows.length * rowH, out);
}

const table = (head, rows) => `<table><thead><tr>${head.map((h, i) => `<th${i ? ' class="num"' : ''}>${h}</th>`).join('')}</tr></thead><tbody>${rows.map((r) => `<tr>${r.map((c, i) => `<td${i ? ' class="num"' : ''}>${c}</td>`).join('')}</tr>`).join('')}</tbody></table>`;

function networkReport(analysis, cast) {
  const names = cast.map((c) => c.name);
  const cols = analysis.length === 1 ? 1 : 2;
  const cellW = cols === 1 ? 760 : 366;
  const ringW = Math.min(cellW, 440);
  const tag = (a) => `${a.label}`;
  const who = (a) => `<b>${esc(a.label)}</b><br><small>${esc(shortModel(a.model))}</small>`;
  const withProp = analysis.filter((a) => a.propagation);
  const withStruct = analysis.filter((a) => a.structure);
  const withDyn = analysis.filter((a) => a.dynamics);
  const list = (parts) => parts.join('; ');
  let fig = 0;
  let body = '';

  // ---- At a glance ----
  const glance = [];
  const lines = (list, f) => list.map((a) => `<b>${esc(tag(a))}</b> ${f(a)}`).join('<br>');
  if (withProp.length) {
    glance.push(['01 · Propagation', 'Does the news get around?', lines(withProp, (a) => `${pct(a.propagation.final_exposed)} of agents told; the rule ${a.propagation.held_inside >= 0.8 ? 'fits' : 'does not fit'}`)]);
  }
  if (withStruct.length) {
    glance.push(['02 · Structure', 'Do agents make new ties?', lines(withStruct, (a) => `${fmt(a.structure.llm.added, 1)} new ties per run${a.structure.llm.closure === null ? '' : `, ${pct(a.structure.llm.closure)} friends of friends`}`)]);
  }
  if (withDyn.length) {
    glance.push(['03 · Social dynamics', 'Whose claim wins?', lines(withDyn, (a) => `ends at ${fmt(a.dynamics.final_mean, 0)} of 100: ${a.dynamics.final_mean < 50 ? '"cancelled"' : '"it is on"'} wins; ${a.dynamics.fit.degroot.rmse <= a.dynamics.fit.bayes.rmse ? 'DeGroot' : 'Bayes'} fits better`)]);
  }
  body += `<div class="cards">${glance.map(([eyebrow, title, text]) => `<div class="card"><div class="eyebrow">${eyebrow}</div><h3>${title}</h3><p>${text}</p></div>`).join('')}</div>`;

  // ---- 1. Propagation ----
  if (withProp.length) {
    body += `<h2>1 · Propagation</h2><p>One agent, the host, starts with a note it wrote itself: it is throwing a birthday party and wants everyone to come. Each tick every agent reads its inbox and messages its connections, or stays quiet. A judge that never sees which arm a message came from marks every message that carries the party. An agent counts as exposed from the tick such a message reaches it.</p>
<p>The comparison is a rule with no model in it: every tick, each agent that knows tells one random connection with probability <i>p</i>. That is simple contagion on the same network. <i>p</i> is fitted on the odd-numbered repeats and the rule is then checked against the even-numbered ones.</p>`;
    body += legend([[ARM.llm, 'model agents, mean of repeats'], [ARM.ruleA, 'rule at the fitted p, mean and 5–95% of 1,000 simulations'], [ARM.ref, 'no-seed control']]);
    body += figure(++fig, list(withProp.map((a) => `${esc(tag(a))} reached ${pct(a.propagation.final_exposed)} of agents, the rule ${pct(a.propagation.rule_final)}`)) + '.',
      'Share of the 25 agents that have been told about the party. Thin coral lines are single repeats. The control arm has no party at all, so anything above zero there is a false positive of agents and judge together.',
      cells(withProp, cols, cellW, 280, (a, x, y, w, h) => {
        const p = a.propagation;
        return tickChart(x, y, w, h, {
          title: `${a.label} · p = ${fmt(p.p_fit)}`, ticks: p.ticks, max: 1, yFormat: (v) => pct(v),
          bandFill: { low: p.rule.low, high: p.rule.high, color: ARM.ruleA },
          series: [
            ...p.llmCurves.map((values, k) => ({ values, color: ARM.llm, width: 1, opacity: 0.3, name: `repeat ${k + 1}` })),
            ...(p.controlCurve ? [{ values: p.controlCurve, color: ARM.ref, label: 'control', name: 'no-seed control' }] : []),
            { values: p.rule.mean, color: ARM.ruleA, label: 'rule', name: 'rule, mean of simulations' },
            { values: p.llmCurve, color: ARM.llm, width: 2.5, label: 'agents', name: 'model agents, mean' },
          ],
        });
      }));
    body += legend([[ARM.llm, 'first told by'], ['#CBD5E1', 'connection']]);
    body += figure(++fig, 'Who told whom, in the first repeat.',
      'The number in a node is the tick at which that agent was first told; 0 is the host. Empty nodes never heard. A coral line joins each agent to the one who told it first.',
      cells(withProp, cols, ringW, ringW, (a, x, y, w) => {
        const p = a.propagation;
        const r = p.runs.find((q) => q.run.arm === 'llm');
        const told = new Set(r.by.map((b, i) => (b === null ? null : edgeKey(b, i))).filter(Boolean));
        return ring(x, y, w, {
          title: `${a.label} · ${r.run.run_id}`, names,
          edges: p.edges.map(([s, t]) => (told.has(edgeKey(s, t)) ? { a: s, b: t, color: ARM.llm, width: 2.2 } : { a: s, b: t, color: '#CBD5E1' })),
          nodes: r.at.map((at, i) => (at === Infinity
            ? { fill: '#fff', stroke: '#94A3B8', title: `${names[i]}: never told` }
            : { fill: i === p.seed ? INK : ARM.llm, text: String(at), title: i === p.seed ? `${names[i]}: the host` : `${names[i]}: told at tick ${at} by ${names[r.by[i]]}` })),
        });
      }));
    const seg = [
      { key: 'exposed_yes', label: 'told, says yes', color: ARM.llm },
      { key: 'exposed_no', label: 'told, says no (forgot)', color: '#64748B' },
      { key: 'unexposed_yes', label: 'never told, says yes (made up)', color: ARM.ruleA },
      { key: 'unexposed_no', label: 'never told, says no', color: '#CBD5E1', ink: INK },
    ];
    body += legend(seg.map((s) => [s.color, s.label, true]));
    body += figure(++fig, list(withProp.map((a) => `${esc(tag(a))}: ${pct(a.propagation.recall)} of told agents remember, ${a.propagation.unexposed ? `${pct(a.propagation.hallucinated)} of untold agents say yes anyway` : 'every agent was told'}`)) + '.',
      'After the last tick each agent is interviewed in a fresh context that holds only its inbox and outbox, and asked whether it has heard about the party. The host is left out. Counts are agents over all repeats.',
      stackedBars(withProp.map((a) => ({ label: a.label, values: a.propagation.cells })), seg));
    body += table(['Model', 'Shares per tick', 'Recipients per share', 'Fitted p', 'Held-out error', 'Inside rule band', 'Open recall', 'Decoy "yes"', 'Control "yes"', 'Control flagged'],
      withProp.map((a) => { const p = a.propagation; return [who(a), pct(p.p_behavior), fmt(p.fanout, 1), fmt(p.p_fit), `${fmt(100 * p.held_rmse, 1)} pts`, pct(p.held_inside), pct(p.open_recall), pct(p.decoy_yes), p.control ? pct(p.control.cued_yes) : '—', p.control ? pct(p.control.judged_carrying, 1) : '—']; }));
    body += `<ul class="notes"><li><b>Shares per tick</b>: how often an agent that already knows passes it on in a given tick. <b>Recipients per share</b>: to how many people at once; the rule always tells exactly one.</li>
<li><b>Held-out error</b>: distance between the rule's mean curve and the repeats it was not fitted on, in percentage points of agents. <b>Inside rule band</b>: share of ticks where those repeats fall within the rule's 5–95% range.${withProp.some((a) => !a.propagation.split) ? ' Where a model has a single repeat, the rule is fitted and checked on that same repeat.' : ''}</li>
<li><b>Open recall</b>: told agents who bring the party up unprompted when asked "anything coming up?". <b>Decoy</b>: agents who say they heard about a housewarming nobody was ever told about. <b>Control</b>: the same questions and the same judge in runs where no party exists.</li></ul>`;
  }

  // ---- 2. Structure ----
  if (withStruct.length) {
    body += `<h2>2 · Structure</h2><p>The network starts sparse. An agent can ask to connect only with someone whose name has appeared in its inbox, and a tie is recorded only when the other agent accepts. Any arm that adds ties and never removes them shows density going up, so the increase proves nothing. Both rule arms are therefore tuned to add the same number of ties as the model's agents; what can differ is the shape of the network that results.</p>`;
    body += legend([[ARM.llm, 'tie formed during the run'], ['#CBD5E1', 'starting tie']]);
    body += figure(++fig, list(withStruct.map((a) => `${esc(tag(a))}: density ${fmt(a.structure.start.density, 3)} → ${fmt(a.structure.llm.density, 3)}`)) + '.',
      'The network at the end of the first repeat. Density is the share of all possible pairs that are tied, 2|E| / |V|(|V|−1), averaged over repeats.',
      cells(withStruct, cols, ringW, ringW, (a, x, y, w) => {
        const s = a.structure;
        const r = s.runs[0];
        const old = new Set(s.startEdges.map(([p, q]) => edgeKey(p, q)));
        return ring(x, y, w, {
          title: `${a.label} · ${r.run.run_id}`, names,
          edges: r.edges.map(([p, q]) => (old.has(edgeKey(p, q)) ? { a: p, b: q, color: '#CBD5E1' } : { a: p, b: q, color: ARM.llm, width: 2.2, title: `${names[p]} – ${names[q]}` })),
          nodes: names.map((name, i) => ({ fill: INK, title: `${name}: ${r.edges.filter((e) => e.includes(i)).length} ties` })),
        });
      }));
    body += legend([[ARM.llm, 'model agents'], [ARM.ruleA, 'friend-of-a-friend rule'], [ARM.ruleB, 'random-tie rule']]);
    const metric = (key, title, format, max) => ({ key, title, format, max });
    const metrics = [metric('closure', 'ties closing a triangle', (v) => pct(v), 1), metric('clustering', 'clustering at the end', (v) => fmt(v), null), metric('degree_sd', 'ties per agent, SD', (v) => fmt(v), null)];
    const dotItems = withStruct.flatMap((a) => metrics.map((m) => ({ a, m })));
    body += figure(++fig, list(withStruct.map((a) => { const s = a.structure; return s.llm.closure === null ? `${esc(tag(a))} formed no ties` : `${esc(tag(a))} closes triangles ${pct(s.llm.closure)} of the time (random ties: ${pct(s.rules.random.closure ? s.rules.random.closure.mean : null)})`; })) + '.',
      'Each row is one arm, top to bottom: model agents, friend-of-a-friend rule, random-tie rule. Dots are means; bars are the 5–95% range of 1,000 rule simulations with the same number of new ties.',
      cells(dotItems, 3, 234, 132, ({ a, m }, x, y, w) => {
        const s = a.structure;
        const rows = [
          { name: 'model agents', color: ARM.llm, value: s.llm[m.key] },
          ...['fof', 'random'].map((mode, k) => { const r = s.rules[mode][m.key]; return { name: mode === 'fof' ? 'friend-of-a-friend rule' : 'random-tie rule', color: k ? ARM.ruleB : ARM.ruleA, value: r ? r.mean : null, low: r ? r.low : null, high: r ? r.high : null }; }),
        ];
        const top = m.max || Math.max(0.05, ...rows.flatMap((r) => [r.value || 0, r.high || 0])) * 1.1;
        return dotRows(x, y, w, { head: m === metrics[0] ? a.label : '', title: m.title, rows, max: top, format: m.format });
      }, 29));
    body += table(['Model', 'Requests', 'New ties', 'Declined', 'Density start → end', 'Perceived start → end', 'Perceived ties that are real', 'Real ties perceived', 'Fake names "known"'],
      withStruct.map((a) => { const s = a.structure; return [who(a), fmt(s.llm.requests, 1), fmt(s.llm.added, 1), fmt(s.llm.declined, 1), `${fmt(s.start.density, 3)} → ${fmt(s.llm.density, 3)}`, `${fmt(s.llm.perceived_start, 3)} → ${fmt(s.llm.perceived_end, 3)}`, pct(s.llm.precision), pct(s.llm.recall), `${fmt(s.llm.fakes_start, 1)} → ${fmt(s.llm.fakes_end, 1)}`]; }));
    body += `<ul class="notes"><li><b>Perceived ties</b> come from the interview: each agent, with only its inbox and outbox to go on, is shown the 24 other names and 5 names that do not exist, and asked whom it knows. A pair counts as tied when both name each other. The interview is given before the first tick and after the last.</li>
<li><b>Fake names "known"</b>: how many of the 5 invented names an agent claims to know, on average, at the start and at the end.</li>
<li>Requests, new ties and declines are per run, averaged over repeats. A request the other agent neither accepts nor declines lapses after one tick.</li></ul>`;
  }

  // ---- 3. Social dynamics ----
  if (withDyn.length) {
    body += `<h2>3 · Social dynamics</h2><p>Everyone has heard that Riley might have a party on Saturday. One agent starts out certain it is on, another certain it is cancelled; one of them sits at the hub of the network and the other at its edge, and the two swap places across runs. Riley is not in the network, so nobody can check. After every tick each agent is asked, in a separate call it never sees again, how likely the party is, from 0 to 100.</p>
<p>Two rules read the very same messages. <b>Bayes</b> is the update GHOSTS runs today: a message containing a keyword moves the belief of its author and its reader by a fixed step. <b>DeGroot</b> moves each agent to the average of its own belief and the beliefs of whoever messaged it about the party.</p>`;
    body += legend([[ARM.llm, 'model agents (thin: single agents, first run)'], [ARM.ruleA, 'GHOSTS Bayes update'], [ARM.ruleB, 'DeGroot averaging']]);
    const CONDITION = { on_at_hub: '"it is on" starts at the hub', off_at_hub: '"cancelled" starts at the hub' };
    const dynItems = withDyn.flatMap((a) => Object.keys(CONDITION).filter((c) => a.dynamics.conditions[c]).map((c) => ({ a, c })));
    body += figure(++fig, list(withDyn.map((a) => { const d = a.dynamics; return d.position_gap === null ? `${esc(tag(a))} ends at ${fmt(d.final_mean, 0)}` : `${esc(tag(a))}: starting at the hub is worth ${signed(d.position_gap, 0)} points to "it is on"`; })) + '.',
      'Belief that the party will happen, 0–100, mean over agents and runs. The line at 50 is "no idea". Rule lines are computed from the messages the agents actually sent.',
      cells(dynItems, 2, 366, 280, ({ a, c }, x, y, w, h) => {
        const d = a.dynamics;
        const first = d.runs.find((r) => r.run.condition === c);
        const m = d.conditions[c].mean;
        return tickChart(x, y, w, h, {
          title: `${a.label} · ${CONDITION[c]}`, ticks: d.ticks, max: 100, mid: 0.5, yFormat: (v) => fmt(v, 0),
          series: [
            ...first.llm.map((values, i) => ({ values, color: ARM.llm, width: 1, opacity: 0.22, name: names[i] })),
            { values: m.bayes, color: ARM.ruleA, label: 'Bayes', name: 'GHOSTS Bayes update, mean' },
            { values: m.degroot, color: ARM.ruleB, label: 'DeGroot', name: 'DeGroot averaging, mean' },
            { values: m.llm, color: ARM.llm, width: 2.5, label: 'agents', name: 'model agents, mean' },
          ],
        });
      }));
    body += table(['Model', 'Final belief, "on" at hub', 'Final belief, "cancelled" at hub', 'Spread at end (SD)', 'Believe / doubt', 'Error vs Bayes', 'Error vs DeGroot', 'r Bayes', 'r DeGroot'],
      withDyn.map((a) => {
        const d = a.dynamics;
        const c = d.conditions;
        const share = (k) => mean(d.runs.map((r) => r[k]));
        return [who(a), c.on_at_hub ? fmt(c.on_at_hub.final_mean, 0) : '—', c.off_at_hub ? fmt(c.off_at_hub.final_mean, 0) : '—', fmt(d.final_sd, 0), `${pct(share('final_on'))} / ${pct(share('final_off'))}`, fmt(d.fit.bayes.rmse, 1), fmt(d.fit.degroot.rmse, 1), fmt(d.fit.bayes.r), fmt(d.fit.degroot.r)];
      }));
    body += `<ul class="notes"><li><b>Spread at end</b>: standard deviation of the agents' final beliefs. Near zero is consensus; a large value with both believers and doubters is a split. <b>Believe / doubt</b>: share of agents ending above / below 50.</li>
<li><b>Error</b>: root-mean-square distance, in belief points, between what an agent said and what the rule predicts for that agent at that tick. <b>r</b>: their correlation. Lower error and higher r mean the rule describes the agents better.</li>
<li>Bayes starts everyone at 50 and uses likelihood 0.6, as GHOSTS does. DeGroot starts from the agents' own answers before the first tick.</li></ul>`;
  }

  // ---- Traits ----
  const withTraits = analysis.filter((a) => a.traits.n > 3);
  if (withTraits.length) {
    body += `<h2>4 · The person behind the agent</h2><p>Each agent is built from one real participant's answers to the BFI-2. If the model acts like that person and not just answers like them, the person's own trait scores should predict what the agent does. Here: how often the agent messages anyone at all, over every run.</p>`;
    body += figure(++fig, list(withTraits.map((a) => { const best = FACETS.reduce((b, f) => (Math.abs(a.traits.facets[f].r ?? 0) > Math.abs(a.traits.facets[b].r ?? 0) ? f : b), FACETS[0]); return `${esc(tag(a))}: strongest link is ${FACET_LABEL[best].toLowerCase()}, r = ${signed(a.traits.facets[best].r)}`; })) + '.',
      'Correlation between each participant\'s own trait score and the share of ticks in which their agent sent a message. Bars are 95% intervals; with 25 agents they are wide, and an interval that crosses zero is no evidence of a link.',
      cells(withTraits, cols, cellW, 170, (a, x, y, w) => {
        const labelW = 150;
        let out = '';
        FACETS.forEach((f, k) => { out += T(x, y + 38 + k * 26, FACET_LABEL[f], 'svg-label'); });
        return out + dotRows(x + labelW, y, w - labelW, {
          title: `${a.label} · r with activity`, min: -1, max: 1, zero: 0, format: (v) => signed(v),
          rows: FACETS.map((f) => ({ name: FACET_LABEL[f], color: ARM.llm, value: a.traits.facets[f].r, low: a.traits.facets[f].low, high: a.traits.facets[f].high })),
        });
      }));
  }

  // ---- Method ----
  body += `<h2>How it was run</h2><ul class="notes">
<li><b>Agents.</b> 25 participants chosen to span the Big Five trait range. Each agent is told its name and given that participant's own BFI-2 answers as its description of itself. The same 25 sit in the same network positions for every model and every repeat.</li>
<li><b>Networks.</b> Generated once from fixed parameters and a seed, then saved: a small world for propagation, a sparse small world for structure, a hub-and-periphery network for dynamics.</li>
<li><b>Ticks.</b> 8 per simulated day. Ticks are simultaneous: every agent decides on what it had received before the tick, and what it sends arrives for the next one. Agents can message only their connections; anything else is dropped.</li>
<li><b>Judge.</b> One model labels every message, seeing only the text. ${esc([...new Set(analysis.map((a) => shortModel(a.judge_model)))].join(', '))}.</li>
<li><b>Model settings.</b> Temperature is each model's own default unless a run sets one. Bedrock models run with their default reasoning; local Ollama models run with thinking switched off, which is about ten times faster per call.</li>
<li><b>Rule arms.</b> They need no model, so they are simulated 1,000 times each on the same saved network instead of being run once.</li>
<li><b>Limits.</b> 25 agents, one network per experiment and a handful of repeats: read differences between models as indications, not estimates. The rules stand in for how people are usually modelled; no human sharing data is involved.</li></ul>`;

  const runs = analysis.reduce((a, x) => a + x.runs.length, 0);
  return page({
    eyebrow: 'GHOSTS · Network experiments',
    title: 'Twenty-five digital twins, <em>one rumor</em>',
    lede: 'Three experiments put the same 25 persona agents in a network and watch what a language model does with them: whether news spreads, whether new ties form, and how belief moves when two claims compete. Each is set against simple rules of the kind used to model people.',
    facts: [
      { label: 'Models', html: analysis.map((a) => `${esc(a.label)}<br><small>${esc(shortModel(a.model))}</small>`).join('<br>') },
      { label: 'Agents', value: String(cast.length) },
      { label: 'Runs', value: String(runs) },
      { label: 'Model calls', value: analysis.reduce((a, x) => a + x.calls, 0).toLocaleString('en-US') },
      { label: 'Messages sent', value: analysis.reduce((a, x) => a + x.messages, 0).toLocaleString('en-US') },
      { label: 'Failed calls', html: `${analysis.reduce((a, x) => a + x.failures, 0)} <small>counted as staying quiet</small>` },
    ],
    sections: body,
    footer: `Generated by "Network 4 - Analyze and Report" on ${new Date().toISOString().slice(0, 10)}. Data: network-runs.csv, network-agents.csv. Individual-level results for the same participants: all-runs.html.`,
  }).replace('</style>', `${NET_CSS}</style>`);
}

// One row per run for network-runs.csv.
function networkRunRows(analysis) {
  const rows = [];
  for (const a of analysis) {
    for (const run of a.runs) {
      const p = a.propagation && a.propagation.runs.find((r) => r.run.run_id === run.run_id);
      const s = a.structure && a.structure.runs.find((r) => r.run.run_id === run.run_id);
      const d = a.dynamics && a.dynamics.runs.find((r) => r.run.run_id === run.run_id);
      rows.push({
        run_id: run.run_id, label: run.label, model: run.model, experiment: run.experiment, arm: run.arm, condition: run.condition, repeat: run.repeat,
        ticks: run.ticks, calls: run.calls, failures: run.failures, messages: run.messages,
        final_exposed: p ? p.curve[p.curve.length - 1] : '', carrying_messages: p ? p.carrying : '',
        ties_added: s ? s.added : '', end_density: s ? s.end.density : '', end_clustering: s ? s.end.clustering : '', closure: s ? s.closure : '', perceived_density: s ? s.perceived_end : '',
        final_belief: d ? d.final_mean : '', final_belief_sd: d ? d.final_sd : '', rmse_bayes: d ? d.fit.bayes.rmse : '', rmse_degroot: d ? d.fit.degroot.rmse : '',
      });
    }
  }
  return rows;
}
