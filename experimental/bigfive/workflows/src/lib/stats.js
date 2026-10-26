// Shared statistics (spliced into Code nodes by build.mjs via `// @include lib/stats.js`). No dependencies.
function mean(xs) {
  return xs.length ? xs.reduce((a, b) => a + b, 0) / xs.length : null;
}

function pearson(xs, ys) {
  const n = xs.length;
  if (n < 3) return null;
  const mx = mean(xs);
  const my = mean(ys);
  let sxy = 0;
  let sxx = 0;
  let syy = 0;
  for (let i = 0; i < n; i++) {
    const dx = xs[i] - mx;
    const dy = ys[i] - my;
    sxy += dx * dy;
    sxx += dx * dx;
    syy += dy * dy;
  }
  if (sxx === 0 || syy === 0) return null;
  return sxy / Math.sqrt(sxx * syy);
}

// Fisher z 95% confidence interval for r at sample size n.
function fisherCI(r, n) {
  if (r === null || n < 4) return [null, null];
  const z = Math.atanh(Math.max(-0.999999, Math.min(0.999999, r)));
  const se = 1 / Math.sqrt(n - 3);
  return [Math.tanh(z - 1.959964 * se), Math.tanh(z + 1.959964 * se)];
}

// Standard normal CDF (Abramowitz & Stegun 7.1.26, |error| < 1.5e-7).
function normalCdf(x) {
  const t = 1 / (1 + 0.3275911 * Math.abs(x) / Math.SQRT2);
  const y = 1 - (((((1.061405429 * t - 1.453152027) * t) + 1.421413741) * t - 0.284496736) * t + 0.254829592) * t * Math.exp(-(x * x) / 2);
  return x >= 0 ? 0.5 * (1 + y) : 0.5 * (1 - y);
}

function lgamma(x) {
  const c = [76.18009172947146, -86.50532032941677, 24.01409824083091, -1.231739572450155, 0.1208650973866179e-2, -0.5395239384953e-5];
  let y = x;
  const tmp = x + 5.5 - (x + 0.5) * Math.log(x + 5.5);
  let ser = 1.000000000190015;
  for (const cj of c) ser += cj / ++y;
  return -tmp + Math.log((2.5066282746310005 * ser) / x);
}

// Continued fraction for the incomplete beta function (Numerical Recipes betacf).
function betacf(a, b, x) {
  const MAXIT = 200;
  const EPS = 3e-14;
  const FPMIN = 1e-300;
  const qab = a + b;
  const qap = a + 1;
  const qam = a - 1;
  let c = 1;
  let d = 1 - (qab * x) / qap;
  if (Math.abs(d) < FPMIN) d = FPMIN;
  d = 1 / d;
  let h = d;
  for (let m = 1; m <= MAXIT; m++) {
    const m2 = 2 * m;
    let aa = (m * (b - m) * x) / ((qam + m2) * (a + m2));
    d = 1 + aa * d;
    if (Math.abs(d) < FPMIN) d = FPMIN;
    c = 1 + aa / c;
    if (Math.abs(c) < FPMIN) c = FPMIN;
    d = 1 / d;
    h *= d * c;
    aa = (-(a + m) * (qab + m) * x) / ((a + m2) * (qap + m2));
    d = 1 + aa * d;
    if (Math.abs(d) < FPMIN) d = FPMIN;
    c = 1 + aa / c;
    if (Math.abs(c) < FPMIN) c = FPMIN;
    d = 1 / d;
    const del = d * c;
    h *= del;
    if (Math.abs(del - 1) < EPS) break;
  }
  return h;
}

// Regularized incomplete beta I_x(a, b).
function betai(a, b, x) {
  if (x <= 0) return 0;
  if (x >= 1) return 1;
  const bt = Math.exp(lgamma(a + b) - lgamma(a) - lgamma(b) + a * Math.log(x) + b * Math.log(1 - x));
  return x < (a + 1) / (a + b + 2) ? (bt * betacf(a, b, x)) / a : 1 - (bt * betacf(b, a, 1 - x)) / b;
}

// Two-sided p-value for a t statistic with df degrees of freedom.
function tTwoSidedP(t, df) {
  if (!Number.isFinite(t) || df < 1) return null;
  return betai(df / 2, 0.5, df / (df + t * t));
}

// Paired t-test on differences d = a - b. Returns { mean, t, df, p }.
function pairedT(as, bs) {
  const n = as.length;
  if (n < 3) return { mean: n ? mean(as.map((a, i) => a - bs[i])) : null, t: null, df: n - 1, p: null };
  const d = as.map((a, i) => a - bs[i]);
  const md = mean(d);
  const sd = Math.sqrt(d.reduce((s, x) => s + (x - md) * (x - md), 0) / (n - 1));
  if (sd === 0) return { mean: md, t: md === 0 ? 0 : Infinity, df: n - 1, p: md === 0 ? 1 : 0 };
  const t = md / (sd / Math.sqrt(n));
  return { mean: md, t, df: n - 1, p: tTwoSidedP(t, n - 1) };
}

// Holm step-down adjusted p-values, same order as the input; nulls stay null.
function holm(ps) {
  const idx = ps.map((p, i) => [p, i]).filter(([p]) => p !== null).sort((a, b) => a[0] - b[0]);
  const m = idx.length;
  const out = ps.map(() => null);
  let running = 0;
  idx.forEach(([p, i], rank) => {
    running = Math.max(running, Math.min(1, (m - rank) * p));
    out[i] = running;
  });
  return out;
}

// Steiger (1980) Z for two dependent correlations sharing one variable: r_jk vs r_jh, with r_kh the
// correlation between the two non-shared variables. Returns { z, p } (two-sided).
function steiger(rjk, rjh, rkh, n) {
  if ([rjk, rjh, rkh].some((r) => r === null) || n < 4) return { z: null, p: null };
  const clamp = (r) => Math.max(-0.999999, Math.min(0.999999, r));
  const z1 = Math.atanh(clamp(rjk));
  const z2 = Math.atanh(clamp(rjh));
  const rbar = (rjk + rjh) / 2;
  const psi = rkh * (1 - 2 * rbar * rbar) - 0.5 * rbar * rbar * (1 - 2 * rbar * rbar - rkh * rkh);
  const c = psi / ((1 - rbar * rbar) * (1 - rbar * rbar));
  const z = ((z1 - z2) * Math.sqrt(n - 3)) / Math.sqrt(2 - 2 * c);
  return { z, p: 2 * (1 - normalCdf(Math.abs(z))) };
}
