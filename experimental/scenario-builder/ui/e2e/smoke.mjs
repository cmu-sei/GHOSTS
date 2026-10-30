// The UI smoke check (plan Task 11): the real pages and agent panel in headless Chromium,
// against the API on :5280 with the fake model (node agent/evals/fake-model.ts) behind it.
//
//   node ui/e2e/smoke.mjs        # with the API, the fake model and `npx ng serve` (:4310) up
//   SMOKE_API=http://localhost:5291 node ui/e2e/smoke.mjs   # the page's /api calls go to another API instead
//
// playwright-core and its headless shell come from the GHOSTS checkout and its cache, so this
// folder installs nothing for it.

import { createRequire } from 'node:module';

const require = createRequire('/workspaces/GHOSTS/node_modules/');
const { chromium } = require('playwright-core');

const BASE = process.env.SMOKE_URL ?? 'http://localhost:4310';
const SHELL = process.env.SMOKE_BROWSER ?? `${process.env.HOME}/.cache/ms-playwright/chromium_headless_shell-1228/chrome-linux/headless_shell`;
const API = process.env.SMOKE_API ?? BASE;
const WAIT = { timeout: 60_000 };

const api = async (path, init = {}) => {
  const response = await fetch(`${API}/api${path}`, { ...init, headers: { 'content-type': 'application/json', ...(init.headers ?? {}) } });
  if (!response.ok) throw new Error(`${init.method ?? 'GET'} ${path}: ${response.status} ${await response.text()}`);
  return response.json();
};
const row = text => `app-question-row:has(.question:text-is("${text}"))`;
const status = async (page, text) => (await page.locator(`${row(text)} .status`).textContent()).trim();
const step = name => console.log(`ok - ${name}`);

const browser = await chromium.launch({ executablePath: SHELL });
const context = await browser.newContext({ viewport: { width: 1500, height: 950 } });
const page = await context.newPage();
const errors = [];
page.on('pageerror', e => errors.push(e.message));
// On the context, so a document opened in a new tab reaches the same API.
if (API !== BASE)
  await context.route(`${BASE}/api/**`, async route => route.fulfill({ response: await route.fetch({ url: route.request().url().replace(BASE, API) }) }));

