# The elicitation checklist

Fourteen questions. They are the ones an authoring agent may not answer for the exercise developer,
and they are the whole interview: **everything not on this list the agent proposes with one sentence of
reasoning, for the developer to accept in a word.** The measure of the interview is how few questions
reach a validated draft, not how thorough it sounds (design §4, Step 4).

The agent asks only the ones the intent leaves open. An intent that already says "a nine-person utility
SOC, first rotation" has answered A1 and A2, and asking again is noise.

**"You decide" is an allowed answer.** The agent then proposes a value with its reasoning and records it
in the decisions ledger **as proposed, not as the developer's**. That distinction is the point of the
ledger: a reviewer can see which mission judgments a person made and which a model made.

## Where the list comes from

Two sources, and each question cites both where it has both.

**The schema.** A field with no defensible default is an interview question. Of the schema's fields,
most have a default that is right more often than not (`provenance.origin` is `operator`, an objective
is `MET`-typed at priority 1, an event's `execution.mode` is `manual`, the telemetry booleans are
false) — those are declared as `default` in the schema itself as of v1.1.0 and the agent never asks
about them. What is left over is this list: the fields where a default would be a mission decision
wearing a value's clothes. `adversaries[].capability` has no defensible default because 1 and 5 describe
different exercises; `rulesOfPlay.branching` has none because the answer is the training objective.

**The two Step 0 runs.** The wizard run by an agent settled **16 mission judgments by default or guess**
outright and 5 partly (`step0-agent-run.md`, "Mission judgments settled by default or guess"). The
non-programmer run filled **13 rows by guessing** and wanted to ask someone at every one of them — the
GHOSTS lead twice, **the writer of the intent eleven times** (`step0-human-run.md`, the third number).
Neither run was asked any of it. Those eleven rows and those sixteen judgments are the same list observed
twice, from two directions, and this file is that list turned into questions. Each entry below names the
rows it came from, so a reader can check the derivation rather than take it on faith.

Three things deliberately **not** on the list, because Step 0 shows they are not judgment calls:

- **ATT&CK technique ids.** Both Step 0 runs entered an id they could not verify (agent #10, human row
  5). That is a retrieval failure, not a missing judgment, and the fix is `attack_technique_lookup` and
  tier 2 — never a question, and never recall.
