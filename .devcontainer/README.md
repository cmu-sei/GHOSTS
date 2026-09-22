# Dev Container Services & Maintenance

This dev container runs several persistent services alongside the coding agents, and
provides scripts for maintaining a project over its lifetime. This document covers both.

Those scripts all live in `.devcontainer/scripts/`, and every command below is written to be run from your
project's root directory.

See the [project README](../README.md) for getting started.

## Unified Endpoint (LiteLLM)

In addition to the five coding agents, the container runs a [LiteLLM](https://docs.litellm.ai) proxy that exposes the models from your active profile(s) behind a single OpenAI-compatible API at **`http://localhost:7010`** (port 7010 is forwarded to your host). This is handy for scripts, notebooks, or any OpenAI-compatible tool that wants one endpoint instead of each provider's native API.

The proxy listens on localhost only and requires no API key. It does **not** change how Claude Code, Codex, OpenCode, Pi, or Grok CLI are configured — those agents still talk to their providers directly.

List the available models:

```bash
curl http://localhost:7010/v1/models
```

Send a chat completion (use a model name from the list above):

```bash
curl http://localhost:7010/v1/chat/completions \
  -H 'Content-Type: application/json' \
  -d '{"model": "aws/sonnet-5", "messages": [{"role": "user", "content": "Hello!"}]}'
```

The model names exposed depend on the active profiles:

| Profile | Model names |
|---------|-------------|
| `aws` | `aws/fable-5.1`, `aws/opus-5`, `aws/sonnet-5`, `aws/gpt-6-astra`, `aws/gpt-5.6-terra`, `aws/gpt-5.6-sol` |
| `awsgov` | `awsgov/fable-5.1`, `awsgov/opus-5`, `awsgov/sonnet-5`, `awsgov/gpt-5.4` |
| `opal` | `opal/gpt-oss-120b`, `opal/gemma-4-26b` |
| `etc` | `etc/gpt-oss-120b` |

Model names are namespaced by profile, which also keeps the OPAL and ETC `gpt-oss-120b` models distinct when both are enabled. Proxy logs are written to `~/.data/litellm/litellm.log`.

## Browser Chat UI (Open WebUI)

For a graphical chat experience, the container also runs [Open WebUI](https://openwebui.com) at **`http://localhost:7000`** (port 7000 is forwarded to your host). It is wired to the LiteLLM proxy as its only model source, so it offers exactly the same profile-namespaced models listed above — pick one from the model selector and start chatting.

Two things to know about the Bedrock models (`aws/*`) in the chat UI:

- **No "Thinking" block.** Claude on Bedrock returns its reasoning encrypted, and the GPT models hide theirs entirely, so the collapsible Thinking panel stays empty for every `aws/*` model. Only `opal/gpt-oss-120b` streams visible reasoning. The models still reason — you just can't see it, and Bedrock doesn't report those tokens.
- **`aws/gpt-6-astra` can be slow to start.** It reasons silently before emitting a single token, so the reply may sit empty for anything from a couple of seconds to a minute before it appears. It is thinking, not stuck. Lowering **Reasoning Effort** in the chat controls (the sliders icon → Advanced Params) shortens the wait — the proxy already defaults it to `low` — and raising it lengthens it.

The per-chat **Reasoning Effort** control works on every `aws/*` model and overrides the proxy's default. **Temperature** and **Top P** are silently ignored for `aws/*` models — Bedrock rejects them on these models, so the proxy strips them rather than failing your message.

Open WebUI starts automatically alongside the LiteLLM proxy (only when a profile shipped models). It runs in single-user mode with authentication disabled, since it listens on localhost only inside your dev container. Its database and uploads persist to `~/.data/open-webui/`, and logs are written to `~/.data/open-webui/open-webui.log`.

These services (LiteLLM, Open WebUI, the Open Terminal sandbox, and SearXNG) are managed by [supervisord](http://supervisord.org), which restarts any that crash. Its status dashboard is at **`http://localhost:6090`** (forwarded to your host); inside the container you can also run `supervisorctl status`. (A shell alias points `supervisorctl` at this repo's config for you, since the control endpoint is `:6090` rather than its default `:9001` — so run it bare, without `-c`.)

The whole browser-chat stack (LiteLLM, Open WebUI, Open Terminal, SearXNG, and supervisord) ships as one dev container feature, `./features/chat`. It is **unchecked by default** in the feature prompt the spawn scripts run (see [Choosing Features](#choosing-features)) — it's the bulk of the image build, and the coding agents talk to their providers directly and don't need it. Pass `--chat` (or `-Chat` on Windows) to have it preselected instead:

```bash
.devcontainer/scripts/spawn.sh <target-directory> --chat   # POSIX
.devcontainer/scripts/spawn.ps1 <target-directory> -Chat   # Windows
```

The flag only sets the default; you can flip the answer at the prompt either way.

Including or omitting the stack is a genuine build-time difference (and ~1.2 GB of RAM at runtime), because the feature either installs into the image or it doesn't. To add it to an already-spawned project, uncomment the `"./features/chat": {}` entry in `.devcontainer/devcontainer.json` and rebuild the container.

**Autostart is a second, separate question**, asked right after the feature list whenever the stack ends up included:

```
Start the browser chat stack automatically on container start? [Y/n]
```

Yes (the default) writes `CHAT_AUTOSTART=1` to `.devcontainer/devcontainer.env`; no writes `CHAT_AUTOSTART=0`. The feature's `poststart.sh` reads that variable on **every** container start: set to `1` it brings the four services up and waits for them to answer before the start finishes, otherwise it prints a one-line notice and returns immediately, leaving the stack one command away — run `start_chat_stack` in the container whenever you want it.

Unlike the feature list, this answer isn't baked into the image: it's an ordinary environment variable, so change your mind by editing `CHAT_AUTOSTART` in `.devcontainer/devcontainer.env` and rebuilding (Docker reads that file when the container is *created*, so reopening or restarting reuses the value from the last create — the same caveat as every other variable in there). No re-spawn, no feature change. That's why the question is also asked on the adopted-project path, where the feature prompt deliberately isn't, and why a project without the chat feature gets no `CHAT_AUTOSTART` line at all.

That one entry is all a project needs: the feature is self-contained. Everything it requires at create and start time ships inside `.devcontainer/features/chat/` and runs from the feature's own `postCreateCommand`/`postStartCommand` — `postcreate.sh` generates the LiteLLM config from the configured profiles and installs the `supervisorctl` alias, `poststart.sh` brings the stack up via `supervisord/start.sh` (which lives next to the static `supervisord.conf` and the per-service wrappers it drives). The project's own `postcreate.sh`/`poststart.sh` contain nothing chat-related.

In a container that already has the stack, you can stop or start the services on demand (no rebuild needed):

```bash
supervisorctl stop all
supervisorctl start all
```

> **Blank responses after an Open WebUI version upgrade?** Your browser is serving the old version's cached frontend. Clear it: DevTools (F12) → **Application** → **Storage** → **Clear site data**, then reload. (A plain hard refresh doesn't fully clear it.)

Models default to **native function calling**, so when you attach a tool the model invokes it through the provider's real tool-calling API rather than Open WebUI's prompt-injected fallback. You can change this per model or per chat from the Advanced Params / Chat Controls settings in the UI.

### Web Search (SearXNG)

Web search is pre-wired to a local [SearXNG](https://docs.searxng.org) metasearch engine that runs **inside this container**, so the chat can pull in live results without any external search API key. It's already configured — just toggle **Web Search** on in the chat controls and the model can search the web and cite what it finds.

SearXNG starts automatically alongside Open WebUI (only when a profile shipped models). Unlike the other services it listens on localhost only and is **not** forwarded to your host — it's an internal backend for the chat UI, not something you browse directly. Its generated settings, secret key, and logs persist to `~/.data/searxng/`.

### Open Terminal

The chat UI is pre-wired to [Open Terminal](https://github.com/open-webui/open-terminal) — a sandbox API that gives the AI an environment **inside this container** to run shell commands, read and write files, and execute code. Like SearXNG it listens on localhost only and is **not** forwarded to your host — Open WebUI's backend reaches it over `127.0.0.1`, so there's no host-facing endpoint to browse. It's already configured as an integration, so there's nothing to set up: in a chat, attach the terminal from the chat controls and the model can use it as a tool.

The AI works in your project's `.devcontainer/work/` sub-directory — a personal scratch area that is **not tracked by version control** (see `.devcontainer/work/README.md`), so files the AI creates or generates stay out of git by default rather than cluttering your repo. The directory lives under your workspace and persists across rebuilds. The terminal listens on localhost only and is protected by a bearer key (generated once and stored at `~/.data/open-terminal/api-key`).

> **Note:** Commands run as your container user with full permissions inside the dev container. That's intentional for a single-user dev environment — but it's why the terminal is never exposed beyond localhost.

## Claude Code Settings

The Claude Code defaults (commit/PR attribution and the statusline) ship inside the Claude Code feature - `.devcontainer/features/claude/default-settings.json` and `statusline-command.sh`. On every container create, the feature's create step merges them into your **user-level** `~/.claude/settings.json` (on the persistent data volume), with your existing settings winning at every key - so it seeds once and never clobbers a change you made later, and your project's working tree is left untouched. The statusline is invoked as bare `statusline`, a symlink the same step puts on your `PATH`, which is what lets one user-level settings file work across every project. To change the defaults, edit the files under `.devcontainer/features/claude/`.

Nothing is written to a project-level `.claude/settings.json`; if you create one yourself it is yours alone, and the container will not touch it. (Adopting this `.devcontainer/` in an existing project therefore adds no files to it.)

### Pinned models

Which Bedrock model each of `opus`/`sonnet`/`haiku` resolves to is defined per profile in `.devcontainer/profiles/<profile>/claude.json`, alongside the equivalent files for the other agents (`litellm.json`, `opencode.json`, `models.json`, `config.toml`, `grok.toml`). On every container create, `postcreate.sh` merges the configured profiles' entries into the `env` block of the **user-level** `~/.claude/settings.json`; the `/model` picker is left stock, so every family listed there — Fable included — is offered. Because the fragment is tracked in git and re-read on every create, a model bump reaches you on your next rebuild after a `git pull`. LiteLLM, OpenCode, and Pi are configured to the same three families (Fable 5.1, Opus 5, Sonnet 5) from the sibling files in each profile directory, plus xAI's Grok 4.6, which is also the model behind the Grok CLI (see below); Pi's picker shows only those, so Haiku is not offered there.

### Grok CLI

`grok` is xAI's coding agent, installed from the official installer at [x.ai/cli](https://x.ai/cli). Under the `aws` and `awsgov` profiles it is pointed at **Grok 4.6 on Amazon Bedrock** rather than at xAI's own service: the feature's create step (`features/grok/postcreate.sh`) writes `/etc/grok/managed_config.toml` from each profile's `grok.toml`, and that file defines the Bedrock model, makes it the default, and restricts the model picker to the profiles' models so the built-in xAI-hosted entries (which would need a `grok login` and send your code to x.ai) are not offered. Grok reaches Bedrock through its OpenAI-compatible endpoint ("Bedrock Mantle") with a short-lived **Bedrock API key** as the bearer token, minted on demand from the profile's AWS credentials by the `bedrock-api-key` helper — so no `grok login` and no xAI account are needed. Product telemetry is off by default for the same data-boundary reason. Anything in that managed file can be overridden in your own `~/.grok/config.toml` (for example `[models] allowed_models = []` to lift the picker restriction if you do sign in to xAI). Under the SEI profiles (`opal`, `etc`) Grok gets no models and is not configured.

Grok 4.6 is also offered to the other tools from the same profiles: LiteLLM (`aws/grok-4.6`, `awsgov/grok-4.6`), OpenCode (`amazon-bedrock/us.xai.grok-4.6` under `aws`, `amazon-govcloud/grok-4.6` under `awsgov`), and Pi (`amazon-bedrock/us.xai.grok-4.6`, **`aws` only**). Pi has no Grok under `awsgov`: Pi can only reach Bedrock through the Converse API, and in GovCloud Grok 4.6 refuses to run under the account's `aws_review` data-retention mode, which the Claude models require — the other tools avoid the conflict by using Bedrock's OpenAI-compatible endpoint instead.

Grok reads the same project instructions (`AGENTS.md`), skills (`~/.agents/skills`, `~/.claude/skills`) and `.claude/settings.json` permissions as the other agents, so find-skills is already visible to it. Its state lives in `~/.grok` on the persistent volume.

The merge only writes the keys the profile fragments define, so your own `env` entries in that file are left alone. If you ever want to override a pin for yourself, set it in `~/.claude/settings.local.json` — that takes precedence and isn't touched by creates.

### Auto permission mode

`postcreate.sh` also defaults Claude Code to **auto** permission mode, where a safety classifier approves routine actions instead of prompting you for each one — a sensible default in a container, where the agent is already sandboxed. It's seeded **only if you haven't set a mode yourself**, so switching modes with `/config` sticks across rebuilds. To pick a different default permanently, just set one — auto mode is never re-applied over an existing choice.

This is written to the **user-level** `~/.claude/settings.json` (persisted on the data volume), not the project's `.claude/settings.json`, because it has to be: `"auto"` is only honored from managed policy, user settings, or a CLI flag. The same key in a project settings file is ignored as repo-controllable, so putting it there would look right and do nothing. If auto mode is ever unavailable (a model that doesn't support it, or an org-side kill switch), Claude Code falls back to its default mode with a notice — it can't lock you out.

## Custom Skills

You can extend Claude Code's behavior with reusable prompt instructions by adding a `SKILL.md` file under `.claude/skills/<skill-name>/`. See the [Skills documentation](https://code.claude.com/docs/en/skills) for details.

### find-skills

**find-skills** — a skill that lets agents discover and install other skills from the [open skills ecosystem](https://skills.sh/) via the `npx skills` package manager — is installed for you on every container create. It's part of the container rather than the project: `postcreate.sh` installs it at **user scope** into `~/.agents/skills/` (shared by Codex, OpenCode, and Grok CLI) plus `~/.claude/skills/` and `~/.pi/agent/skills/`, so it's available to every agent in every project you open and adds nothing to your working tree. Re-running the create also upgrades it to the latest version.

You can install and manage skills yourself at any time with `npx skills add <owner/repo>`, `npx skills find <query>`, and `npx skills list` — add `-g` to install at user scope alongside find-skills instead of into the project. To drop find-skills for the current container, run `npx skills remove -g -s find-skills -y`; the next create reinstalls it.

## Choosing Features

Every concern in this container — each coding agent, the Bedrock account gates, the browser-chat stack — is a dev container feature listed in `.devcontainer/devcontainer.json`. Listing a feature is the whole opt-in: each one owns both its build-time `install.sh` and its create-time `postcreate.sh`, so nothing has to be added to the project's own `scripts/postcreate.sh` to go with it.

The spawn scripts ask which of them to build, right after the CUI and profile questions:

```
  Features

  [x] gets installed. Defaults follow the profile(s) you picked.

  1. [x] claude    Claude Code
  2. [ ] codex     Codex CLI  (no config for awsgov)
  3. [x] opencode  OpenCode
  4. [x] pi        Pi Coding Agent
  5. [x] grok      Grok Build
     [x] bedrock   Bedrock account gates (retention, GovCloud access)  (automatic - follows the profiles)
  6. [ ] chat      Browser chat stack - adds minutes to the build

Customize the feature set? [y/N]
```

The list is always shown, because it's how you see what your profile choice implied; the question after it decides whether you're asked for anything else. Press Enter (or answer `n`) to take those defaults, or answer `y` to get `Numbers to flip (comma or space separated, Enter to accept):` and toggle entries until you're happy.

The defaults come from the profiles you just selected: a feature is unchecked when no selected profile ships the config fragment it needs (Codex has no GovCloud route, for instance, so it is unchecked under `awsgov` alone), and `chat` is unchecked unless you passed `--chat`/`-Chat`.

**`bedrock` has no number, and that's deliberate.** It isn't a tool you'd choose — it's the AWS *account* gates (data retention, GovCloud model entitlement) that every agent reaching Bedrock needs — so it simply follows the profiles: on when a selected profile targets Bedrock, off otherwise. It's printed so the derived choice is visible, but it can't be flipped, because turning it off while keeping an AWS profile would leave you with models that don't answer and no obvious reason why. If you really want to build without it, comment its line out in `devcontainer.json` after the spawn.

**If `chat` ends up on, one more question follows the list:** whether the stack should start itself on every container start (`CHAT_AUTOSTART`). That one is a runtime setting rather than a build-time one — see [Browser Chat UI](#browser-chat-ui-open-webui) for what it does and how to change it later.

**One feature isn't listed at all: `./features/_template_`.** It installs nothing — it exists only to pull in PowerShell through `dependsOn`, which the maintainers of the template repository need for testing `setup.ps1`/`spawn.ps1` and no project ever does (those scripts run on your *host*, not in the container). Setup switches it on or off from the root `.template-container` marker, the same file the CUI question checks, so a spawned or adopted project always gets it commented out and skips the layer. If agent-dev ever needs other maintenance-only tooling, it goes in that feature's `dependsOn` and stays out of your image the same way.

Anything you turn off is **commented out** rather than deleted, so `devcontainer.json` remains a record of what's available:

```jsonc
    "./features/claude": {},
    // "./features/chat": {},
```

To change your mind later, uncomment (or comment) the line and rebuild the container. Nothing else needs to change.

**Why this is asked before the container exists.** Features are fetched and installed while the **image is being built**, from the `devcontainer.json` the Dev Containers CLI parsed *before* `initializeCommand` ran. So no in-container setting and no variable in `devcontainer.env` can prevent an install — editing `devcontainer.json` on the host, ahead of the build, is the only lever. That's why the question belongs to spawn and isn't repeated when you open the project; an answer given then could only take effect on the *next* build. It matters most for `./features/bedrock`, which pulls in the AWS CLI feature through `dependsOn`: an `opal`-only project would otherwise build both for nothing.

You get this prompt when you spawn **into an existing project** too — spawning is the recommended way to adopt one, precisely because the feature question has to be answered before the first build. If instead you copied `.devcontainer/` in by hand, there was no spawn step and so no feature prompt — edit `devcontainer.json` by hand before the first build.

## Starting a Git Repository

The last thing the spawn scripts ask is whether to initialize Git. Answer yes and you get a repository on `main` with a single **Initial commit** holding the finished project — the profiles you kept, the features you chose, everything. `--git`/`-Git` preselects "yes" for that prompt; it no longer performs the init itself.

A few details worth knowing:

- **`devcontainer.env` is never committed.** It holds the API keys you entered a moment earlier, and it's ignored by `.devcontainer/.gitignore`. Setup verifies that rule is in place before it stages anything — if it isn't, you get the repository but nothing staged, and a warning, because that one line is what keeps your keys out of Git history.
- **Your identity stays where you put it.** If you have a global `user.name`/`user.email`, the commit uses it and nothing is written to your config. If you don't, setup asks and sets it **for this repository only** (`git config`, not `--global`).
- **Nothing inside the container ever runs Git.** Creates don't dirty your working tree, so there is nothing to auto-commit — your history is only ever written by you (or by that one initial commit, if you asked for it).
- **An existing repository is left completely alone**, whether you spawned into it or copied `.devcontainer/` in by hand. `--git`/`-Git` changes nothing there: setup skips the init with a note when the directory is already inside a repository, so `.devcontainer/` simply shows up as an untracked addition for you to review and commit. `devcontainer.env` is ignored either way.

## Renaming a Project

To rename an existing project (folder, container, and volume), run the rename script from the **host terminal** (outside the container):

**macOS/Linux:**

```bash
.devcontainer/scripts/rename.sh new-project-name
```

**Windows (PowerShell):**

```powershell
.devcontainer\scripts\rename.ps1 new-project-name
```

The script will confirm before making changes (default is no). It deletes the old Docker container and volume, then renames the folder. The container and volume are recreated automatically when you reopen in VS Code.

## Updating Credentials

`setup.sh` (`setup.ps1` on Windows) owns every question this project asks, in four modes:

| Invocation | When it runs | What it does |
|------------|--------------|--------------|
| `--spawning [--chat] [--git]` | From the spawn scripts, on the host, before the project is opened | Asks everything: CUI, profiles, features, the profiles' API keys, and whether to initialize Git. Writes `devcontainer.env`, comments out the unselected features, and commits |
| `--ensure` | From `initializeCommand` (`scripts/init` / `init.cmd`), on every container start | Nothing at all when `devcontainer.env` exists. Runs the first-run flow when it doesn't — the adopted-project case |
| `<profile>...` | By hand | Updates or adds those profiles' credentials |
| *(no arguments)* | By hand | Offers to fill in any credential left blank, then prints the configured profiles and the commands to change them |

**Everything is asked on the host, at spawn time.** `devcontainer.env` is gitignored, so a real key is in no danger of being committed there, and a host terminal is a much better place to paste one than `initializeCommand`'s — which is often not a terminal at all. So once that file exists, `--ensure` does nothing: opening or reopening the project prompts for nothing and can't fail over a missing key. A key you skipped at the prompt stays blank until you run `setup.sh <profile>` yourself.

The one path that still asks at first open is a **hand-copied** `.devcontainer/` (dropped into an existing project, so no spawn ran): there's no `devcontainer.env`, so `--ensure` asks for CUI, profiles, and keys in the **Dev Containers** output panel. If it has no terminal to ask on it warns, builds anyway, and tells you to run `setup.sh` on the host — it never fails the build. Pointing a spawn script at the project instead is the better route: it asks everything on the host, adds nothing but `.devcontainer/`, and leaves the first open with nothing to ask.

When credentials rotate (e.g., AWS long-term keys expire after 14 days), run the setup script from a terminal **inside the dev container**:

```bash
.devcontainer/scripts/setup.sh aws           # Update AWS credentials
.devcontainer/scripts/setup.sh opal          # Update OPAL credentials
.devcontainer/scripts/setup.sh aws opal      # Update multiple profiles at once
```

Running with no arguments shows your configured profiles and the commands to update each one:

```bash
.devcontainer/scripts/setup.sh
```

To add a profile that wasn't included in the initial setup, pass its name the same way:

```bash
.devcontainer/scripts/setup.sh etc           # Add and configure a new profile
```

Only the profiles still in `.devcontainer/profiles/` can be added this way, and that set was decided once by the CUI question: whichever side of the answer the project is not on was **deleted**, so a public project has `aws` alone and a CUI project has `awsgov`, `opal` and `etc`. Naming a deleted profile is an error, which is the point — the answer needs no second check.

After updating credentials, rebuild the container to load the new environment variables:

- Open the command palette (F1 or Cmd/Ctrl+Shift+P)
- Run: `Dev Containers: Rebuild Container`

`devcontainer.env` is a Docker `--env-file`, read **once, when the container is created** — so every process already running, including this terminal and the agents in it, still has the old values, and reopening or restarting the container reuses what Docker captured at that create. Only a rebuild re-reads the file. Setup prints both of these when it finishes; it cannot apply the change for you, because no script can alter the environment of the shell that called it.

To keep working without a rebuild, load the file into the **current shell** — anything you start from it (an agent, `aws`) then inherits the new values:

```bash
set -a; . .devcontainer/devcontainer.env; set +a
```

`set -a` is what exports the values instead of leaving them shell-local. A bare `source` is not equivalent: it happens to work for a variable the container already has (Docker exported every line of the file when the container was created, blank credentials included, and assigning to an already-exported variable keeps it exported) and silently does nothing for one that is new since then. Treat it as a stopgap for one shell: nothing outside it is affected, and bash reads the file slightly differently than Docker does (it keeps the trailing `\r` of a file written on Windows, and treats whitespace, `$` or a backtick in a value as syntax). **Adding a profile always needs the rebuild** — each agent's config is regenerated from `CONFIGURED_PROFILES` on create, and no amount of sourcing does that.

## GovCloud Model Access

**Applies to the `awsgov` profile only.** A fresh GovCloud account cannot call the Anthropic models until model access is granted in **three** places:

1. The **Anthropic use-case form** — the "use case details" form the Bedrock console puts in front of you for Anthropic models. Required for Anthropic models specifically, and **account-level**. Automated; no console visit needed.
2. A **model-access agreement**, created from the **linked commercial account**. One agreement per model covers **both** GovCloud regions. Automated.
3. A **per-region entitlement**, which is what that region's Model access page in the GovCloud console grants. The two gov regions are independent. Automated too — the container requests it directly with the same (undocumented) call the console itself makes, so the console page is only a **fallback** if that request fails.

Until every gate is done, agent requests fail with an access or entitlement error that looks like a credential problem.

The account's **data retention mode**, which some models additionally require, is not one of these gates and is set for you on every container create — see [Data retention](#data-retention) below.

The **agreement** (gate 2) is attempted from the GovCloud account first, using the container's own credentials — in that case there is nothing else to fetch. If AWS refuses, the refusal is categorical:

> `AccessDeniedException`: The GovCloud account *&lt;id&gt;* cannot perform this operation. You must use your associated standard AWS account to establish a model access agreement or view the EULA in either us-east-1 or us-west-2 regions

Then the agreement has to be created from the **linked commercial account**, using the **bare** model id (`anthropic.claude-opus-5`) rather than the `us-gov.`-prefixed one — and **one agreement per model covers both GovCloud regions**, since "either us-east-1 or us-west-2" is a choice of endpoint, not a per-region mapping.

Three flags tell you where you are. Note that `authorizationStatus` is **account-level** and so is useless on its own — it goes green as soon as *any* agreement exists anywhere, including in the most common broken case:

| `agreementAvailability` | `entitlementAvailability` | Meaning | What to do |
|---|---|---|---|
| `NOT_AVAILABLE` | `NOT_AVAILABLE` | Fresh account — no agreement at all. | Gate 2: run the script; it creates the agreement, waits for it, then requests the entitlement in the same run. |
| `PENDING` | `NOT_AVAILABLE` | Agreement just created, still propagating (3–50 seconds). | Nothing — the script waits for it and carries on. |
| `AVAILABLE` | `NOT_AVAILABLE` | Agreement done, this region's entitlement missing. | Gate 3: requested automatically on the next container create, or run the script now. |
| `AVAILABLE` | `AVAILABLE` | Model access complete — the model is usable. | Nothing. |

The `./features/bedrock` feature's create step (`features/bedrock/postcreate.sh`) checks this on every container create, in the region the profile actually calls (`AWS_REGION`), for the models pinned in `profiles/awsgov/claude.json` — so the check can't drift from the models the agents use. When a model's agreement is in place but the region's entitlement isn't, **it requests the entitlement itself** (`Requesting entitlement for <model> in <region>...`, then `✓ enabled <model> in <region>`); that needs no decision from you, because the consent was the agreement's EULA. Anything it can't fix — no agreement yet, or a request AWS refused — is listed in a warning with the full reason and a pointer to the script below. The check is a Bedrock control-plane call, so no inference and no token spend, it costs about 2.5 seconds when everything is already fine, and it **never** fails the create.

To fix it, run this from a terminal **inside the dev container**:

```bash
.devcontainer/scripts/enable-govcloud-models.sh
```

The script checks current status and exits early if everything is already enabled, so it's safe to re-run — and each step is skipped when its gate is already met, so a re-run never re-does work or re-asks a confirmation you already gave. When enablement is needed it:

1. **Submits the Anthropic use-case form**, if the GovCloud account doesn't already have one. It asks you nothing: the answers describe the organisation rather than the project, so they're constants at the top of the script (`USE_CASE_*`) — company, website, industry, intended users and the use-case description, the same for every project. Edit them there if they ever change. The assembled form is still shown and you confirm before it's submitted — it's an attestation about how the models will be used, made under your name. The GovCloud account reads and writes its own form, so this step never needs anything extra. If a form is already on file it's printed and left alone — the form is write-once, so re-submitting it reports success and changes nothing; correcting one on file is a question for AWS Support, not this script.
2. **Creates the model-access agreement** for each model that doesn't have one — and only those; a model whose agreement is already in place is left alone. It tries this with the container's own GovCloud credentials first, so if that works you're done. Only if AWS refuses on authorization grounds does it prompt you to paste short-term credentials for the linked commercial account (e.g. exported from Kion) as an `export` block (it proceeds as soon as all three values are in, so nothing extra to type), verify them with STS so you see which account you're acting as, and create the agreement in `us-east-1` once per distinct model with the bare model id.
3. **Requests the per-region entitlement** for every model whose agreement is in place, with the container's own GovCloud credentials. No prompt: the consent for these models was the EULA in step 2, and this request only switches them on in the region that consent was given for. If step 2 just created an agreement, the script waits for it to land (up to two minutes) and entitles it in the **same run**, so a fresh account is finished in one pass.
4. Re-checks and reports what's left, split by what each remaining item actually needs.

If commercial credentials do get requested, they're parsed (never `eval`'d) and never written to `devcontainer.env` or any other file. Each use is scoped to a single call, so the container's own GovCloud credentials still serve the status probes, and they're cleared as soon as the agreements are done.

**If the entitlement request fails** (it uses an undocumented endpoint, so it could stop working without notice), the script prints AWS's full response and falls back to the console: the Model access page URL for the profile's region plus the models to enable there. In the console: **Bedrock → Model access → Modify model access**, select the models, **Submit**. The pages are per region and independent — enabling a model in `us-gov-west-1` does nothing for `us-gov-east-1` — so use the region the script names. Then re-run the script to confirm.

After access is granted, restart your agent to pick it up.

<a name="data-retention"></a>
### Data retention

Some models are only available when the account's data retention mode in the **invoking** region is permissive enough. This is separate from model access and fails at inference time rather than on any availability flag:

> `ValidationException`: The model returned the following errors: data retention mode 'default' is not available for this model

**Under the `aws` and `awsgov` profiles this container sets that mode to `aws_review` in the profile's region (`AWS_REGION`) on every container create**, from `features/bedrock/postcreate.sh` — so listing `./features/bedrock` in `devcontainer.json` is the opt-in, and a project that lists neither it nor a Bedrock profile is untouched. There is no prompt: `postCreateCommand` has no terminal to ask at, and a model that can't run at all is not a useful default. The mode is read first, so a region already on `aws_review` (or on the older, more permissive `provider_data_share`) is left exactly as it is and only gets a one-line note. When it does change something, the create log says so and prints what the mode authorizes.

Setting `aws_review` means:

- Your prompts and the models' completions are retained inside the AWS boundary for up to 30 days.
- AWS may have humans review that content, for models whose provider requires it.
- Your content is **not** shared with the model provider.
- It applies per region to the whole account — but each model's own allowed modes still govern what happens to its data, so models that permit zero retention are unaffected.

**Consider that, especially under the `awsgov` profile** — a CUI workload is exactly the case where AWS human review may not be acceptable. It is reversible at any time, and there is no console UI for it, so the API is the only way in either direction:

```bash
aws bedrock put-account-data-retention --mode inherit --region us-gov-east-1
```

(`inherit` is the unconfigured default: it defers to each model's own default rather than expressing a preference. The modes form an ordered scale — `none` < `default` < `aws_review` < `provider_data_share` — and a model is available when your effective mode is at or above what it requires; `inherit` is not on the scale, so it never satisfies a model that requires review. Today the **Fable** family is the only one that does; nothing released before Fable 5 is affected.)

The setting is per region and does **not** propagate, but only one region needs it: the `awsgov` profile calls the Anthropic models from `us-gov-east-1` only (`us-gov-west-1` appears there for a Mantle GPT model, which needs none of this).

See [Data retention](https://docs.aws.amazon.com/bedrock/latest/userguide/data-retention.html) in the Bedrock user guide.

## Herdr terminal workspaces

[Herdr](https://herdr.dev/) is installed by the local `./features/herdr` feature
using `curl -fsSL https://herdr.dev/install.sh | sh`. It installs the stable release
into the container user's `~/.local/bin`; the feature's create step (`features/herdr/postcreate.sh`) runs `herdr update` on every
create, with update failures allowed to warn without aborting setup.

A **session** is a background server (the default is enough to start). A
**workspace** groups a project, **tabs** organize its views, and each **pane** is a
real terminal. Herdr recognizes supported **agents** inside panes and shows
working, blocked, done, idle, or unknown state. In terminal mode, keys go to the
pane; prefix mode sends one command to Herdr; navigate mode stays in navigation.

1. Open a VS Code terminal inside the container, change into your project, and run
   `herdr`. Setup is automated, including skipping onboarding in the seeded config.
   If `HERDR_ENV=1`, you are already
   inside Herdr; use the current pane instead of launching a nested instance.
2. Run an agent such as `claude`, `codex`, `opencode`, `pi`, or `grok` in the pane.
   The same provider profiles apply. Herdr detects the agent automatically.
3. Start with the mouse: click tabs and panes, drag split borders, right-click for
   menus, and drag-select to copy. Use the right-click menu to split a pane.
4. For keyboard control, press **Ctrl+B**, release, then **V** to split right,
   **-** to split down, or **C** for a new tab. **Ctrl+B**, then **?** shows current
   bindings. If VS Code intercepts a chord, see Herdr's
   [keyboard guide](https://herdr.dev/docs/keyboard/).
5. Detach with **Ctrl+B**, then **Q**, or close the terminal. Run `herdr` again to
   reattach. Processes keep running while the container and Herdr server run.
6. When you intend to stop the session and all its pane processes, run
   `herdr server stop`.

`~/.config/herdr` points to `~/.data/herdr` on the persistent volume, saving
configuration, layouts, and logs. Container stops and rebuilds end running
processes. On next launch, Herdr restores saved layouts; supported agent
conversations can resume when their integrations recorded native session
references. Other panes return as new shells. Pane screen history stays at
Herdr's default (off). See [session state and restore](https://herdr.dev/docs/session-state/).

The initial config sets `[terminal] shell_mode = "login"` so panes load the
container's login environment, and puts Herdr-created Git worktrees under
`~/.data/herdr/worktrees` so their files survive rebuilds too. Existing configs are
preserved. Customize
`~/.config/herdr/config.toml`, then use `herdr server reload-config` to apply
supported changes; shell changes apply to new panes. `herdr --default-config`
prints all defaults. Herdr starts on demand and needs no forwarded port.

Every create installs or refreshes the official integrations for Claude Code,
Codex, OpenCode, Pi, and Grok with `herdr integration install <agent>`, after
profile generation. Claude, Codex, and Grok report native session references for
restore while Herdr detects state from the screen. Pi and OpenCode also report
lifecycle state directly. The installers preserve unrelated agent settings;
Codex's installer enables hooks in its user config, leaving generated provider
defaults in `/etc/codex/config.toml`. Restart agents after adding integrations
to an already-running container.

The [Herdr control skill](https://github.com/herdrdev/herdr/blob/master/skills/herdr/SKILL.md)
is installed automatically at user scope using the same layout as find-skills:

```bash
npx -y skills@latest add herdrdev/herdr -s herdr \
    -g --copy -a claude-code -a codex -a opencode -a pi -y
```

It teaches agents to inspect and control Herdr panes and requires `HERDR_ENV=1`.
Nothing is installed into the repository. Integrations and skill installation
are noninteractive and failure-tolerant; re-create the container to retry, or
run the corresponding install commands manually.

For troubleshooting, use `herdr --version`, `herdr integration status`,
`herdr agent list`, and `herdr agent explain <target> --json` from your Herdr session.
The [agent guide](https://herdr.dev/agent-guide.md) and
[integration documentation](https://herdr.dev/docs/integrations/) explain detection
and restore support.

## Data Persistence

Agent data (conversation history, projects, todos, settings), and shell history persist across container rebuilds via a single Docker named volume. You don't need to do anything to enable this — it works automatically.

The volume is named based on the project folder name (e.g., `my-project-data`) and mounted at `~/.data/`. It contains subdirectories for each agent and shell history:

| Subdirectory | Symlinked from | Contents |
|-------------|----------------|----------|
| `claude/` | `~/.claude` | Claude Code history, projects, todos, settings |
| `opencode/` | `~/.local/share/opencode` | OpenCode sessions and data |
| `opencode-state/` | `~/.local/state/opencode` | OpenCode model selection and prompt history |
| `pi/` | `~/.pi` | Pi Coding Agent data |
| `codex/` | `~/.codex` | Codex CLI config and data |
| `grok/` | `~/.grok` | Grok CLI sessions, settings, and the `grok` binary itself |
| `herdr/` | `~/.config/herdr` | Herdr configuration, saved layouts, and logs |
| `litellm/` | — | LiteLLM proxy logs |
| `open-webui/` | — | Open WebUI database, uploads, secret key, and logs |
| `open-terminal/` | — | Open Terminal API key and logs |
| `searxng/` | — | SearXNG secret key, generated settings, and logs |
| `supervisor/` | — | supervisord pidfile and log |
| `shell-history/` | — | Bash and Zsh history files |

Every instance of the dev container uses a separate volume so their data is fully isolated.

To clear persisted data, run this command from the **host terminal** (outside the container):

```bash
docker volume rm my-project-data    # Clear all persisted data
```

> **Note:** Volumes persist even when the container is deleted. They are only removed with explicit `docker volume rm` or `docker volume prune`.
