// Every word of the builder's help: what each page is for, how the builder works, the glossary, the tour and the
// welcome. Static on purpose: help works with no model and no network.

export type PageId = 'scenarios' | 'context' | 'questions' | 'summary' | 'log' | 'topology' | 'grayspace' | 'updraft' | 'finish' | 'admin';

export interface PageHelp {
  name: string;
  /** What the page is for, in one sentence. */
  lead: string;
  /** What to do here, in order. */
  steps: string[];
  /** What "done" looks like. */
  done: string;
  notes?: string[];
  /** The page to go to next, in the same scenario. */
  next?: PageId;
}

export interface Concept {
  id: string;
  title: string;
  body: string[];
}

export interface Term {
  id: string;
  term: string;
  definition: string;
  /** Where in the builder you meet it. */
  where: string;
  /** Words that mark the term where it appears in a stage title or question. Tool words that appear everywhere have none. */
  match?: string[];
}

export interface TourStop {
  /** The `data-tour` value of the element the stop points at. */
  target: string;
  title: string;
  body: string;
}

export const PAGES: Record<PageId, PageHelp> = {
  scenarios: {
    name: 'Scenarios',
    lead: 'Every scenario you can open, and where a new one begins.',
    steps: [
      'Name a new scenario, pick its scenario type, and press Create.',
      'It opens on Context, where you add what you already have.',
      'Open an existing scenario by its name. The Stage column shows how far it has got.'
    ],
    done: 'Your scenario exists and you are on its Context page.',
    notes: [
      'A scenario keeps the scenario type it was made with. Pick the one that matches how the exercise runs: a discussion around a table, or a team working on a range.'
    ]
  },
  context: {
    name: 'Context',
    lead: 'Give the agent what you already know, so it recommends from your material instead of guessing.',
    steps: [
      'Paste notes or an email, add a link, or upload documents such as a terms of reference, an agenda or a report.',
      'Each one is listed under Sources. Remove any that don’t belong.',
      'Press Start the questions. You can come back and add more at any time.'
    ],
    done: 'Your sources are listed, or you have chosen to start without any.',
    notes: [
      'Nothing here is required. Without sources, the agent works from your answers alone.',
      'Only the text of each source is kept. When the agent leans on one, it names it in its reason.'
    ],
    next: 'questions'
  },
  questions: {
    name: 'Questions',
    lead: 'The heart of the scenario: one decision per row, one stage at a time.',
    steps: [
      'Work down the open stage. Press Answer on a row, or let the agent ask you in the panel on the right.',
      'Not sure? Use the recommended answer, or press You decide and let the agent choose.',
      'When every row is answered, press Open the next stage.'
    ],
    done: 'Every stage on the left says Closed, and the last one says Complete.',
    notes: [
      'Any answer can be changed later, in any stage you can see. The Decision Log keeps the history.',
      'A stage stays locked until the one before it is answered, so each one builds on what is settled.'
    ],
    next: 'summary'
  },
  summary: {
    name: 'Executive Summary',
    lead: 'A short account of the whole scenario, drafted for you once enough is decided.',
    steps: [
      'Answer the stage it follows. The agent drafts the summary then.',
      'Read it as your sponsor would. Fix anything wrong or unclear, and press Save.'
    ],
    done: 'The summary reads true and is saved. The stage it follows can then close.',
    next: 'log'
  },
  log: {
    name: 'Decision Log',
    lead: 'Everything decided so far, by whom and why, newest first.',
    steps: [
      'Scan it to see what the agent decided while you were away.',
      'Each row shows the value before and after, and the reason given.',
      'To undo something, change the answer on the Questions page. The log records that too.'
    ],
    done: 'Nothing to do here: it is a record, not a task.',
    next: 'topology'
  },
  topology: {
    name: 'Topology',
    lead: 'Turn the scenario’s network answers into a real range, on the platform you use.',
    steps: [
      'Answer the network questions first. This page builds from those answers.',
      'Pick a platform and press Open. GraySpace and Updraft can be opened today; the others are coming.',
      'Build and deploy from that platform’s page.'
    ],
    done: 'The deployment is built on one platform.',
    next: 'finish'
  },
  grayspace: {
    name: 'Deploy to GraySpace',
    lead: 'Build this scenario’s enclave on GraySpace, and deploy it.',
    steps: [
      'An admin connects GraySpace once, by its address and token.',
      'Press Deploy everything to take the defaults, or work down the numbered decisions one at a time.',
      'Validate, save, plan, then create an instance and follow its job to the end.'
    ],
    done: 'An instance is running and its job has finished.',
    notes: ['Each numbered decision shows the answers it was drafted from.'],
    next: 'finish'
  },
  updraft: {
    name: 'Deploy to Updraft',
    lead: 'A preview of how this scenario would be built as a lab with Updraft.',
    steps: [
      'Read each numbered decision, and the answers it would use.',
      'Nothing deploys from here yet: Updraft isn’t connected.'
    ],
    done: 'You know what Updraft would need.',
    next: 'finish'
  },
  finish: {
    name: 'Finish',
    lead: 'Take the scenario out of the builder, at any stage.',
    steps: [
      'Download the Scenario JSON for the systems that run the exercise, or the Markdown to read.',
      'Preview and download documents made from your template libraries.',
      'Send it to a connected app under Plugins.'
    ],
    done: 'Everyone and everything that needs the scenario has it.',
    notes: ['Open findings never block an export. Each export and document says when it is still a draft.']
  },
  admin: {
    name: 'Admin',
    lead: 'Where admins shape what every designer works from.',
    steps: [
      'Scenario Types: make, edit and publish the stages and questions scenarios are built from.',
      'Libraries: the document templates the Finish page offers.'
    ],
    done: 'Your changes are published. New scenarios use them; existing ones are offered the update.'
  }
};

