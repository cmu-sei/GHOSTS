# GHOSTS Network Experiments

**Propagation · Relationship Formation · Belief Dynamics**

> **Status:** Design draft. Nothing described here is built yet.
> **Last updated:** 2026-11-11
> **Related:** Digital twin (Big Five) study in `experimental/bigfive`

---

## Contents

1. [Purpose](#purpose)
2. [At a Glance](#at-a-glance)
3. [Design Principles](#design-principles)
4. [Shared Components](#shared-components)
5. [Loop A: Propagation](#loop-a-propagation)
6. [Loop B: Relationship Formation](#loop-b-relationship-formation)
7. [Loop C: Belief Dynamics](#loop-c-belief-dynamics)
8. [Run Plan](#run-plan)
9. [What These Experiments Can Claim](#what-these-experiments-can-claim)
10. [System Notes](#system-notes)
11. [Related: Digital Twin Study](#related-digital-twin-study)
12. [Defaults to Confirm](#defaults-to-confirm)
13. [Open Questions](#open-questions)
14. [Glossary](#glossary)
15. [References](#references)

---

## Purpose

The research program asks three increasingly demanding questions about GHOSTS:

| # | Question | Kind of evidence needed |
|---|---|---|
| 1 | Does GHOSTS produce the intended behavior? | Functional correctness and internal consistency |
| 2 | Does GHOSTS reproduce meaningful characteristics of real human and social systems? | Comparison against human reference data |
| 3 | Can GHOSTS predict human and social behavior under conditions nobody scripted? | Out-of-sample prediction against real outcomes |

The three loops on this page are built to answer **question 1** rigorously. They also produce the measurements that question 2 would need, but question 2 can only be answered once matching human data exists (see [Open Questions](#open-questions)).

The design follows the evaluation in Park et al. (2023), *Generative Agents*: 25 agents, 2 simulated days, a party invitation, interviews checked against memory, and network density. Each of those measurements is split into its own loop, with controls added.

---

## At a Glance

| | Loop A: Propagation | Loop B: Relationship Formation | Loop C: Belief Dynamics |
|---|---|---|---|
| **Question** | Do agents pass information on, and does importance change how far it goes? | Do agents form ties on their own, and what shape does the network take? | How do beliefs change under a rumor and a correction? |
| **Held fixed** | Network (no new ties) | Starting network; no focal event in the main condition | Network (no new ties); everyone starts informed |
| **Conditions** | Party invite · Trivial news · Party on a two-team network | No event · Party invite | Rival as hub or periphery × correction sent or not |
| **Main measure** | % exposed over time; importance effect | Density change; clustering; triangle closure | Belief curves; response to the correction |
| **Null model** | Simple contagion (share rate p) | Friend-of-friend rule; random-tie rule | GHOSTS forward Bayes; offline belief-model comparison |
| **LLM calls per run** | ~1,000 | ~650 | ~1,250–1,500 |

---

## Design Principles

- **Ground truth is a log the agents can't touch.** Delivery code records every message (sender, recipient, tick, text) and every tie event. No outcome is ever measured against a record that an agent's own output wrote.
- **Memory is only what the agent experienced.** An agent's memory holds its notes, the messages it received, the messages it sent, and (in Loop B) its connection events. Interview answers and belief checkpoints are never written back.
- **Agents decide; code enforces.** The LLM chooses who to message, what to say, and whom to meet. Rules such as "contacts only" are enforced in code, not in the prompt.
- **Every loop has a rule-decides run (null model).** The LLM has to beat a simple rule on a measure specified in advance. If the two only match on the overall curve, the conclusion is that, for that measure, the agents behave like the rule.
- **The model is a robustness factor.** Results should hold across models and prompt wordings. What "holds" means is defined before running.
- **One fixed, blinded, calibrated judge.** A single judge model labels messages and answers for every run. Its input carries no run, condition or model labels, and it is checked against human coding.
- **Deliberately not used:**
  - `npc_learning` and `npc_beliefs`: the shipped workflows fill them with random topics and fixed-formula updates.
  - The evidence processor and `hypotheses`: keyword matching ignores negation.
  - The shipped social workflows: they write random data to the same NPCs.

---

## Shared Components

All three loops use these. Each loop refers to them by number.

### S1. Run Record

One record per run:

| Field | Notes |
|---|---|
| `run_id` | |
| `loop` | A, B or C |
| `condition` | |
| `model`, `provider` | Ollama or Bedrock |
| `temperature` | Fixed at 0.7 |
| `prompt_version` | W1 or W2 (two wordings, for robustness) |
| `random_seed`, `network_seed` | |
| `host_id`, `rival_id` | `rival_id` is used in Loop C only |
| `status`, `started_at`, `finished_at` | |

### S2. Fresh NPCs per Run

The same 25 saved NPC profiles are created as new records (`POST /api/npcs`) inside a new execution (`POST /api/executions`). Each run needs fresh records because GHOSTS has no endpoint for deleting a connection, so reused NPCs would carry the previous run's ties.

### S3. Starting Network

- **Default:** a small-world network (Watts & Strogatz, 1998) generated from `network_seed`.
  - 25 agents, each linked to 2 neighbors on each side (k = 4), with 10% of links rewired.
  - That gives 50 links and a density of 2·50 / (25·24) = **0.167**, the same starting density as Park et al.
- **Two-team network:** groups of 12 and 13, dense inside each group, 3 bridge links between them, 50 links in total. Same density, different shape.
- **Storage:** each link is written to GHOSTS twice (a→b and b→a), because GHOSTS stores one directed row per pair. It is also written to the tie log at tick 0 with origin `seeded`.

### S4. Persona and Contacts

- **Persona text** is built from the NPC profile the same way the `Build Persona` node in `GHOSTS NPC Chat` does it. One plain sentence is added for `MotivationalProfile.SocialContact`, e.g. "You really enjoy being around people."
- **Contact list:** each contact's name plus a relationship label ("coworker", "neighbor", …) drawn from a fixed list.

### S5. Memory

Everything the agent has, in time order:

- notes
- messages received (sender, time, text)
- messages sent (recipient, time, text)
- connection events (Loop B only)

Nothing else is ever added.

### S6. Decision Call

One LLM call per agent per tick.

- **Input:** the persona, the current time ("Day 1, 10:00"), the full memory (S5) and the contact list.
- **Instructions:** neutral, with "do nothing" listed first as an equal option. Nothing about parties, passing news on, or meeting people.
- **Output, Loops A and C:**

  ```json
  { "nothing": false, "messages": [{ "to": "Dana Ruiz", "text": "..." }] }
  ```

- **Output, Loop B:**

  ```json
  {
    "nothing": false,
    "messages": [{ "to": "Dana Ruiz", "text": "..." }],
    "connect_requests": [{ "to": "Sam Okafor", "note": "..." }],
    "replies": [{ "from": "Lee Park", "accept": true }]
  }
  ```

- **Limits:** up to 3 messages per tick. In Loop B, at most 1 connection request per tick, and a reply to each pending request.

### S7. Validation (in Code)

| Problem | Action |
|---|---|
| Message to someone who isn't a contact | Dropped; logged as `invalid_action: not_contact` |
| More than 3 messages | First 3 kept; the rest logged as invalid |
| Loop B: request to a name not in memory, or to an existing contact | Dropped; logged. Names are exact-matched against the 25 |
| Malformed JSON | Retried once with the same input. If it fails again, the agent does nothing that tick, logged as `failed_turn` |

### S8. Delivery

Everything sent during tick t lands in recipients' memory at the start of tick t+1. That includes scripted messages, connection requests and replies. Nobody sees anything from the current tick, so the order agents act in within a tick can't affect results.

### S9. Fresh-Context Questions

Interviews and belief checkpoints are separate calls: the persona and full memory, plus the questions. Answers go to their own table and are never written back to memory.

### S10. Judge

- **One fixed judge model** for every run and condition. It is not one of the agent models under test.
- **Blinded:** its input has no run, condition or model labels.
- **Calibrated:** for each model, a person hand-codes 200 messages and 100 answers. If agreement (Cohen's κ) is below 0.8, the judge prompt is fixed before any results are used.
- **Code first:** plain code replaces the judge wherever possible, e.g. name matching and yes/no fields in the JSON.

### S11. Per-Run Checks

- **Shipped workflows off.** These are deactivated during runs:
  - `GHOSTS Social Graph`
  - `GHOSTS Connections`
  - `GHOSTS Belief`
  - `GHOSTS Preferences`
  - `GHOSTS Post to Social Media`
- **Network unchanged.** After the run, the GHOSTS connections must equal the seeded network (Loops A and C), or the seeded network plus the tie log (Loop B).
- **Failure rates.** Invalid-action and failed-turn rates are reported per model, never hidden.
- **Context length.** Tokens are recorded per call. Any call above 80% of the model's context window is flagged, since memory would otherwise be cut off silently.

### Simulated Time

24 ticks: Day 1 and Day 2, hourly from 08:00 to 19:00. The party is on Saturday, after Day 2, so it is always in the future.

---

## Loop A: Propagation

**Tests:** whether agents pass information through a fixed network, and whether the information's importance changes how far it travels.
**Held fixed:** the network (no new ties) and the content, apart from the condition being tested.

### Conditions

| Condition | Network | Host's note |
|---|---|---|
| **A-P** | Small-world | "I'm having my birthday party Saturday at 7pm at my place, 412 Elm St. I'd really like everyone to come." |
| **A-T** | Small-world | "I bought a new lamp for my living room this week." |
| **A-P2** | Two-team | Same as A-P. Out-of-sample test for the rule run |

### Setup

1. Create fresh NPCs (S2) and build the network for the condition (S3).
2. Pick the host: an agent with the median number of contacts (4).
3. Give every agent its persona and contacts (S4). The host also gets the note for its condition. No other agent knows anything.

### Loop

```
for tick t = 1 … 24:
  deliver everything sent at t−1                       (S8)
  for each of the 25 agents:
    decision call, messages only                       (S6)
    validate                                           (S7)
    write each valid message to the message log:
      run_id, tick, from, to, text
```

### After the Loop

**Interview** (S9), one conversation per agent:

| # | Question |
|---|---|
| Q1 | Open recall: "What's coming up over the next few days that you know about?" |
| Q2 | Cued: "Have you heard anything about [Host]'s birthday party? If yes, when and where is it, and who told you?" |
| Q3 | Decoy: "Have you heard anything about the company picnic on Friday? If yes, who told you?" (No picnic is ever seeded.) |

Q2 and Q3 are asked in random order for each agent.

**Judge** (S10):

- **Each message:** does it carry the host's news? Are the date, time and place correct, wrong or absent?
- **Q1 answers:** does it mention the party?
- **Q2 answers:** does it say yes? Are the date, time and place correct, wrong or absent? Which source does the agent name?

### Measures

All measures are over the 24 agents other than the host.

| Measure | Definition |
|---|---|
| Exposed | Agent has received at least one message carrying the news by tick t (from the message log) |
| Spread curve | % exposed at each tick; final %; first ticks reaching 25%, 50% and 75% |
| Spread tree | Who first told each exposed agent; hops from the host; share of seeded links that ever carried the news |
| Recall % | Share of agents whose Q1 answer mentions the party |
| Recognition % | Share of agents answering yes to Q2 |
| Validated-yes rate | TP / (TP + FP) |
| Hallucination rate | FP / (FP + TN) |
| Forgetting rate | FN / (FN + TP) |
| Decoy false-yes rate | Q3 yeses / 25 |
| Detail fidelity | Among TP: % with the correct date, time and place |
| Source accuracy | Among TP: % whose named source actually sent them the news |
| Importance effect | Final % exposed in A-P − final % in A-T, with a 95% bootstrap interval over repeats |
| Persona effect | Logistic regression: for each tick an agent was exposed, did it pass the news on? Predictor: SocialContact. Pooled over repeats |

How the interview answers are scored against the message log:

| | Exposed | Not exposed |
|---|---|---|
| **Q2 yes** | TP | FP (hallucinated) |
| **Q2 no** | FN (forgot) | TN |

### Rule-Decides Run

No LLM; 1,000 runs per setup.

- **Rule:** each tick, each exposed agent sends the news to each contact independently with probability p.
- **Fitting p from A-P:**
  `p = news messages sent by exposed agents ÷ (exposed agent-ticks × that agent's contacts)`
- **Comparisons:**
  - Does the LLM spread curve fall inside the rule's 95% band?
  - **Out-of-sample:** on A-P2, run the rule with the p fitted on A-P, without refitting.
  - **Importance:** the rule ignores content, so its A-P minus A-T difference is 0 by construction. The LLM's importance effect is the test.

---

## Loop B: Relationship Formation

**Tests:** whether agents form new ties on their own, what network shape results, and whether what agents say about who they know matches the ties actually formed.
**Held fixed:** the starting network. The main condition has no focal event.

### Conditions

| Condition | Content seeded |
|---|---|
| **B-0** | Each agent gets one ordinary personal note, so there's something to talk about |
| **B-P** | Same as B-0, plus the host's party invite. Does a shared event drive tie formation? |

Each personal note is written once from the profile (family, job, hobbies), for example "Your son has his first soccer game Saturday morning." A person reviews the notes, and they are then frozen and reused in every B run.

### Setup

1. Create fresh NPCs (S2) and build the small-world network (S3).
2. Give every agent its persona and contacts (S4) and its personal note. In B-P, the host (median contacts) also gets the party note.
3. **Day-0 interview** (S9), Q5 only, to get the starting values.

### Loop

```
for tick t = 1 … 24:
  deliver messages, connection requests and replies from t−1      (S8)
  for each of the 25 agents:
    decision call with messages, connect_requests, replies         (S6)
    validate; requests only to names already in memory             (S7)
    write messages to the message log
    write requests to the tie log as "requested"
    write replies to the tie log as "accepted" or "declined"
    on accept:
      write both directed rows to GHOSTS connections
      add the new contact to both contact lists from t+1
```

### After the Loop

**Interview** (S9), Q5: a shuffled list of 24 real names (everyone except the agent) plus 4 made-up names. "For each name: do you know who this person is? If yes, give one thing you know about them." Output per name: `{ "name", "knows", "fact" }`. This follows Park's definition of a relationship: *both know of each other*.

**Judge (optional):** is each stated fact consistent with the agent's memory?

### Measures

All measures are over all 25 agents.

| Measure | Definition |
|---|---|
| Perceived tie | i says it knows j **and** j says it knows i |
| Density | η = 2\|E\| / (25·24) = \|E\| / 300 |
| Perceived η | At day 0 and at the end, and the change |
| Actual η | From the tie log: 0.167 at the start, then the end value and the change |
| Day-0 check | Recall of the seeded network should be close to 1, because agents were told their contacts. Ties claimed at day 0 that aren't in the network give the baseline false-tie rate |
| Fake-name rate | Fake names marked "knows" ÷ (4 × 25) |
| Perceived vs actual | Precision and recall at the end |
| Clustering | Clustering coefficient at the start and the end |
| Triangle closure | % of new ties between agents who already shared a contact |
| Degree spread | Distribution of contact counts across agents |
| Event effect | Density change in B-P − density change in B-0 |
| Persona effect | Do requests sent and ties gained track SocialContact? |

### Rule-Decides Run

No LLM; 1,000 runs.

Names have to reach agents somehow, so both rules use the same message step: every agent messages each contact with probability m per tick, and each message mentions one of the sender's other contacts with probability u. On top of that:

- **Friend-of-friend rule:** for each name an agent has heard of but isn't connected to, it sends a request with probability q_r. The other agent accepts with probability q_a.
- **Random rule:** any unconnected pair forms a tie with probability q, set so the expected number of new ties matches the LLM's.

Fit m, u, q_r and q_a from the B-0 logs. Compare the LLM against both rules on density change, clustering and triangle closure. The two rules give the same density with different clustering, so together they show which pattern the LLM's ties resemble, if either.

---

## Loop C: Belief Dynamics

**Tests:** how agents' beliefs change when a rumor contradicts what they know, and how they respond when the rumor's source is shown to be wrong.
**Held fixed:** the network (no new ties) and awareness, since everyone knows about the party from the start.

### Conditions

| Condition | Rival's position | Correction sent? |
|---|---|---|
| **C-hub-corr** | Best-connected eligible agent | Yes |
| **C-hub-none** | Best-connected eligible agent | No |
| **C-per-corr** | Least-connected eligible agent | Yes |
| **C-per-none** | Least-connected eligible agent | No |

### Setup

1. Create fresh NPCs (S2) and build the small-world network (S3).
2. The host is an agent with the median number of contacts. The rival is at least 2 hops from the host, in the position its condition requires.
3. Give every agent its persona and contacts (S4), plus a note: "A flyer on the office board says: [Host]'s birthday party, Saturday 7pm, 412 Elm St. Everyone welcome." The host's note is the first-person version.
4. **Checkpoint 0** (S9), before tick 1: everyone's baseline belief.

### Loop

```
for tick t = 1 … 24:
  deliver everything sent at t−1                                      (S8)
  if t = 4:
    the rival sends each of its contacts:
      "Heads up, I heard [Host]'s party on Saturday is cancelled."
    logged as injected; also stored in the rival's own sent messages
  if t = 16 and the condition sends a correction:
    the host sends each of its contacts:
      "Just so everyone knows, my party Saturday is still on.
       [Rival] got mixed up; it was never cancelled."
    logged as injected
  for each of the 25 agents:
    decision call, messages only                                      (S6)
    validate                                                          (S7)
    write each valid message to the message log
  if t ∈ {3, 6, 9, 12, 16, 17, 18, 21, 24}:
    checkpoint for every agent                                        (S9)
```

**Checkpoint question:** "How likely is it that [Host]'s party happens on Saturday, from 0 to 100? Which messages influenced you most?"
**Output:** `{ "likelihood": 0–100, "influenced_by": [message ids] }`

The correction is sent at tick 16 and delivered at tick 17. Checkpoint 16 is therefore the last one before anyone sees it, and checkpoint 17 the first one after.

### After the Loop

- **Judge** (S10): the stance of each message (on, cancelled, or none).
- **Rumor tracing,** done in code: does a "cancelled" message name the rival, or come from someone who received the rival's message?

### Measures

All measures are over the 23 agents other than the host and the rival.

| Measure | Definition |
|---|---|
| Belief curves | Each agent's checkpoints, plus the average |
| End-state spread | Standard deviation of beliefs at tick 24 |
| Majority | % of agents above 50 at tick 24 |
| Split | At least 20% at 70 or above **and** at least 20% at 30 or below |
| Rumor reach | % of agents who received at least one "cancelled" message, hub vs periphery |
| Correction effect | Δb = belief at 17 − belief at 16, compared between correction and no-correction conditions (difference-in-differences). Split by whether the agent heard the rumor directly from the rival or secondhand, and by whether it received the correction directly |
| Persistence | Belief at tick 24 compared with checkpoint 0 |

### Belief-Model Comparison

This is done offline, using the logged messages. Each model predicts every agent's belief at every checkpoint from that agent's inbox.

| Model | How it computes belief | Parameters |
|---|---|---|
| Forward Bayes (same as GHOSTS `Bayes.cs`) | Each "on" message multiplies the odds by L/(1−L); each "cancelled" message by (1−L)/L | L |
| Bayes + reevaluation (Updyke, 2023) | Same, but at the correction, earlier messages traced to the rival are re-scored as uninformative and the chain is recomputed | L, r (how far re-scoring goes: 0 = none, 1 = full) |
| DeGroot (1974) | Moves toward the average belief of contacts who messaged the agent since the last checkpoint | w |
| Bounded confidence (Hegselmann & Krause, 2002) | Like DeGroot, but only contacts within ε of the agent's own belief count | w, ε |
| Belief-adjustment (Hogarth & Einhorn, 1992) | b ← b + β·(s − R), with separate β for confirming and disconfirming messages | β+, β− |

- **Fit and score:** fit each model on checkpoints 0–16, predict 17–24, and score by mean absolute error against the beliefs the agents gave.
- **The discriminator:**
  - In the correction conditions, reevaluation predicts a jump at checkpoint 17 for agents whose "cancelled" evidence traces to the rival. Forward Bayes predicts a step the size of a single message.
  - In the no-correction conditions, both predict no jump.

### Rule-Decides Run

No LLM; 1,000 runs.

- **Rule:** agents message each contact with probability m, stating their current belief. Beliefs update by GHOSTS's current formula (forward Bayes, fixed L).
- **Fitting:** m comes from the LLM logs.
- **Arithmetic:** use exact math. `Bayes.cs` rounds, which locks belief at 1.0; see [System Notes](#system-notes).
- **Purpose:** this shows what GHOSTS's current belief mechanism predicts for the same scenario.

---

## Run Plan

| Loop | Pilot (1 model, 1 wording, 3 repeats) | Full (5 repeats × 2 models × 2 wordings) |
|---|---|---|
| A | A-P, A-T: 6 runs, ~6,000 calls | 3 conditions: 60 runs, ~60,000 calls |
| B | B-0, B-P: 6 runs, ~4,000 calls | 2 conditions: 40 runs, ~26,000 calls |
| C | 4 conditions: 12 runs, ~17,000 calls | 4 conditions: 80 runs, ~110,000 calls |

- **Order:** A, then B, then C. Loop A has the simplest instruments, and its interview checks carry over to the other loops.
- **Gate:** each pilot must pass the S10 judge check, the S11 checks and the day-0 check before its full run.
- **Repeats:** the pilot's run-to-run variance sets the final number of repeats.
- **Rule-decides runs:** negligible cost.

### What Exists vs What's New

| Exists in GHOSTS | New |
|---|---|
| NPC create and read (`POST /api/npcs`, `GET /api/npcs/{id}`) | Baserow tables: runs, memory, messages, ties, checkpoints, interviews, judgments (the same pattern as the Big Five study) |
| Executions to group a run's NPCs | Network generator |
| Connections (`POST /api/npcs/{id}/connections`, `GET /api/npcs/connections`), so the relationships view shows each run's network | Memory builder |
| Persona building (`Build Persona` in `GHOSTS NPC Chat`) | Decision step with code validation |
| Ollama and Bedrock model nodes in n8n | Delivery step and scripted messages |
| The n8n + Baserow + HTML report pattern from the Big Five study | Judge, rule simulators, measures, belief-model fitting, report |

---

## What These Experiments Can Claim

| Loop | Can show (question 1) | Needs before question 2 can be answered |
|---|---|---|
| A | Agents pass information on; importance changes spread; hallucination and forgetting rates; whether the LLM does more than simple contagion | Real data on how invitations or news spread through a comparable group |
| B | Agents form ties from conversation; whether the resulting shape looks like friend-of-friend closure or random ties; whether self-reports match actual ties | Real data on tie formation in a comparable group over a comparable time |
| C | Which belief model best fits how LLM agents update; whether they snap back after a correction | Human belief-updating data under the same rumor-and-correction setup |

A match between the LLM run and a rule-decides run is not a failure for question 1. It shows when GHOSTS can use cheap rules instead of LLM calls. It is also not evidence of human-like behavior.

---

## System Notes

Facts about the current GHOSTS code that shaped this design:

- **No connection delete.** There is no API to delete a connection, so each run uses fresh NPCs.
- **Directed rows.** Connections are stored one row per direction, so each link is written twice.
- **Shipped workflows write random data.**
  - `GHOSTS Social Graph` links each NPC to the first 10 others it finds and logs knowledge with a random topic.
  - `GHOSTS Connections` links random pairs.
  - `GHOSTS Belief` applies a Bayes update to a randomly chosen belief.
- **The evidence processor matches keywords as substrings.** It runs on `SocialMediaPost` activity, matches any hypothesis keyword as a substring (so it ignores negation), and updates the author and every connection with a fixed likelihood. Direct messages don't trigger it.
- **Fixed-likelihood belief is just an exposure count.** With the default likelihood of 0.6, belief after n matching posts is exactly 1/(1 + (2/3)ⁿ).
- **`Bayes.cs` locks belief at 1.0.** It rounds to 10 decimals, so after about 59 consecutive matching posts belief becomes exactly 1.0 and no later evidence can lower it.
- **`Bayes.cs` is forward-only.** It is the update rule that Updyke (2023) treats as the machine curve; the human curve in that work adds reevaluation. `npc_beliefs` stores the step, source, likelihood and posterior that reevaluation would need, but nothing reevaluates.
- **NPC profiles include motivation scores.** They carry Reiss motivation scores, including `SocialContact`.
- **Prior finding on LLM agents.** Networks of LLM agents tend to converge toward the factually accurate view unless prompted toward confirmation bias (Chuang et al., 2024). Loop C may show less polarization than human groups.

---

## Related: Digital Twin Study

The agent-level study lives in `experimental/bigfive`. Each agent's persona is one participant's BFI-2 answers. The agent takes the 40-item Mini-Markers test, and its answers are compared with that participant's own Mini-Markers answers.

**Current results (run-001, Claude Haiku, n = 438).** The baseline is how well each participant's own BFI-2 scores match their own Mini-Markers scores, compared with the twin using Steiger's (1980) test.

| Trait | Twin r | Baseline r | Steiger p |
|---|---|---|---|
| Extraversion | .891 | .885 | .37 |
| Agreeableness | .786 | .796 | .39 |
| Conscientiousness | .830 | .840 | .31 |
| Emotional stability | .781 | .738 | .001 |
| Openness | .732 | .750 | .32 |

What the results show:

- **Four of five traits:** the twin equals the participant's own BFI-2 score passed straight through.
- **Emotional stability:** the twin beats the baseline, likely because it uses item-level detail that the trait-total baseline throws away.
- **Level shift:** the twin scores lower than the human on every trait (−0.19 to −0.48, all p < .001).
- **Model dependence:** a mistral:7b run (n = 23) diverges sharply on openness (r = .40, shift −2.26).

Next steps for the twin:

- **A fairer baseline:** a regression from BFI-2 items to Mini-Markers, instead of trait-total correlation.
- **Predictions outside the persona:** a twin claim needs predictions of something the persona doesn't contain, either a different construct or something the participant actually did.

---

## Defaults to Confirm

| Area | Default |
|---|---|
| Starting network | Small-world, k = 4, 10% rewired (density 0.167); two-team network for A-P2 |
| Time | 24 hourly ticks over 2 days |
| Limits | At most 3 messages per tick; 1 connection request per tick (Loop B) |
| Roles | Host at the median number of contacts; rival at least 2 hops away, best- or least-connected by condition |
| Content | A-T: the lamp note · Loop B: frozen personal notes · Loop C: the office flyer · Decoy: the company picnic · 4 fake names |
| Loop C timing | Rumor at tick 4, correction at tick 16; 2 × 2 design |
| Judge | One fixed model; agreement κ ≥ 0.8 |
| Temperature | 0.7 |

---

## Open Questions

1. **Human reference data.** Beyond the BFI-2 and Mini-Markers, what human data exists or can be collected? This covers the study participants, and also real-world data on information spread, tie formation, or belief change. Every question-2 claim depends on it.
2. **"Holds across models."** What counts as a result holding, for each loop's main measure? For example: Loop A's importance effect has the same sign on every model and every prompt wording.
3. **What counts as correct belief updating.** Should an NPC's belief updating match forward Bayes, reevaluation, or measured human behavior? Updyke (2023) presents reevaluation as a conjecture and a how-possibly explanation, not an empirically validated model of human updating.

---

## Glossary

| Term | Meaning |
|---|---|
| **Tick** | One simulated hour. Every agent gets one decision call per tick |
| **Exposed** | Has received at least one message carrying the news, per the message log |
| **Perceived tie** | Two agents who both say, in the interview, that they know who the other is |
| **Actual tie** | A connection in the tie log (seeded, or requested and accepted) |
| **Density (η)** | 2\|E\| / (\|V\|(\|V\|−1)): the share of all possible pairs that are tied |
| **Rule-decides run** | The same loop with a simple rule instead of the LLM; the null model |
| **Judge** | The fixed model that labels messages and answers after a run |
| **Checkpoint** | A fresh-context question asking an agent's current belief, never stored in its memory |
| **Reevaluation** | Revising the weight of past evidence when new evidence shows it was misread (Updyke, 2023) |
| **Scripted (injected) message** | A message written by the experiment, not by an agent, and flagged as such in the log |

---

## References

- Chuang, Y.-S., et al. (2024). Simulating opinion dynamics with networks of LLM-based agents. *Findings of NAACL 2024.*
- Deffuant, G., Neau, D., Amblard, F., & Weisbuch, G. (2000). Mixing beliefs among interacting agents. *Advances in Complex Systems.*
- DeGroot, M. H. (1974). Reaching a consensus. *Journal of the American Statistical Association.*
- Friedkin, N. E., & Johnsen, E. C. (1990). Social influence and opinions. *Journal of Mathematical Sociology.*
- Hegselmann, R., & Krause, U. (2002). Opinion dynamics and bounded confidence: Models, analysis and simulation. *Journal of Artificial Societies and Social Simulation.*
- Hogarth, R. M., & Einhorn, H. J. (1992). Order effects in belief updating: The belief-adjustment model. *Cognitive Psychology.*
- Jøsang, A. (2016). *Subjective Logic: A Formalism for Reasoning Under Uncertainty.* Springer.
- O'Connor, C., & Weatherall, J. O. (2018). Scientific polarization. *European Journal for Philosophy of Science.*
- Park, J. S., O'Brien, J. C., Cai, C. J., Morris, M. R., Liang, P., & Bernstein, M. S. (2023). Generative agents: Interactive simulacra of human behavior. *UIST '23.*
- Reiss, S. (2004). Multifaceted nature of intrinsic motivation: The theory of 16 basic desires. *Review of General Psychology.*
- Steiger, J. H. (1980). Tests for comparing elements of a correlation matrix. *Psychological Bulletin.*
- Updyke, D. D. (2023). *Undermining Trust in Learning and Inference Systems.* MS thesis, Carnegie Mellon University. https://kilthub.cmu.edu/articles/thesis/Undermining_Trust_in_Learning_and_Inference_Systems/24097875 (code: https://github.com/dustinupdyke/epistemic_momentum)
- Watts, D. J., & Strogatz, S. H. (1998). Collective dynamics of 'small-world' networks. *Nature.*
- Zollman, K. J. S. (2007). The communication structure of epistemic communities. *Philosophy of Science.*
