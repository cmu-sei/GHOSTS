# Help for the First-Time Designer

2026-11-09. Approved in chat the same day.

## Who It Is For

A person building their first scenario who doesn't yet know what they are doing or why. They can answer
questions about their exercise; they don't know what a scenario type, a stage or a proposed answer is, why the
questions come in this order, or what the builder hands them at the end.

Success: without leaving the app or asking anyone, they can tell what the builder will produce, pick a scenario
type, and on every page know what it is for, what to do there and what "done" looks like; and they can look up any
term the app uses.

## Scope

Help explains **the tool and its vocabulary**. It adds no teaching per stage or per question: scenario types, their
files, the API and the database are unchanged. Where a scenario type already carries a stage `purpose` or a
`description` (types the model drafts do; the shipped TTX and Small Unit types don't), help shows it. The question's
existing `why` is surfaced better, not rewritten.

Everything works with no model: all help is static text that ships with the UI. Nothing waits on the agent.

Out of scope: per-question guidance or examples, an "explain this" call to the agent, help stored per user on the
server, translated help, Admin pages (admins aren't the first-time user).

## What the Designer Sees

### 1. The Guide

A **Guide** button in the top bar of every page (and the `?` key, when focus isn't in a text field) opens a
440 px panel that slides in from the right, over the agent panel. It doesn't block the page: the designer can keep
answering with it open. `Esc` or the close button shuts it and returns focus to where it was.

One search field at the top filters all three sections at once. The sections:

- **On This Page** (the default). An eyebrow (`QUESTIONS · STAGE 2 OF 5`), the page's name, what the page is for,
  what to do here, and what "done" looks like. Every page has its own: the Scenarios list, Context, Questions,
  Summary, Decision Log, Topology (and its GraySpace and Updraft screens), Finish. On Questions it adds the stage
  shown: its number and title, its `purpose` when the type has one, and what happens once its questions are answered.
- **How It Works.** A journey map: Context → Questions (each stage of this scenario's type, by title) → Summary →
  Decision Log → Topology (only when the type has one) → Finish, with **You are here** on the current step. Then the
  concept cards:
  - Stated, proposed and open, drawn with the ledger's own marks.
  - Stages, and how the next one opens once this one's questions are answered and its drafts written.
  - Working with the agent: it asks one question at a time and recommends an answer; any row can be answered by hand.
  - Handing over: saying "you decide" lets the agent make every remaining decision to the end; **Take back control**
    stops that.
  - Sources: what the Context page takes, and how the agent names a source in its reason.
  - Drafts: text the agent writes once a stage is answered, such as the executive summary, which anyone can edit.
  - Findings: what's still open or questionable; exports say so, and nothing is ever blocked.
  - Who sees what: participants, exercise control, designers.
- **Glossary.** Every term the app uses, each with a one-line definition and where in the app it appears: Scenario,
  Scenario Type, Stage, Gate, Question, Recommended Answer, Stated, Proposed, Open, Agent, Handing Over, Source,
  Draft, Executive Summary, Finding, Decision Log, Visibility, Participant, Exercise Control, Designer, Teaching
  Points, Ground Truth, Escalation Ceiling, Inject, Rules of Play, Sensitivity Review, Tabletop Exercise (TTX),
  Range, Topology, Enclave, Template Library, Document Template, Plugin, Pull API, Scenario JSON.

The Guide's footer: **Take the tour**, **Show the welcome again**, and the keys (`?` open, `Esc` close).

### 2. The Welcome (Scenarios Page)

A band above the create form, in the page, not a modal. It shows until dismissed:

- A serif headline about building an exercise by answering questions, not writing documents from scratch.
- A four-step strip: **Bring What You Have** (Context) → **Answer Stage by Stage** (Questions) → **Read It Back**
  (Summary, Decision Log) → **Take It Out** (Finish).
- One line on the agent: it recommends every answer, and can decide the rest when asked.
- **Which Scenario Type?**: each published type with its stage count, whether it has a Topology page, and its
  `description` when it has one. A type can't be changed after the scenario is made.
- **Got it** hides it. The Guide's **Show the welcome again** brings it back, going to the Scenarios page first.

### 3. The Tour (Scenario Workspace)

The first time the designer opens any scenario, a small card in the bottom-left corner offers
**Take the 60-second tour** or **Not now**; either answer stops the offer. The tour is never started for them.

Six stops, on the Questions page (the tour goes there first): the pages bar, the stage rail, a ledger row with the
legend, the gate line, the agent panel, the Guide button. Each stop dims the window except a cut-out around its
target and shows a caption card with **2 of 6**, **Back** and **Next** (the last says **Done**). Arrow keys step,
`Esc` leaves. A stop whose target isn't on screen is skipped. Targets are `data-tour` attributes on the existing
elements. The Guide replays it.

### 4. Help in the Ledger

- A legend above the ledger: ● Stated ● Proposed ○ Open, in the ledger's colours.
- Glossary terms in the Questions header's stage title and in question text get a dotted underline (not in the rail,
  whose stages are buttons already). Clicking or
  focusing one opens a small popover with its definition and **More in the Glossary**, which opens the Guide on that
  term. Whole words only, case-insensitive, first match of each term in a text.
- Each row's `why`, today shown only while editing, is also behind a **Why this matters** toggle beside the label when
  not editing; it shows with the pointer or keyboard focus, as **Change** does.
- The agent panel's intro gains a **How the agent works** link that opens the Guide on that concept.

## Look

The app's own language, made editorial: IBM Plex Serif for the Guide's and Welcome's display headings, IBM Plex
Mono for eyebrows and key hints, Plex Sans for everything else, the same family the app already uses. Generous
spacing, hairline rules, the existing `--sb-*` colours; the marks and chips are the ledger's own. One slide or fade
per surface; the existing reduced-motion rule turns them off. Headings and labels in Title Case; buttons and
sentences in sentence case. Never the word "slice".

## How It's Built

All in the UI (`ui/src/app/shared/help/`):

- `help-content.ts`: all the words: pages, concepts, glossary, tour stops, welcome steps.
- `help.service.ts` (root): whether the Guide is open, its section, search and focused term; the current page
  context (page id, plus the scenario type and stage the shell feeds it); the tour's step; and two localStorage
  flags: welcome dismissed, tour offered.
- `glossary-text.ts`: splits a text into plain runs and term runs (the matching rule above).
- Components: `guide`, `tour`, `term` (underline and popover), `welcome`, `legend`.

Edits to existing files, each small: the top bar (Guide button), the app root (mounts the Guide and the tour, the `?`
key), the scenario shell (feeds the context, offers the tour, `data-tour` on the pages bar and rail), the stage
rail and the Questions header (terms), the question row (terms, the why toggle), the gate panel and agent panel
(`data-tour`, the intro link), the scenarios list (the welcome), `angular.json` and `package.json`
(`@fontsource/ibm-plex-serif` and `@fontsource/ibm-plex-mono`, installed the container-local way).

Page context comes from the router's URL (the last segment that names a page) plus what the scenario shell sets.
The welcome reads full scenario types with the existing `GET /api/scenario-types/{id}`, one per published type.

## Accessibility

The Guide is a labelled `role="dialog"` with `aria-modal="false"`; it takes focus when opened and gives it back when
closed. The tour's caption is a labelled dialog that holds focus while it runs. Term popovers open on click or
Enter and close on `Esc` or blur. Everything works by keyboard, and nothing relies on colour alone: each mark also
has its word.

## Verification

- Vitest specs: the service (flags, sections, search, context), the term matcher, the Guide's content for each page
  and for a Questions stage, tour stepping and skipping, the welcome showing and dismissing, the why toggle.
- `npx ng test --watch=false` in `ui/` (not `npm test`, whose pretest rebuilds the bundle a running `ng serve`
  watches), and `npx ng build` in a /tmp copy.
- Headless screenshots of every surface, against a copy of the database on :5281 with model calls aborted, and
  the real database's scenario `updated` times checked unchanged afterwards.