/** The scenario's pages in the order a designer moves through them. */
export const JOURNEY: { page: PageId; title: string; line: string }[] = [
  { page: 'context', title: 'Context', line: 'Bring what you have.' },
  { page: 'questions', title: 'Questions', line: 'Answer stage by stage.' },
  { page: 'summary', title: 'Summary', line: 'Read the draft back.' },
  { page: 'log', title: 'Decision Log', line: 'See who decided what.' },
  { page: 'topology', title: 'Topology', line: 'Build the range.' },
  { page: 'finish', title: 'Finish', line: 'Take it out.' }
];

/** The ledger's three marks, as the Stated, Proposed and Open concept draws them. */
export const MARKS: { mark: 'stated' | 'proposed' | 'open'; name: string; line: string }[] = [
  { mark: 'stated', name: 'Stated', line: 'A person gave it, by hand or through the agent.' },
  { mark: 'proposed', name: 'Proposed', line: 'The agent decided it. Its reason shows underneath.' },
  { mark: 'open', name: 'Open', line: 'Not answered yet.' }
];

export const CONCEPTS: Concept[] = [
  {
    id: 'provenance',
    title: 'Stated, Proposed and Open',
    body: ['Every answer carries a mark that says where it came from, here and in everything the builder exports.']
  },
  {
    id: 'stages',
    title: 'Stages',
    body: [
      'A scenario type splits its questions into stages, from the broad (who, why, when) to the specific (events, names).',
      'A stage opens once the one before it is answered, so each builds on what is settled and the agent’s recommendations sharpen as you go.',
      'When every question in a stage is answered, and any draft due after it is written, Open the next stage appears.'
    ]
  },
  {
    id: 'agent',
    title: 'Working with the Agent',
    body: [
      'The agent in the right-hand panel asks the open stage’s questions one at a time, each with a recommended answer and why.',
      'Take its recommendation, write your own, or answer any row on the page yourself. Your answer always stands.',
      'If it wants to change something a person stated, it proposes the change as a card the owner applies or discards.'
    ]
  },
  {
    id: 'handover',
    title: 'Handing Over',
    body: [
      'Press You decide on a question card and the agent answers that one question.',
      'Type “you decide” in the panel and it makes every remaining decision itself, to the end, each marked as proposed.',
      'Press Take back control at any time. What it decided stays, and you can change any of it.'
    ]
  },
  {
    id: 'sources',
    title: 'Sources',
    body: [
      'What you add on Context: pasted text, links and documents. Only the text is kept.',
      'The agent searches them before it recommends an answer, and names the source in its reason.'
    ]
  },
  {
    id: 'drafts',
    title: 'Drafts',
    body: [
      'Some parts are written rather than answered, such as the executive summary. The agent drafts each once the stage it follows is answered.',
      'Edit a draft like any document. The stage it follows closes once it is written.'
    ]
  },
  {
    id: 'findings',
    title: 'Findings',
    body: [
      'A finding is something still open or worth a second look, such as an unanswered question or a missing draft.',
      'Findings never block you. Exports and documents made with findings open say they are drafts.'
    ]
  },
  {
    id: 'visibility',
    title: 'Who Sees What',
    body: [
      'Some answers are for exercise control only: the ground truth, the twist, the planned events. Their questions say “(exercise control only)”.',
      'Each document is made for one audience (participants, facilitators or designers) and leaves out whatever that audience mustn’t see.'
    ]
  },
  {
    id: 'output',
    title: 'What You Get',
    body: [
      'One scenario document holding every answer, in the shape the systems that run exercises read: the Scenario JSON.',
      'Documents for people, made from templates: a plan, a read-in, a facilitator guide, whatever your libraries hold.',
      'Systems pull the scenario from the builder, or you send it to them from Finish.'
    ]
  }
];

