// Report rendering (spliced into Code nodes by build.mjs via `// @include lib/report.js`).
// Self-contained HTML, inline SVG, no JavaScript. Editorial look: serif display headings, mono coral
// eyebrows, hairline rules, direct labels instead of legends, a facts sidebar, captioned figures.
const FACET_LABEL = { extraversion: 'Extraversion', agreeableness: 'Agreeableness', conscientiousness: 'Conscientiousness', emotional_stability: 'Emotional stability', openness: 'Openness' };
const INK = '#0F172A';
const SLATE = '#64748B';
const RULE = '#E2E8F0';
const CORAL = '#F0524D';
const SERIES = '#94A3B8';

const esc = (s) => String(s ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#x27;' }[c]));
const fmt = (x, d = 2) => (x === null || x === undefined || Number.isNaN(Number(x)) ? '—' : Number(x).toFixed(d));
const signed = (x, d = 2) => (x === null || x === undefined ? '—' : `${Number(x) > 0 ? '+' : Number(x) < 0 ? '−' : ''}${Math.abs(Number(x)).toFixed(d)}`);
const pfmt = (p) => (p === null || p === undefined ? '—' : Number(p) < 0.001 ? '< .001' : `${Number(p).toFixed(3).replace(/^0/, '')}`);
const shortModel = (model) => {
  const m = String(model || '');
  if (/anthropic\./.test(m)) return m.replace(/^.*anthropic\./, '').replace(/-\d{8}.*$/, '');
  return m;
};
const csvLine = (vals) => vals.map((v) => {
  const s = v === null || v === undefined ? '' : String(v);
  return /[",\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
}).join(',');
const toCsv = (rows, columns) => [csvLine(columns), ...rows.map((r) => csvLine(columns.map((c) => r[c])))].join('\n') + '\n';

const CSS = `
:root{--ink:${INK};--slate:${SLATE};--rule:${RULE};--coral:${CORAL}}
*{box-sizing:border-box}body{margin:0;background:#fff;color:var(--ink);font:16px/1.6 Inter,system-ui,-apple-system,"Segoe UI",sans-serif}
.page{max-width:1120px;margin:0 auto;padding:72px 40px 96px}
.eyebrow{font:500 12px/1 "JetBrains Mono",ui-monospace,SFMono-Regular,Menlo,monospace;letter-spacing:.14em;text-transform:uppercase;color:var(--coral)}
h1{font:400 52px/1.1 Newsreader,Georgia,"Times New Roman",serif;letter-spacing:-.01em;margin:18px 0 12px;max-width:820px}
h1 em{font-style:italic;color:var(--coral)}
h2{font:400 30px/1.2 Newsreader,Georgia,serif;margin:64px 0 10px}
.lede{font-size:19px;color:var(--slate);max-width:720px;margin:0 0 48px}
.grid{display:grid;grid-template-columns:1fr 240px;gap:56px;align-items:start}
aside{border-top:1px solid var(--ink);padding-top:16px;position:sticky;top:24px}
aside .fact{padding:12px 0;border-bottom:1px solid var(--rule)}
aside .label{font:500 11px/1 "JetBrains Mono",ui-monospace,monospace;letter-spacing:.12em;text-transform:uppercase;color:var(--slate)}
aside .value{font:400 26px/1.2 Newsreader,Georgia,serif;margin-top:6px;word-break:break-word}
aside .value small{font-size:15px;color:var(--slate);font-family:Inter,system-ui,sans-serif}
figure{margin:28px 0 0}figure svg{display:block;width:100%;height:auto}
figcaption{margin-top:14px;font-size:14px;color:var(--slate);max-width:720px}
figcaption .fig{font:500 11px/1 "JetBrains Mono",ui-monospace,monospace;letter-spacing:.12em;color:var(--coral);margin-right:10px}
figcaption b{color:var(--ink);font-weight:600}
p{max-width:720px}
table{width:100%;border-collapse:collapse;margin-top:20px;font-size:14px;font-variant-numeric:tabular-nums}
th{text-align:left;font:500 11px/1.4 "JetBrains Mono",ui-monospace,monospace;letter-spacing:.1em;text-transform:uppercase;color:var(--slate);padding:10px 12px 10px 0;border-bottom:1px solid var(--ink)}
td{padding:10px 12px 10px 0;border-bottom:1px solid var(--rule);vertical-align:top}
td.num,th.num{text-align:right;padding-right:0;padding-left:12px}
tr.hi td{color:var(--coral);font-weight:600}
.svg-label{font:12px Inter,system-ui,sans-serif;fill:${SLATE}}
.svg-mono{font:11px "JetBrains Mono",ui-monospace,monospace;fill:${SLATE};letter-spacing:.08em}
.svg-value{font:13px Inter,system-ui,sans-serif;fill:${INK};font-weight:600}
.svg-serif{font:22px Newsreader,Georgia,serif;fill:${INK}}
footer{margin-top:80px;padding-top:16px;border-top:1px solid var(--rule);font-size:13px;color:var(--slate)}
@media print{.page{padding:24px}aside{position:static}}
`;

function page({ eyebrow, title, lede, facts, sections, footer }) {
  const factHtml = facts.map((f) => `<div class="fact"><div class="label">${esc(f.label)}</div><div class="value">${f.html || esc(f.value)}</div></div>`).join('');
  return `<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>${esc(title.replace(/<[^>]+>/g, ''))}</title>
<link rel="preconnect" href="https://fonts.googleapis.com"><link href="https://fonts.googleapis.com/css2?family=Newsreader:ital,opsz,wght@0,6..72,400;1,6..72,400&family=Inter:wght@400;500;600&family=JetBrains+Mono:wght@500&display=swap" rel="stylesheet">
<style>${CSS}</style></head>
<body><div class="page">
<div class="eyebrow">${esc(eyebrow)}</div>
<h1>${title}</h1>
<p class="lede">${lede}</p>
<div class="grid"><main>${sections}</main><aside>${factHtml}</aside></div>
<footer>${footer}</footer>
</div></body></html>`;
}

const figure = (n, finding, explanation, svg) => `<figure>${svg}<figcaption><span class="fig">FIG. ${n}</span><b>${finding}</b> ${explanation}</figcaption></figure>`;

// --- SVG helpers -------------------------------------------------------------------------------
const sv = (w, h, inner) => `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${w} ${h}" width="${w}" height="${h}" role="img">${inner}</svg>`;
const T = (x, y, s, cls = 'svg-label', anchor = 'start', extra = '') => `<text x="${x.toFixed(1)}" y="${y.toFixed(1)}" class="${cls}" text-anchor="${anchor}" ${extra}>${esc(s)}</text>`;
const L = (x1, y1, x2, y2, stroke = RULE, extra = '') => `<line x1="${x1.toFixed(1)}" y1="${y1.toFixed(1)}" x2="${x2.toFixed(1)}" y2="${y2.toFixed(1)}" stroke="${stroke}" ${extra}/>`;

// Five small agent-vs-human scatter panels on a 1-9 scale with the identity line and r.
function scatterPanels(comps, summary) {
  const P = 196;
  const pad = { l: 30, r: 10, t: 42, b: 30 };
  const inner = P - pad.l - pad.r;
  const W = FACETS.length * P + 10;
  const H = P + 8;
  const scale = (v) => ((v - 1) / 8) * inner;
  let out = '';
  FACETS.forEach((f, i) => {
    const ox = i * P + pad.l;
    const oy = pad.t;
    const x0 = ox;
    const y0 = oy + inner;
    out += `<rect x="${x0}" y="${oy}" width="${inner}" height="${inner}" fill="none" stroke="${RULE}"/>`;
    out += L(x0, y0, x0 + inner, oy, '#CBD5E1', 'stroke-dasharray="3 3"');
    for (const c of comps) {
      const x = x0 + scale(Number(c[`human_${f}`]));
      const y = y0 - scale(Number(c[`agent_${f}`]));
      out += `<circle cx="${x.toFixed(1)}" cy="${y.toFixed(1)}" r="2.2" fill="${CORAL}" fill-opacity="0.35"/>`;
    }
    out += T(x0, oy - 26, FACET_LABEL[f], 'svg-mono');
    out += T(x0, oy - 8, `r = ${fmt(summary[`${f}_r`])}  [${fmt(summary[`${f}_r_ci_low`])}, ${fmt(summary[`${f}_r_ci_high`])}]`, 'svg-value');
    out += T(x0, y0 + 14, '1', 'svg-label');
    out += T(x0 + inner, y0 + 14, '9', 'svg-label', 'end');
    out += T(x0 + inner / 2, y0 + 26, 'human', 'svg-label', 'middle');
    if (i === 0) out += T(x0 - 6, oy + inner / 2, 'agent', 'svg-label', 'end', 'transform="rotate(-90 ' + (x0 - 6).toFixed(1) + ' ' + (oy + inner / 2).toFixed(1) + ')"');
  });
  return sv(W, H, out);
}

// Horizontal mean-shift bars per facet (agent minus human); the largest |shift| is highlighted.
function shiftBars(summary) {
  const W = 760;
  const rowH = 38;
  const H = FACETS.length * rowH + 44;
  const labelW = 170;
  const mid = labelW + (W - labelW - 60) / 2;
  const half = (W - labelW - 60) / 2;
  const shifts = FACETS.map((f) => Number(summary[`${f}_shift`] ?? 0));
  const maxAbs = Math.max(0.5, ...shifts.map(Math.abs));
  const px = (v) => (v / maxAbs) * half;
  const largest = shifts.reduce((bi, s, i) => (Math.abs(s) > Math.abs(shifts[bi]) ? i : bi), 0);
  let out = L(mid, 10, mid, H - 30, INK);
  out += T(mid, H - 10, '0', 'svg-mono', 'middle');
  out += T(mid - half, H - 10, `−${maxAbs.toFixed(1)}`, 'svg-mono', 'middle');
  out += T(mid + half, H - 10, `+${maxAbs.toFixed(1)}`, 'svg-mono', 'middle');
  FACETS.forEach((f, i) => {
    const y = 16 + i * rowH;
    const w = px(shifts[i]);
    const color = i === largest ? CORAL : SERIES;
    out += T(0, y + 16, FACET_LABEL[f], 'svg-label');
    out += `<rect x="${(w < 0 ? mid + w : mid).toFixed(1)}" y="${y}" width="${Math.abs(w).toFixed(1)}" height="22" fill="${color}"/>`;
    out += T(w < 0 ? mid + w - 8 : mid + w + 8, y + 16, signed(shifts[i]), 'svg-value', w < 0 ? 'end' : 'start');
  });
  return sv(W, H, out);
}

// One stacked bar: participants answered ok / failed / answered but not comparable (no human Mini-Markers).
function qualityBar(run, compared) {
  const W = 760;
  const H = 86;
  const ok = Number(run.participants_done || 0);
  const failed = Number(run.participants_failed || 0);
  const total = Math.max(1, ok + failed);
  const notComparable = Math.max(0, ok - compared);
  const segs = [
    ['compared', compared, CORAL],
    ['answered, no human Mini-Markers', notComparable, SERIES],
    ['failed to parse', failed, '#CBD5E1'],
  ];
  let x = 0;
  let out = '';
  segs.forEach(([label, n, color], i) => {
    const w = (n / total) * W;
    if (w > 0) {
      out += `<rect x="${x.toFixed(1)}" y="20" width="${w.toFixed(1)}" height="28" fill="${color}"/>`;
      if (w > 60) out += T(x + 8, 39, `${n}`, 'svg-value', 'start', 'fill="#fff"');
    }
    const lx = i * 260;
    out += `<rect x="${lx}" y="64" width="10" height="10" fill="${color}"/>`;
    out += T(lx + 16, 73, `${label}: ${n}`, 'svg-label');
    x += Math.max(w, 0);
  });
  return sv(W, H, out);
}

// Ranked mean absolute difference per run, best first.
function rankedBars(summaries) {
  const W = 760;
  const rowH = 36;
  const labelW = 230;
  const H = summaries.length * rowH + 30;
  const maxV = Math.max(1, ...summaries.map((s) => Number(s.mean_abs_diff)));
  let out = '';
  summaries.forEach((s, i) => {
    const y = 8 + i * rowH;
    const w = (Number(s.mean_abs_diff) / maxV) * (W - labelW - 80);
    out += T(0, y + 16, shortModel(s.model) || s.run_id, 'svg-label');
    out += T(0, y + 28, s.run_id, 'svg-mono');
    out += `<rect x="${labelW}" y="${y}" width="${w.toFixed(1)}" height="22" fill="${i === 0 ? CORAL : SERIES}"/>`;
    out += T(labelW + w + 8, y + 16, `${fmt(s.mean_abs_diff, 3)}  (n = ${s.n})`, 'svg-value');
  });
  return sv(W, H, out);
}

// Per-facet r for every run (gray), the best run in coral, the BFI-2 baseline as a dashed reference.
function facetLines(summaries, best, baselineFrom) {
  const W = 760;
  const H = 300;
  const pad = { l: 44, r: 150, t: 20, b: 40 };
  const iw = W - pad.l - pad.r;
  const ih = H - pad.t - pad.b;
  const xs = FACETS.map((_, i) => pad.l + (i / (FACETS.length - 1)) * iw);
  const y = (r) => pad.t + (1 - Math.max(0, Math.min(1, r))) * ih;
  let out = '';
  for (const g of [0, 0.25, 0.5, 0.75, 1]) {
    out += L(pad.l, y(g), pad.l + iw, y(g), RULE);
    out += T(pad.l - 8, y(g) + 4, g.toFixed(2), 'svg-mono', 'end');
  }
  FACETS.forEach((f, i) => { out += T(xs[i], H - 14, FACET_LABEL[f], 'svg-label', 'middle'); });
  const baseline = FACETS.map((f) => Number(baselineFrom[`${f}_baseline_r`]));
  const bestLast = y(Number(best[`${FACETS[FACETS.length - 1]}_r`]));
  if (baseline.every((v) => Number.isFinite(v))) {
    out += `<polyline fill="none" stroke="${INK}" stroke-dasharray="4 4" points="${FACETS.map((_, i) => `${xs[i].toFixed(1)},${y(baseline[i]).toFixed(1)}`).join(' ')}"/>`;
    let ly = y(baseline[baseline.length - 1]) + 4;
    if (Math.abs(ly - (bestLast + 4)) < 16) ly = bestLast + 4 + (ly >= bestLast + 4 ? 16 : -16); // keep clear of the best run's label
    out += T(pad.l + iw + 8, ly, 'BFI-2 baseline', 'svg-label');
  }
  for (const s of summaries) {
    const isBest = s.run_id === best.run_id;
    const pts = FACETS.map((f, i) => `${xs[i].toFixed(1)},${y(Number(s[`${f}_r`])).toFixed(1)}`).join(' ');
    out += `<polyline fill="none" stroke="${isBest ? CORAL : SERIES}" stroke-width="${isBest ? 2.5 : 1.5}" points="${pts}"/>`;
    const last = Number(s[`${FACETS[FACETS.length - 1]}_r`]);
    if (isBest) out += T(pad.l + iw + 8, y(last) + 4, shortModel(s.model) || s.run_id, 'svg-value', 'start', `fill="${CORAL}"`);
  }
  return sv(W, H, out);
}

// Shift table shaded relative to the largest |shift| across runs and facets.
function shiftHeat(summaries) {
  const vals = summaries.flatMap((s) => FACETS.map((f) => Math.abs(Number(s[`${f}_shift`] ?? 0))));
  const maxAbs = Math.max(0.01, ...vals);
  const cell = (v) => {
    const a = Math.min(1, Math.abs(Number(v ?? 0)) / maxAbs);
    const bg = Number(v) >= 0 ? `rgba(240,82,77,${(a * 0.55).toFixed(2)})` : `rgba(15,23,42,${(a * 0.45).toFixed(2)})`;
    return `<td class="num" style="background:${bg}">${signed(v)}</td>`;
  };
  return `<table><thead><tr><th>run</th>${FACETS.map((f) => `<th class="num">${FACET_LABEL[f]}</th>`).join('')}</tr></thead><tbody>${summaries
    .map((s) => `<tr><td>${esc(shortModel(s.model) || s.run_id)}<br><span class="eyebrow" style="color:${SLATE}">${esc(s.run_id)}</span></td>${FACETS.map((f) => cell(s[`${f}_shift`])).join('')}</tr>`)
    .join('')}</tbody></table>`;
}

// --- Reports -----------------------------------------------------------------------------------
function shiftFinding(over, under) {
  const list = (xs) => xs.map(([f, s]) => `${FACET_LABEL[f].toLowerCase()} (${signed(s)})`).join(', ');
  if (over.length && under.length) return `Agents overshoot ${list(over)} and undershoot ${list(under)}.`;
  if (over.length) return `Agents overshoot ${list(over)}.`;
  if (under.length) return `Agents undershoot ${list(under)}.`;
  return 'Agents stay within ±0.1 of their humans on every facet.';
}

function runReport(summary, comps, run) {
  const name = shortModel(summary.model) || summary.run_id;
  const rs = FACETS.map((f) => [f, Number(summary[`${f}_r`])]).filter(([, r]) => Number.isFinite(r));
  const bestR = rs.reduce((a, b) => (b[1] > a[1] ? b : a), rs[0] || ['—', NaN]);
  const worstR = rs.reduce((a, b) => (b[1] < a[1] ? b : a), rs[0] || ['—', NaN]);
  const shifts = FACETS.map((f) => [f, Number(summary[`${f}_shift`])]).sort((a, b) => Math.abs(b[1]) - Math.abs(a[1]));
  const over = shifts.filter(([, s]) => s > 0.1);
  const under = shifts.filter(([, s]) => s < -0.1);
  const beats = FACETS.filter((f) => Number(summary[`${f}_r`]) > Number(summary[`${f}_baseline_r`]) && Number(summary[`${f}_steiger_p`]) < 0.05);
  const total = Number(run?.participants_done || 0) + Number(run?.participants_failed || 0);

  const sections = `
<h2>Does the agent move with its human?</h2>
<p>Each dot is one participant: their own Mini-Marker facet score against the score of the agent that was given their BFI-2 answers. The dashed line is perfect agreement; r is the Pearson correlation with its Fisher 95% interval.</p>
${figure(1, `Agents track humans best on ${FACET_LABEL[bestR[0]]?.toLowerCase() ?? '—'} (r = ${fmt(bestR[1])}) and worst on ${FACET_LABEL[worstR[0]]?.toLowerCase() ?? '—'} (r = ${fmt(worstR[1])}).`, `${summary.n} participants with both an agent answer and a human Mini-Marker row. The dashed BFI-2 baseline in the table below is how well each person's own BFI-2 predicts their Mini-Markers.`, scatterPanels(comps, summary))}
<h2>Where do the agents run hot or cold?</h2>
<p>Mean shift is agent minus human on the 1–9 Mini-Marker scale, averaged over participants. Positive means the agents describe themselves as more of that trait than their humans did.</p>
${figure(2, shiftFinding(over, under), `Mean absolute difference across all five facets is ${fmt(summary.mean_abs_diff, 3)}. The largest shift is highlighted; p-values for the shifts are in the table, Holm-corrected across the five facets.`, shiftBars(summary))}
<h2>How clean was the run itself?</h2>
${figure(3, `${run?.participants_done ?? '—'} of ${total || '—'} turns parsed cleanly; ${run?.participants_failed ?? 0} failed.`, 'A turn parses cleanly when all 40 adjectives come back exactly once with a value from 1 to 9. Participants whose human Mini-Marker row was blank are answered by the agent but cannot be compared.', qualityBar(run || {}, summary.n))}
<h2>By the numbers</h2>
<table><thead><tr><th>facet</th><th class="num">shift</th><th class="num">p (Holm)</th><th class="num">MAE</th><th class="num">r [95% CI]</th><th class="num">BFI-2 baseline r</th><th class="num">Steiger p</th></tr></thead><tbody>
${FACETS.map((f) => `<tr${beats.includes(f) ? ' class="hi"' : ''}><td>${FACET_LABEL[f]}</td><td class="num">${signed(summary[`${f}_shift`])}</td><td class="num">${pfmt(summary[`${f}_shift_p_holm`])}</td><td class="num">${fmt(summary[`${f}_mae`])}</td><td class="num">${fmt(summary[`${f}_r`])} [${fmt(summary[`${f}_r_ci_low`])}, ${fmt(summary[`${f}_r_ci_high`])}]</td><td class="num">${fmt(summary[`${f}_baseline_r`])}</td><td class="num">${pfmt(summary[`${f}_steiger_p`])}</td></tr>`).join('')}
</tbody></table>
<p style="font-size:13px;color:${SLATE}">Steiger's test compares the agent's r with the BFI-2 baseline r on the same participants (dependent correlations sharing the human Mini-Marker score). ${beats.length ? `Highlighted rows: the agent predicts the human's Mini-Markers better than the human's own BFI-2 does (${beats.map((f) => FACET_LABEL[f].toLowerCase()).join(', ')}).` : 'No facet beats the baseline at p < .05.'}</p>`;

  return page({
    eyebrow: `Big Five study · run ${summary.run_id}`,
    title: `How closely does <em>${esc(name)}</em> answer like the person it was given?`,
    lede: `Each agent was handed one real participant's BFI-2 answers as its persona and asked the 40 Saucier Mini-Markers. Here is how its answers line up with what that participant actually said.`,
    facts: [
      { label: 'run', value: summary.run_id },
      { label: 'model', html: `<small>${esc(summary.model)}</small>` },
      { label: 'provider', value: summary.provider || '—' },
      { label: 'compared participants', value: String(summary.n) },
      { label: 'mean abs. difference', value: fmt(summary.mean_abs_diff, 3) },
      { label: 'median r across facets', value: fmt(rs.map(([, r]) => r).sort((a, b) => a - b)[Math.floor(rs.length / 2)] ?? null) },
      { label: 'analyzed', value: String(summary.analyzed_at || '').slice(0, 10) },
    ],
    sections,
    footer: `Generated by the n8n workflow “Big Five 5 - Analyze and Report”. Scoring: facet = mean of its items, reverse items as 10 − value; BFI-2 emotional stability = 6 − negative emotionality.`,
  });
}

