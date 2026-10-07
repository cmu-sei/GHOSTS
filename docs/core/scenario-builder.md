# Scenario Builder and authoring

The Scenario Builder turns an exercise developer's plain-English intent into a validated GHOSTS scenario. It
lives in the frontend at `/scenarios/{id}/builder` and has two steps. Everything it does with a model, extraction
and the conversation, goes through Amazon Bedrock with the model picked on the Sources step.

1. **Sources.** First the **Model** and its **Effort**: one pair per scenario, Sonnet at its default effort to
   begin with. The model both reads the sources and runs the conversation; the effort level sets how hard it
   thinks in the conversation. Then text, web pages and files the scenario is built from. Each source is split into chunks
   the conversation searches and cites, and extraction runs as soon as a source is added, reading its chunks
   into entities (hosts, segments, people, organizations) and a graph. An entity a person marks **Reviewed** is
   taken as the real name. Chunks a run leaves pending, usually because the model was unavailable, can be
   retried from the Sources step.
2. **Conversation.** An authoring agent interviews the developer, drafts a scenario document against the
   schema in `schemas/scenario-document/`, validates it, and shows the plan, the decisions ledger and the
   warnings. Nothing is imported until the developer clicks **Import**; the server validates the exact bytes
   again and writes them into the Builder's scenario. Every turn shows the server's own gate report of what
   the agent called and what validated.

Afterwards the scenario opens in the planner, whose **Document** tab shows the approved version, what any edit
since has changed, and downloads of the document and its exercise plan.

## Configuration

The Builder calls Amazon Bedrock's Converse API. Credentials come from `AWS_ACCESS_KEY_ID` and
`AWS_SECRET_ACCESS_KEY` or the default AWS credential chain, never from configuration and never to the browser.
The `ScenarioAuthoring` section of `appsettings.json`:

| Key | Meaning | Default |
|---|---|---|
| `Region` | The Bedrock region. | `us-east-1` |
| `Model` | The model a scenario uses until one is picked on its Sources step. | Sonnet 5.5 |
| `Models[]` | The models the Sources step offers, each with `Name`, `Id`, `Efforts`, the effort levels it takes (`low`, `medium`, `high`, `xhigh`, `max` on current Anthropic models), and `Pricing`, US dollars per million input, output, cache-read and cache-write tokens. A model with no `Efforts` has the Sources step's effort control greyed out; one with no `Pricing` shows token counts but no cost. | the three Anthropic models, at Anthropic's list prices |
| `FallbackModel` | For ten minutes after `Model` returned HTTP 503, a new session that names no model starts here. A session that has begun keeps its model. Empty: no fallback. | empty |
| `MaxOutputTokens` | The output limit of each model call. A full draft needs at least 64,000. | `64000` |
| `PromptCaching` | Cache points on the system prompt, the tools and the conversation. Turn off for a model that rejects them. | `true` |
| `RequestTimeoutSeconds` | Each model call's request timeout. | `300` |
| `TurnTimeoutMinutes` | A turn's overall limit; the model call it is waiting on is cancelled. | `30` |
| `ValidatorTimeoutSeconds` | Each validator and dry-run call's own timeout. | `90` |

Extraction reads chunks `ScenarioBuilder:ExtractionConcurrency` at a time (default 6), one model call per
4,000-character chunk.

**Who is who.** GHOSTS has no sign-in. The API reads the user from the header an authenticating proxy sets,
`Identity:UserHeader` (`X-Forwarded-User` by default); without a proxy everyone is `anonymous`. A new scenario is
its author's draft, shown to its author alone, and cannot run until its author publishes it, which needs its
document to validate with 0 errors. Everything under a scenario, including its sources, chunks, entities, graph
and authoring sessions, follows the same rule.

**ATT&CK data.** The agent's technique and group lookups read an index embedded in the API. The Builder's
ATT&CK enrichment needs MITRE's STIX bundle in `config/AttackData/`; see the README there for where to get it.

## What a session costs

Under the conversation's text box, the page shows the session's model calls, input tokens (with how many were
read from the prompt cache), output tokens, and an estimated cost. The counts are the provider's own, recorded
for every call. The cost multiplies them by the model's configured `Pricing`, so it is an estimate at list
prices: Bedrock's rate for a region or a provisioned model may differ. With caching on, a drafting session on
Sonnet 5.5 runs about ten calls, a few dozen uncached input tokens, a quarter million cached, and some fifteen
thousand out, which is roughly twenty cents.

## Limits to know

- **One API instance.** A session runs one turn or import at a time through a lock held in the API's memory.
  Two API instances serving the same session could run two turns at once; running one instance, as the Docker
  Compose stack does, avoids it. A database-held lock would be the fix if that changes.
- **Bedrock only.** Neither the conversation nor extraction has an Ollama provider.
- **Citations open the chunk, not the place.** A `[chunk N]` citation opens the chunk's text; chunks record an
  index, not a page or character offset.
- **The plan is the server's.** The exercise plan under a reply's **Plan** button, and the `.plan.md` export,
  are rendered by the server from the document; the agent's own summary in the reply is prose.

## The acceptance tests on a real model

`ScenarioAuthoringServiceTests` covers the server's rules with a scripted model. The specification's
acceptance tests that need a real model (Payroll Week in two messages, Night Shift and Wire Friday to an
importable document, token records on every call, a draft grounded on a network source, a hostile source) are
in `Ghosts.Api.Tests/Live`. They are skipped unless pointed at a running API, because they cost money and
depend on the provider:

```
GHOSTS_LIVE_API=http://localhost:5000 GHOSTS_LIVE_EFFORT=medium dotnet test src/Ghosts.Api.Tests --filter FullyQualifiedName~Live
```

`GHOSTS_LIVE_MODEL` picks one of the configured models. A turn may take several minutes. Run them on the model a
deployment will rely on before relying on it.