export const GLOSSARY: Term[] = [
  { id: 'aar', term: 'After-Action Review', definition: 'The discussion after play about what happened, why, and what to change.', where: 'Questions about endings and outputs.', match: ['after-action review', 'AAR'] },
  { id: 'adjudication', term: 'Adjudication', definition: 'Ruling on what each action in play achieves. Someone named does it, and everyone knows who.', where: 'A question in the rules of play.', match: ['adjudication'] },
  { id: 'agent', term: 'Agent', definition: 'The assistant in the right-hand panel. It asks the questions, recommends answers, and can decide for you when asked.', where: 'The panel on the right of every scenario page.' },
  { id: 'attack', term: 'ATT&CK', definition: 'MITRE’s public catalogue of real attacker techniques, each with an ID such as T1566 (phishing).', where: 'Questions about the adversary.', match: ['ATT&CK'] },
  { id: 'decision-log', term: 'Decision Log', definition: 'The history of every answer, draft and proposal: who made it, when, and why.', where: 'The Decision Log page.' },
  { id: 'decision-point', term: 'Decision Point', definition: 'A moment in play where participants must choose. It is where an exercise does its teaching.', where: 'Questions about events and decisions.', match: ['decision points', 'decision point'] },
  { id: 'designer', term: 'Designer', definition: 'Whoever builds the scenario: you. Designer-only material never reaches participants or facilitators.', where: 'Document audiences on Finish.' },
  { id: 'document-template', term: 'Document Template', definition: 'A layout that turns the scenario into a document for one audience, such as a participant read-in.', where: 'Finish, under Document Templates.' },
  { id: 'draft', term: 'Draft', definition: 'A part of the scenario that is written rather than answered, drafted by the agent once its stage is answered.', where: 'The Summary page; a stage marked Draft due.' },
  { id: 'enclave', term: 'Enclave', definition: 'The part of a range built for this one scenario: its sites, network segments and hosts.', where: 'The Topology and GraySpace pages; network questions.', match: ['enclaves', 'enclave'] },
  { id: 'escalation-ceiling', term: 'Escalation Ceiling', definition: 'The worst the scenario is allowed to get, and what never happens. It keeps play inside what the team can handle in the time.', where: 'A stage title and a question.', match: ['escalation ceiling'] },
  { id: 'escalation-steps', term: 'Escalation Steps', definition: 'How the situation gets worse, step by step, up to the ceiling. One step is marked as the last the team can still recover from.', where: 'A question about events.', match: ['escalation steps'] },
  { id: 'executive-summary', term: 'Executive Summary', definition: 'A short account of the whole scenario for a sponsor or senior reader.', where: 'The Summary page.', match: ['executive summary'] },
  { id: 'excon', term: 'Exercise Control', definition: 'The people who run the exercise: they push events, rule on actions and steer play. Often called ExCon, or the controllers.', where: 'Questions marked “(exercise control only)”.', match: ['exercise control', 'ExCon', 'controllers'] },
  { id: 'finding', term: 'Finding', definition: 'Something still open or worth a second look. Findings never block an export; they mark it as a draft.', where: 'The line above a stage’s questions; Finish.' },
  { id: 'fog', term: 'Fog', definition: 'What participants deliberately can’t see. Deciding with a partial picture is part of what is tested.', where: 'A question in the rules of play.', match: ['fog'] },
  { id: 'gate', term: 'Gate', definition: 'What stands between a stage and the next: its unanswered questions and any draft still due.', where: 'The line above a stage’s questions.' },
  { id: 'ground-truth', term: 'Ground Truth', definition: 'What is really happening in the scenario, known only to exercise control. Later checks catch contradictions against it.', where: 'A stage title and a question.', match: ['ground truth', 'ground-truth'] },
  { id: 'handover', term: 'Handing Over', definition: 'Letting the agent decide: one question with You decide on its card, or everything left by typing “you decide”.', where: 'The agent panel.' },
  { id: 'inject', term: 'Inject', definition: 'A piece of news, a message or an event that exercise control pushes into play to move it on.', where: 'Questions about key events, information and the network.', match: ['injects', 'inject'] },
  { id: 'objective', term: 'Objective', definition: 'What the exercise trains, starting with a verb: “Coordinate…”, “Decide…”. Each one needs a way to tell whether it was met.', where: 'Objectives questions.', match: ['objectives', 'objective'] },
  { id: 'participant', term: 'Participant', definition: 'Someone who plays the exercise. Participants never see exercise-control material.', where: 'Many questions; document audiences on Finish.' },
  { id: 'plugin', term: 'Plugin', definition: 'Another app the scenario can be sent to, once an admin connects it by its address.', where: 'Finish, under Plugins.' },
  { id: 'proposed', term: 'Proposed', definition: 'An answer the agent decided, marked in violet with its reason. A good first draft you can keep or change.', where: 'Every question row and the Decision Log.' },
  { id: 'pull-api', term: 'Pull API', definition: 'The address other systems read the finished scenario from, as JSON.', where: 'Finish, under Export.' },
  { id: 'range', term: 'Range', definition: 'A built network of real or virtual systems where a team trains hands-on.', where: 'Small unit scenarios; the Topology page.', match: ['live range'] },
  { id: 'recommended-answer', term: 'Recommended Answer', definition: 'The answer the scenario type or the agent suggests. One click uses it.', where: 'Question rows and the agent’s question cards.' },
  { id: 'red-herring', term: 'Red Herring', definition: 'A deliberate false lead. At most one per exercise, clearly marked in the ground truth.', where: 'A question about the premise.', match: ['red herring'] },
  { id: 'rules-of-play', term: 'Rules of Play', definition: 'How the exercise runs: pacing, the scenario clock, who adjudicates, and how much participants see.', where: 'A stage title.', match: ['rules of play'] },
  { id: 'scenario', term: 'Scenario', definition: 'One exercise as you design it: every answer and draft, from first context to the finished document.', where: 'Everywhere.' },
  { id: 'scenario-json', term: 'Scenario JSON', definition: 'The whole scenario as one machine-readable document, exactly as the systems that run exercises read it.', where: 'Finish, under Export.' },
  { id: 'scenario-type', term: 'Scenario Type', definition: 'The kind of exercise, such as a tabletop or a small unit team exercise. It sets the stages and questions, and can’t be changed once a scenario is made.', where: 'The Scenarios page; Admin.' },
  { id: 'sensitivity-review', term: 'Sensitivity Review', definition: 'A check that names, places and terms can’t offend, mislead or point at real people or organizations.', where: 'A stage title.', match: ['sensitivity review'] },
  { id: 'simulated-users', term: 'Simulated Users', definition: 'Software characters (NPCs) that act like real people on the range or in the story: they send email, post and browse.', where: 'Population and network questions.', match: ['simulated users', 'NPCs'] },
  { id: 'source', term: 'Source', definition: 'Text you give the agent on Context: pasted notes, a link or a document.', where: 'The Context page.' },
  { id: 'stage', term: 'Stage', definition: 'A group of related questions. Stages open in order, each once the one before it is answered.', where: 'The rail on the left of Questions.' },
  { id: 'stated', term: 'Stated', definition: 'An answer a person gave, by hand or through the agent, marked in cobalt.', where: 'Every question row and the Decision Log.' },
  { id: 'success-criteria', term: 'Success Criteria', definition: 'How you will tell each objective was met. An objective without one can’t be assessed.', where: 'Objectives questions.', match: ['success criteria'] },
  { id: 'ttx', term: 'Tabletop Exercise (TTX)', definition: 'A discussion-based exercise: leaders talk through a developing situation and make decisions, with no live systems.', where: 'Scenario types.', match: ['tabletop exercise', 'TTX'] },
  { id: 'tcs', term: 'Tasks, Conditions and Standards', definition: 'How team training is written: the task trained, the conditions it is done under, and the standard it must meet.', where: 'A stage title in small unit scenarios.', match: ['tasks, conditions and standards'] },
  { id: 'teaching-points', term: 'Teaching Points', definition: 'What participants should discover for themselves. They guide facilitators and never appear as takeaways handed to participants.', where: 'A stage title and a question.', match: ['teaching points'] },
  { id: 'template-library', term: 'Template Library', definition: 'A set of document templates for a scenario type, managed in Admin.', where: 'Finish; Admin › Libraries.' },
  { id: 'topology', term: 'Topology', definition: 'The network a scenario needs and where it is built. Only scenario types with a range have a Topology page.', where: 'The Topology page.' },
  { id: 'visibility', term: 'Visibility', definition: 'Who may see an answer: participants, exercise control, or designers only.', where: 'Questions marked “(exercise control only)”; document audiences.' }
];