function allRunsReport(summaries, compsByRun) {
  const ranked = [...summaries].sort((a, b) => Number(a.mean_abs_diff) - Number(b.mean_abs_diff));
  const best = ranked[0];
  const largest = [...ranked].sort((a, b) => Number(b.n) - Number(a.n))[0]; // baseline from the run with the most participants
  const avgR = FACETS.map((f) => [f, mean(ranked.map((s) => Number(s[`${f}_r`])).filter(Number.isFinite))]);
  const hiR = avgR.reduce((a, b) => (b[1] > a[1] ? b : a));
  const loR = avgR.reduce((a, b) => (b[1] < a[1] ? b : a));
  const largestShift = ranked.flatMap((s) => FACETS.map((f) => [s, f, Number(s[`${f}_shift`])])).sort((a, b) => Math.abs(b[2]) - Math.abs(a[2]))[0];
  const participants = new Set(Object.values(compsByRun).flat().map((c) => String(c.participant_id))).size;

  // Paired comparison of every run against the best, on participants both runs compared.
  const bestMad = new Map((compsByRun[best.run_id] || []).map((c) => [String(c.participant_id), Number(c.mean_abs_diff)]));
  const paired = ranked.map((s) => {
    if (s.run_id === best.run_id) return { n: bestMad.size, diff: null, p: null };
    const mine = (compsByRun[s.run_id] || []).filter((c) => bestMad.has(String(c.participant_id)));
    const t = pairedT(mine.map((c) => Number(c.mean_abs_diff)), mine.map((c) => bestMad.get(String(c.participant_id))));
    return { n: mine.length, diff: t.mean, p: t.p };
  });

  const sections = `
<h2>Who lands closest?</h2>
<p>Mean absolute difference between agent and human facet scores on the 1–9 Mini-Marker scale, averaged over facets and participants. Lower is closer.</p>
${figure(1, ranked.length > 1 ? `${esc(shortModel(best.model) || best.run_id)} lands closest (${fmt(best.mean_abs_diff, 3)}), ${esc(shortModel(ranked[ranked.length - 1].model) || ranked[ranked.length - 1].run_id)} farthest (${fmt(ranked[ranked.length - 1].mean_abs_diff, 3)}).` : `${esc(shortModel(best.model) || best.run_id)} is the only run so far (${fmt(best.mean_abs_diff, 3)}).`, 'Runs are ordered best to worst. Each run’s n is the number of participants with both an agent answer and a human Mini-Marker row.', rankedBars(ranked))}
<h2>Do they rank people the same way humans do?</h2>
<p>Correlation between agent and human facet scores across participants. The dashed reference is the BFI-2 baseline: how well each person’s own BFI-2 predicts their Mini-Markers.</p>
${figure(2, `Averaged across models, agreement with humans is highest on ${FACET_LABEL[hiR[0]].toLowerCase()} (r = ${fmt(hiR[1])}) and lowest on ${FACET_LABEL[loR[0]].toLowerCase()} (r = ${fmt(loR[1])}).`, `The highlighted line is the run with the lowest mean absolute difference; the baseline is taken from ${esc(largest.run_id)} (n = ${largest.n}).`, facetLines(ranked, best, largest))}
<h2>Where does each model run hot or cold?</h2>
<p>Mean shift, agent minus human, per facet. Coral cells overshoot, dark cells undershoot.</p>
${figure(3, largestShift ? `The largest shift is ${esc(shortModel(largestShift[0].model) || largestShift[0].run_id)} on ${FACET_LABEL[largestShift[1]].toLowerCase()} (${signed(largestShift[2])}).` : 'No shifts yet.', 'Cells are shaded relative to the largest shift in the table.', shiftHeat(ranked))}
<h2>Ranked runs</h2>
<table><thead><tr><th>#</th><th>run</th><th>model</th><th class="num">n</th><th class="num">mean abs. diff</th><th class="num">median r</th><th class="num">vs best (paired)</th></tr></thead><tbody>
${ranked.map((s, i) => {
    const rs = FACETS.map((f) => Number(s[`${f}_r`])).filter(Number.isFinite).sort((a, b) => a - b);
    const p = paired[i];
    return `<tr${i === 0 ? ' class="hi"' : ''}><td>${i + 1}</td><td>${esc(s.run_id)}</td><td>${esc(s.model)}<br><span style="color:${SLATE}">${esc(s.provider || '')}</span></td><td class="num">${s.n}</td><td class="num">${fmt(s.mean_abs_diff, 3)}</td><td class="num">${fmt(rs[Math.floor(rs.length / 2)] ?? null)}</td><td class="num">${i === 0 ? 'best' : `${signed(p.diff, 3)}, p ${pfmt(p.p)} (n = ${p.n})`}</td></tr>`;
  }).join('')}
</tbody></table>
<p style="font-size:13px;color:${SLATE}">“vs best” is a paired t-test on each participant’s mean absolute difference under this run versus under the best run, over the participants both runs compared.</p>`;

  return page({
    eyebrow: 'Big Five study · all runs',
    title: 'Which model sounds most like <em>its human</em>?',
    lede: 'Every run gives each agent one participant’s BFI-2 answers as its persona and asks the 40 Saucier Mini-Markers. The closer the agent’s facet scores to that participant’s own, the more faithfully the model carries a persona.',
    facts: [
      { label: 'runs', value: String(ranked.length) },
      { label: 'participants', value: String(participants) },
      { label: 'closest model', html: `<small>${esc(best.model)}</small>` },
      { label: 'its mean abs. difference', value: fmt(best.mean_abs_diff, 3) },
      { label: 'updated', value: String(ranked.map((s) => s.analyzed_at).sort().pop() || '').slice(0, 10) },
    ],
    sections,
    footer: 'Generated by the n8n workflow “Big Five 5 - Analyze and Report”. One row per run in all-runs.csv; per-participant comparisons in &lt;run_id&gt;.csv.',
  });
}
