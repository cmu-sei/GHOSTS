import { Workflow, formTrigger, code, sticky, extractXlsx, upsert, tableNote } from '../lib/n8n.mjs';

export default function build() {
  const wf = new Workflow('Big Five 2 - Import Human Data', 'bigFiveImportHumanData');
  wf.add(sticky('About', `## Big Five 2 - Import Human Data
Upload the two study spreadsheets:
- **Personality Data.xlsx** — sheets \`Human Responses\` (Participant ID, age, Gender, ethnicity, bfi1..bfi60) and \`bfi lookup\` (bfiN → item text)
- **Human Mini-Marker Results.xlsx** — sheet \`Sheet1\` (Participant ID, tda1..tda40)

Writes participants (with the BFI-2 persona text), items (the instrument keying), human_bfi2, human_mini_markers and the human rows of scores. Re-running creates nothing twice: rows are matched by \`key\`, new ones are created, changed ones updated.

The spreadsheets are human-subject data: they pass through this n8n only and are never written back to git.`, [-660, -260], 560, 360));

  const form = wf.add(formTrigger('Import form', 'bigfive-import-human-data', 'Big Five · Import Human Data',
    'Upload the two study spreadsheets. Rows are matched by participant id; re-importing is safe.', [
      { label: 'Personality Data', type: 'file', required: true, accept: '.xlsx' },
      { label: 'Mini-Marker Results', type: 'file', required: true, accept: '.xlsx' },
    ], [0, 0]));
  const exA = wf.add(extractXlsx('Extract: Human Responses sheet', 'Personality_Data', 'Human Responses', [240, 0]));
  const passB = wf.add(code('Form again (lookup)', 'pass_form_binary.js', [460, 0]));
  const exB = wf.add(extractXlsx('Extract: bfi lookup sheet', 'Personality_Data', 'bfi lookup', [680, 0]));
  const passC = wf.add(code('Form again (Mini-Markers)', 'pass_form_binary.js', [900, 0]));
  const exC = wf.add(extractXlsx('Extract: Mini-Marker sheet', 'Mini_Marker_Results', null, [1120, 0]));
  const normalize = wf.add(code('Normalize', 'normalize_humans.js', [1340, 0]));
  wf.chain(form, exA, passB, exB, passC, exC, normalize);

  const tables = ['participants', 'items', 'human_bfi2', 'human_mini_markers', 'scores'];
  tables.forEach((table, i) => {
    const read = upsert(wf, table, 'Normalize', [1600, 300 + i * 320]);
    wf.connect(normalize, read);
  });
  tableNote(wf, [-660, 140]);
  return wf;
}
