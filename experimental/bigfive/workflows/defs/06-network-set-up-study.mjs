import { Workflow, formTrigger, code, sticky, readTable, upsert, tableNote } from '../lib/n8n.mjs';

export default function build() {
  const wf = new Workflow('Network 1 - Set Up Study', 'networkSetUpStudy');
  wf.add(sticky('About', `## Network 1 - Set Up Study
Prepares the three network experiments (propagation, structure, social dynamics). Run it once, after "Big Five 1 - Create Tables" and "Big Five 2 - Import Human Data".

- **sim_cast**: 25 participants chosen to span the Big Five trait range (from their own BFI-2 scores), each given a name and a network position. Every model, arm and repeat uses these same 25 agents in the same positions.
- **sim_networks**: the three saved networks, generated from fixed parameters and the seed: a small world for propagation, a sparse small world for structure, a hub-and-periphery network for dynamics, plus who the seed agents are.

The same seed always gives the same cast and networks, so running it again changes nothing.`, [-660, -260], 560, 360));
  const form = wf.add(formTrigger('Set up form', 'network-set-up', 'Network · Set Up Study',
    'Chooses the 25 agents and generates the three networks. The same seed always gives the same result.', [
      { label: 'Seed', type: 'number', placeholder: '1' },
    ], [0, 0]));
  const readParticipants = readTable(wf, 'participants', [240, 0]);
  const readScores = readTable(wf, 'scores', [480, 0]);
  const readCast = readTable(wf, 'sim_cast', [720, 0]);
  const readNetworks = readTable(wf, 'sim_networks', [960, 0]);
  const setup = wf.add(code('Set up', 'net_setup.js', [1200, 0]));
  wf.chain(form, readParticipants, readScores, readCast, readNetworks, setup);
  upsert(wf, 'sim_cast', 'Set up', [1200, 300], { read: readCast, from: setup });
  upsert(wf, 'sim_networks', 'Set up', [1200, 620], { read: readNetworks, from: setup });
  tableNote(wf, [-660, 140]);
  return wf;
}