try {
  // 1. Create a scenario: it opens on its Context page; paste a note, then start the questions.
  const scenarioName = `Smoke ${new Date().toISOString()}`;
  await page.goto(BASE);
  await page.fill('input[aria-label="Scenario name"]', scenarioName);
  await page.click('button:has-text("Create")');
  await page.waitForURL(/scenarios\/\d+\/context/);
  const id = Number(page.url().match(/scenarios\/(\d+)/)[1]);
  await page.fill('input[aria-label="Text name"]', 'Port notice');
  await page.fill('textarea[aria-label="Text"]', 'The harbor cranes stopped at 14:10.');
  await page.click('button:text-is("Add text")');
  await page.waitForSelector('.source:has-text("Port notice")', WAIT);
  if ((await api(`/scenarios/${id}/sources/search?q=cranes`)).length !== 1) throw new Error('the pasted note is not searchable');
  await page.click('button:text-is("Start the questions")');
  await page.waitForURL(/scenarios\/\d+\/questions/);
  const kind = await api('/kinds/crucible-tabletop');
  const stage0 = kind.stages[0].questions;
  step('create a scenario with context');

  // 2. Pick the fake model.
  await page.waitForSelector('select.model', WAIT);
  await page.selectOption('select.model', 'fake');
  step('pick model fake');

  // 3. Send "hello"; the fake asks the first question.
  await page.fill('.input textarea', 'hello');
  await page.click('.input button:has-text("Send")');
  await page.waitForSelector('app-question-card', WAIT);
  step('the agent asks a question');

  // 4. An answer in the designer's own words on the card → the first question (intent) is answered.
  await page.fill('app-question-card >> nth=0 >> textarea', 'Rehearse the port outage response');
  await page.click('app-question-card >> nth=0 >> button:has-text("Use mine")');
  await page.waitForSelector('app-question-card >> nth=1', WAIT);
  await page.waitForFunction(() => document.querySelector('app-question-row .status')?.textContent?.trim() === 'answered', null, WAIT);
  if ((await status(page, stage0[0].text)) !== 'answered') throw new Error(`${stage0[0].id} is not answered`);
  step('a card answer is recorded as the person');

  // 5. You decide → the next question is proposed by the agent.
  await page.click('app-question-card >> nth=1 >> button:has-text("You decide")');
  await page.waitForFunction(
    text => [...document.querySelectorAll('app-question-row')].some(r => r.querySelector('.question')?.textContent?.trim() === text && r.querySelector('.status')?.textContent?.trim() === 'proposed by agent'),
    stage0[1].text, WAIT);
  step('You decide → proposed by agent');

  // 6. The rest of stage 0 through the API as the person, then Approve on the page.
  const answered = new Set((await api(`/scenarios/${id}/progress`)).answered.map(a => a.id));
  for (const q of stage0.filter(q => !answered.has(q.id)))
    await api(`/scenarios/${id}/decisions/${q.id}`, { method: 'PUT', body: JSON.stringify({ value: q.answer === 'choice' ? q.options[0] : q.answer === 'list' ? ['x'] : `Answer for ${q.id}` }) });
  await page.reload();
  await page.click('button:has-text("Confirm the engagement settings")', WAIT);
  await page.waitForSelector('app-gate-panel:has-text("Every question is answered and approved")', WAIT);
  const afterApproval = await api(`/scenarios/${id}/progress`);
  if (!(afterApproval.stage >= 1 || afterApproval.gate.passes)) throw new Error(`stage 0 gate didn't pass: ${JSON.stringify(afterApproval.gate)}`);
  step('approve the stage 0 gate');

  // 7. An agent proposal survives a reload, and Apply changes the answer.
  await api(`/scenarios/${id}/proposals`, {
    method: 'POST', headers: { 'x-via': 'agent' },
    body: JSON.stringify({ questionId: 'audienceType', value: 'Mixed', reason: 'Both kinds of people attend.' })
  });
  await page.reload();
  await page.click('app-proposal-card button:has-text("Apply")', WAIT);
  await page.waitForFunction(() => document.querySelector('app-proposal-card')?.textContent?.includes('applied'), null, WAIT);
  const audience = (await api(`/scenarios/${id}/progress`)).answered.find(a => a.id === 'audienceType');
  if (audience.value !== 'Mixed' || audience.who !== 'stated') throw new Error(`audienceType is ${JSON.stringify(audience)}`);
  await page.click('button.stage >> nth=0');
  await page.waitForSelector(`${row('Civilian or military audience?')} .value:text-is("Mixed")`, WAIT);
  step('apply a proposal');

  // 8. Finish lists the starter documents; the participant read-in opens in a new tab without ground truth.
  await page.click('nav.pages a:text-is("Finish")');
  await page.waitForSelector('.document >> nth=2', WAIT);
  const [readIn] = await Promise.all([
    context.waitForEvent('page'),
    page.click('.document:has-text("Participant Read-In") a:text-is("Preview")')
  ]);
  await readIn.waitForLoadState();
  const readInText = await readIn.textContent('body');
  if (/ground truth/i.test(readInText) || !readInText.includes('Smoke')) throw new Error(`the read-in reads: ${readInText.slice(0, 200)}`);
  await readIn.close();
  step('preview the participant read-in');

  // 9. Download JSON saves the pull document. Playwright cancels any download it routes, so with SMOKE_API
  // the page fetches the link's target instead and the attachment's name and body are checked.
  const slug = (await api(`/scenarios/${id}`)).slug;
  const link = page.locator('a[download]:text-is("Download JSON")');
  let saved;
  if (API === BASE) {
    const [download] = await Promise.all([page.waitForEvent('download'), link.click()]);
    const { readFile } = await import('node:fs/promises');
    saved = { name: download.suggestedFilename(), body: JSON.parse(await readFile(await download.path(), 'utf8')) };
  } else {
    saved = await page.evaluate(async href => {
      const response = await fetch(href);
      return { name: /filename=([^;]+)/.exec(response.headers.get('content-disposition') ?? '')?.[1], body: await response.json() };
    }, await link.getAttribute('href'));
  }
  if (saved.body.slug !== slug || saved.name !== `${slug}.scenario.json`) throw new Error(`downloaded ${saved.name} with slug ${saved.body.slug}`);
  step('download the JSON');

  // 10. Send waits for Blueprint to be connected; + connects it by its API address; Send then says sending isn't built yet.
  await fetch(`${API}/api/plugins/blueprint`, { method: 'DELETE', headers: { 'x-forwarded-user': 'dev' } });
  await page.reload();
  await page.waitForSelector('button[aria-label="Send to Blueprint"]:disabled', WAIT);
  await page.click('button[aria-label="Connect Blueprint"]');
  await page.fill('input[aria-label="Blueprint API URL"]', 'https://blueprint.range.local/api');
  await page.click('.plugin:has-text("Blueprint") button:text-is("Connect")');
  await page.click('button[aria-label="Send to Blueprint"]:enabled', WAIT);
  await page.waitForSelector('[role="status"]:has-text("sending a scenario to it isn\'t built yet")', WAIT);
  step('connect Blueprint, then send to it');

  // 11. Admin: the starter library, and its template editor's preview renders.
  await page.click('a.admin');
  await page.click('.library:has-text("Tabletop Exercise (TTX)")', WAIT);
  await page.click('.template:has-text("Participant Read-In") a.title', WAIT);
  await page.waitForSelector('iframe[sandbox=""]', WAIT);
  const frame = page.frameLocator('iframe');
  await frame.locator('h1').first().waitFor(WAIT);
  step('the admin template editor previews');

  // 12. The scenario grid: search finds it, Archive hides it, Show archived brings it back to restore.
  await page.goto(BASE);
  await page.fill('input[aria-label="Search scenarios"]', scenarioName);
  await page.waitForFunction(name => [...document.querySelectorAll('tbody tr .title')].map(t => t.textContent.trim()).join('|') === name, scenarioName, WAIT);
  await page.click(`tbody tr:has-text("${scenarioName}") button:text-is("Archive")`);
  await page.waitForSelector('td.empty', WAIT);
  await page.check('label.archived-toggle input');
  await page.click(`tbody tr:has-text("${scenarioName}") button:text-is("Restore")`);
  await page.waitForSelector(`tbody tr:has-text("${scenarioName}") button:text-is("Archive")`, WAIT);
  if ((await api('/scenarios')).find(s => s.id === id).archived) throw new Error('the scenario is still archived');
  step('search, archive and restore in the grid');

  if (errors.length) throw new Error(`page errors: ${errors.join(' | ')}`);
  console.log('smoke: all steps passed');
} catch (e) {
  await page.screenshot({ path: '/tmp/sb-smoke-failure.png' }).catch(() => {});
  console.error(`smoke failed: ${e.message}${errors.length ? `\npage errors: ${errors.join(' | ')}` : ''}\nscreenshot: /tmp/sb-smoke-failure.png`);
  process.exitCode = 1;
} finally {
  // The agent may still be answering; drop its routed requests rather than fail on them.
  await context.unrouteAll({ behavior: 'ignoreErrors' });
  await browser.close();
}