- **Clock arithmetic.** "Day 2, 09:40" into `T+2d9h40m` (agent #14, #28, #33, human row 12) is
  conversion. The agent does it and the validator checks it.
- **Anything the API discards.** Supported Platforms was a mission judgment the developer made and the
  application then threw away on save (agent #24 then #42). Asking for a decision the system cannot keep
  is worse than not asking. `terrain.informationEnvironment.platforms` holds it in the document, so the
  agent proposes it and the developer corrects it in a word; it is not worth one of fourteen questions.

---

## A. The training audience and its proficiency

### A1. "Who is in the room, and what are they responsible for?"

**Fills** `audience.role`, `audience.size`, `audience.mandate`
**No defensible default:** the exercise exists for these people and nothing in a corpus knows who they
are. Size is not cosmetic — a nine-person SOC and a two-person shop need different inject volumes.
**The agent must ask** unless the intent says it.
**Step 0:** human row 7; agent #16.

### A2. "What can they already do, and what will they be doing for the first time here?"

**Fills** `audience.proficiency`
**No defensible default:** proficiency sets every difficulty dial downstream — population size, dwell
time, how loud the adversary is. "First rotation" is a phrase in an intent, not a value; what a first
rotation has and has not seen is the developer's knowledge of their own people.
**The agent must ask** unless the intent says it.
**Step 0:** agent #18 (settled partly — "the intent doesn't define the proficiency floor"); human row 6.

### A3. "Who in this scenario is simulated by GHOSTS, and who is a person playing?"

**Fills** `population.pools[].role`, `.count`, `.description`; bounds `audience.size`
**No defensible default:** the pools field *is* the NPC binding, so a wrong answer either animates the
blue cell or leaves the exercise unpopulated. The agent run named this explicitly: nothing on screen
distinguishes "NPCs GHOSTS animates" from "people in the room", and it deliberately left the nine SOC
analysts out of the pools on a guess.
**The agent must ask who;** it **may propose the counts** with reasoning, because population size is a
difficulty dial the developer can correct in a word once the roles are right.
**Step 0:** agent #16, #17, #44; human row 7.

## B. The decisions the audience must be forced to make

### B1. "What decision must this exercise force, and by when?"

**Fills** `assessment.objectives[].name`, `.description`, `.successCriteria`; `rulesOfPlay.deadline.at`,
`.label`
**No defensible default:** this is the exercise's reason to exist. An exercise with no forced decision is
a demonstration. The deadline is what converts a lesson into a decision — without it the audience can be
right eventually, which is not the skill being trained.
**The agent must ask.**
**Step 0:** human row 9; agent #39 (the objectives dropdown is empty, so the graded standard could not be
attached to the decision point at all).

### B2. "What makes that decision correct, what counts as a partial pass, and does the network have to end up clean?"

**Fills** `assessment.victoryConditions`, `assessment.objectives[].metWhen`, `assessment.purpose`
**No defensible default:** the intent gives a sentence ("a correct containment decision by day 7, not a
clean network") and assessment needs a threshold. The agent run had the sentence and still had to invent
what *correct* meant — isolate which host, revoke which credentials, by when — and invent a partial band.
Three exercises can share that sentence and grade differently.
**The agent must ask.** It may propose the `metWhen` condition once the threshold is stated, because that
is translation.
**Step 0:** agent #21 (settled partly: "the standard is stated; the threshold I invented"); human row 9.

### B3. "What puts the decision in front of them, and who decides whether their response worked?"

**Fills** `timeline.events[].at`/`.when`, `.owner`, `.expectedResponse`; `rulesOfPlay.adjudication`
**No defensible default:** an inject schedule is mission tempo. The agent run invented six injects, their
times of day, and what happens on the seven days the intent does not mention. And `adjudication` decides
whether a white cell judges the response or a model does, which changes what the exercise *is*; the
wizard's default is `Manual Control` and nothing explains the alternatives.
**The agent may propose the events and their pacing** with reasoning — that is the draft's job — but
**must ask who adjudicates**, and must ask before inventing beats on days the intent is silent about.
**Step 0:** agent #13, #29, #37; human rows 6, 11.

## C. The end state that counts as success

### C1. "How far may the adversary get, and which is the last point where recovery is still possible?"

**Fills** `rulesOfPlay.escalationLadder.rungs[].name`, `.description`, `.recoverable`;
`adversaries[].winThreshold`, `.objective`
**No defensible default:** the ladder is the shape of the exercise. Its top rung is the consequence the
exercise is named for, and the last recoverable rung is where the training value lives — past it the
audience is watching, not deciding.
**The agent may propose the rungs** between the intent's endpoints, but **must ask which rung is the last
recoverable one**, because that single answer sets the difficulty of everything before it.
**Step 0:** agent #30 (five rungs authored from nothing, rung 4 pinned as the last success point by
guess); human row 6.

### C2. "If they succeed early, does the exercise end, or does the adversary try again?"

**Fills** `rulesOfPlay.branching.summary`; `adversaries[].playbook[].preconditions`; the conditions on
later `timeline.events[].when`
**No defensible default:** the agent run calls this "arguably the single most consequential judgment I
made unasked". If the phish is caught on day 2 and the exercise stops, the audience never reaches the OT
lesson the exercise exists to teach; if the adversary retries, early success is unrewarded. Both are
defensible and they are different exercises.
**The agent must ask.**
**Step 0:** agent #31. Neither Step 0 run decided it, and the human run never saw the question.

## D. What may fail and what may not

### D1. "What is the audience allowed to do, and what will the adversary not do?"

**Fills** `audience.rulesOfEngagement`
**No defensible default:** this is a safety and validity boundary — may they isolate, may they rebuild,
is live malware permitted, what is out of bounds for the red cell. A default here is a guess about
someone else's authorities. It was entirely absent from the §4a intent and both runs filled the box from
nothing.
**The agent must ask.**
**Step 0:** agent #20; human row 9 (resolved from the author's own understanding of cyber authorities).

### D2. "How hard does the adversary press, and what is it actually trying to achieve?"

**Fills** `adversaries[].capability` (1–5), `.objective`, `.type`; scopes `.techniques[]`
**No defensible default:** capability has anchors in the schema but no default that is not a decision —
the wizard's default of 1 would have contradicted the TTP set the agent run entered, and nothing
complained. Capability is "what may fail": it decides whether the audience can plausibly win.
**The agent may propose a capability with reasoning** tied to the objective and the ladder, because the
schema's anchors make it arguable rather than arbitrary; it **must ask if the intent implies neither an
objective nor a ladder**. The technique list is **never** a question and **never** from memory — it comes
from `attack_technique_lookup` against the committed index, scoped by this answer.
**Step 0:** agent #9, #10; human row 5 (no legend for the 1–5 scale anywhere on screen).

### D3. "What must they be able to see — and is there anywhere they are meant to be blind?"

**Fills** `timeline.events[].indicators[]`, `.expectedResponse`; `terrain.defenses[]`;
`rulesOfPlay.fog`; `rulesOfPlay.telemetry.*`
**No defensible default:** an event with no observable is an event the audience cannot respond to, so the
exercise grades them on invisible things. Where a gap is *deliberate* — no endpoint telemetry on the OT
segment, say — that is a designed lesson and the opposite of a defect, and only the developer knows which
one it is.
**The agent may propose the indicators** for each event, and **must ask whether any blindness is
intended.**
**Step 0:** agent #12 (the inject shape cannot hold an observable at all, so every one was dropped or
folded into a title), #26 (no CVE or defense editor exists).

