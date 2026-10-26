import { Workflow, formTrigger, code, sticky } from '../lib/n8n.mjs';

export default function build() {
  const wf = new Workflow('Big Five 1 - Create Tables', 'bigFiveCreateTables');
  wf.add(sticky('About', `## Big Five 1 - Create Tables
Creates the study tables in a Baserow database: participants, items, human_bfi2, human_mini_markers, runs, agent_turns, scores, comparisons, run_summaries. Every table gets a text primary field \`key\`.

Open the form (Test workflow → Open form, or the production URL once active), enter the Baserow URL **as seen from the n8n container** (Aspire dev stack: \`http://baserow\`), your Baserow email and password, and the id of the database (from its URL, \`/database/<id>/\`).

Existing tables are kept: missing columns are added, and a column of the wrong type stops the workflow and names it. Nothing is ever deleted.

The output lists each table with its id. Then import the other four workflows and pick these tables in their Baserow nodes.`, [-660, -200], 560, 420));
  const form = wf.add(formTrigger('Create tables form', 'bigfive-create-tables', 'Big Five · Create Tables',
    'Creates the study tables in an existing Baserow database. Nothing is deleted.', [
      { label: 'Baserow URL', type: 'text', required: true, placeholder: 'http://baserow' },
      { label: 'Email', type: 'text', required: true, placeholder: 'ranger@admin.local' },
      { label: 'Password', type: 'password', required: true },
      { label: 'Database ID', type: 'number', required: true, placeholder: '258' },
    ], [0, 0]));
  const create = wf.add(code('Create tables', 'create_tables.js', [260, 0]));
  wf.connect(form, create);
  return wf;
}