export const TOUR: TourStop[] = [
  { target: 'pages', title: 'Your Scenario, Left to Right', body: 'Context first, then Questions, then the pages that read it back and take it out. Jump to any of them at any time.' },
  { target: 'rail', title: 'Stages', body: 'The questions come in stages, broad to specific. Each bar fills as you answer, and a stage opens once the one before it is answered.' },
  { target: 'ledger', title: 'One Row, One Decision', body: 'Press Answer to answer a row yourself. The mark says who decided: cobalt for a person, violet for the agent, a ring for still open.' },
  { target: 'gate', title: 'What’s Left', body: 'This line counts what is still open in the stage. When it’s all answered, Open the next stage appears here.' },
  { target: 'agent', title: 'The Agent', body: 'It asks the questions one at a time and recommends an answer for each. Type “you decide” and it finishes the scenario for you.' },
  { target: 'guide', title: 'Help, Any Time', body: 'Open the Guide here, or press ?, for what each page is for, how the builder works, and every term it uses.' }
];

export const WELCOME_STEPS: { title: string; body: string }[] = [
  { title: 'Bring What You Have', body: 'Notes, links, documents. The agent reads them first.' },
  { title: 'Answer Stage by Stage', body: 'Broad to specific, each with a recommended answer.' },
  { title: 'Read It Back', body: 'A drafted summary, and a log of who decided what.' },
  { title: 'Take It Out', body: 'The scenario as JSON, documents, or sent to another app.' }
];