## E. Hard constraints

### E1. "How long does it run, and is that real time or compressed?"

**Fills** `rulesOfPlay.duration`, `.pacing`, `.clock.tickMinutes`, `.clock.label`
**No defensible default:** "11 days of play" does not say whether that is eleven real days or eleven
exercise-days compressed into a window, and the compression ratio changes what the audience experiences
and what the deadline means. The wizard's duration default is 8 hours, which would silently produce an
eight-hour exercise for an eleven-day scenario.
**The agent must ask for the pacing;** it computes the duration and every offset from the intent's dates
and the validator checks them.
**Step 0:** agent #27, #28, #34 (11 days = 264h exceeds both duration inputs' cap of 168 with no visible
error); human rows 11, 12.

### E2. "What terrain already exists, what does this exercise add, and what may not be touched?"

**Fills** `terrain.reference.provider`, `.slice`; `terrain.segments[]`; `terrain.hosts[]`;
`terrain.services[]`
**No defensible default:** the intent names a slice ("the *regional-utility* slice") and nothing in
GHOSTS can look up what is in it, so the base terrain is knowledge the developer holds. What the exercise
*adds* is a design decision, and it hides a difficulty dial: whether two OT segments are segmented from
each other decides whether the lateral movement is findable, which the agent run flagged as "a difficulty
decision disguised as a network detail".
**The agent must ask** for the slice and for what is off limits; it **may propose the added segments and
hosts**, which the developer corrects.
**Step 0:** agent #22, #23, #25; human row 10.

### E3. "Is the adversary a real named actor or a fictional one, and whose country is this?"

**Fills** `adversaries[].name`; `sides[].name`, `.alignment`; `context.political`
**No defensible default:** naming a real APT imports real attribution into a training exercise, which is
a decision with consequences outside the exercise; naming a fictional one gives up the real TTP set.
There is no neutral choice. Both Step 0 runs picked unasked and picked **differently** — the human chose
a real adversary nation from current events, the agent invented Donovian "COPPER LANTERN" after seeing the
seeded scenarios. Political context shapes what the adversary is willing to do and was absent from the
intent entirely.
**The agent must ask** real-or-fictional. It **may propose** the names and the political framing once
told which.
**Step 0:** agent #5, #7, #19; human rows 4, 8.

---

## What the agent does with the answers

Everything else is proposed. In particular the agent proposes, and does not ask about: the slug and
name; `context.situation`; every ATT&CK technique (from the index); each event's `title`, `description`,
`effects.setFlags` and `indicators`; the entity graph and its `provenance`; `startingConditions`;
`terrain.informationEnvironment`; `assessment.performanceMetrics`; `workflows[]`; `sources[]` and
`references[]`; and every field the schema gives a `default`.

A finding from the validator that implies one of these fourteen questions goes **back to the developer
with the question it implies** — it is not repaired quietly. A finding that is mechanical (an unknown
reference, an id collision, an offset outside the duration) the agent fixes and re-validates. That split
is the design's, in §4a: "the agent repairs mechanical findings but escalates judgment ones. An agent
that quietly resolves the second kind has rebuilt the original problem, only faster."
