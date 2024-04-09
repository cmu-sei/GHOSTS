# GHOSTS SPECTRE Overview

???+ info "GHOSTS SPECTRE Source Code"
    The [GHOSTS SPECTRE Source Code Repository](https://github.com/cmu-sei/GHOSTS-SPECTRE) is hosted on GitHub, but is now archived.

    Any development on SPECTRE will be done in the main GHOSTS repository.

SPECTRE was our attempt to reduce that agent browsing patterns appear as "computer random".[^1] This module modifies user web browser timelines so that they more closely match that agent's preferences over time. We expect this simple model of ML to be used for other types of agent activity in the future.

Agent browsing patterns are no longer random and match different types of users on a network. These patterns improve over time.

Also, this simple model of ML can now be used for other types of agent activity. This makes for an exciting future on the GHOSTS platform.

Using Machine Learning to Increase NPC Fidelity with Dynamic Preferences Used in Forward-Looking Decisions

As GHOSTS agents make more informed, and hopefully, more complex decisions, there is a need for each agent to have a system of preferences existing at the time the agent is created, and for an ability to update those preferences over time as the agent continues to make decisions and measure the outcome of those decisions afterwards.

SPECTRE provides GHOSTS enables agents to make preferenced decisions and to use the outcome of those decisions to learn and evaluate future choices more intelligently.

## How it works

SPECTRE currently has two components:

### Preference Engine

1. An administrator creates the types of personas they wish to have GHOSTS agents represent
2. GHOSTS agents report in their browser history to the GHOSTS C2 API
3. If the agent has no persona, SPECTRE assigns one at random
4. The persona assignment randomly creates different preferences for the agent based on persona settings (not unlike character creation in D&D)
5. Preferences are a preference and its numeric value (-100 to 100) which roughly represents how much an agent likes or dislikes something
6. Preferences can be any `key:value` pair, such as:
         "sports":50
         "kali":35
         "127.0.0.1":77
7. Preferences of 0 mean that the agent is indifferent (or has no preference)
8. All preference history is stored so that in the (near) future we can track an agent's preferences over time
9. It is assumed that high negative preferences would have agents avoiding those values (e.g. for "onions":-100 one would assume an agent is avoiding eating onions at all costs.)

### Machine Learning

The incoming GHOSTS agent browsing activity can be attenuated to individual agent preferences after they are assigned some number of preferences based on default persona profiles. SPECTRE will aggregate this information periodically and perform model training and testing against that browsing activity, and recommend new browsing patterns for that agent to execute. This creates a new activity timeline for the agent. This cycle is referred to as a "Test". After any given test, that information would be used to inform the next round of ML testing to be done.

## Installation

SPECTRE installs alongside an existing GHOSTS installation as a separate docker container. With a few configuration changes, you should be up and running in minimal time.

There are only two configuration settings, both contained within `appsettings.json`:

```json
"DefaultConnection": "Host=localhost;Port=5432;Database=preferences;User Id=ghosts;Password=scotty@1;Pooling=true;Command Timeout=9900",
"GhostsApiUrl": "http://localhost:5000"
```

The first setting is for your connection to a necessary Postgres database, which SPECTRE will use for its operations. It is fine to host this on the same machine or container that you might be using for GHOSTS itself. In this case, either use the same user as GHOSTS or create a new user for SPECTRE.

The second setting is for SPECTRE to access the GHOSTS API endpoints. This is used to get information about machines and to update their timelines, based on SPECTRE findings and executions.

[^1]: This work is detailed in the technical report [_Using Machine Learning to Increase NPC Fidelity_](https://resources.sei.cmu.edu/library/asset-view.cfm?assetid=743896). Some of the team also discussed this project in an SEI podcast episode, entitled [_ML-Driven Decision-Making in Realistic Cyber Exercises_](https://resources.sei.cmu.edu/library/asset-view.cfm?assetid=888745).

## Quick Start

Spectre is rather like an add-on -- it sits alongside the core API and uses that system to get its initial agent information, process their timelines, and then post updated timelines back to the API for dissemination to the clients.

- There is one setting in the spectre `appsettings.json` file that may need updating based on the install - it is how Spectre will connect to the GHOSTS Core API: `"GhostsApiUrl": "http://host.docker.internal:52388"` — update this to your core API host and port.
- Now go to Spectre's host:port/swagger to bring up the API.
- `GET /Agents` will show you what you have under SPECTRE control - at the start, it is likely no agents. We need to sync with core GHOSTS Core API to get its agents into SPECTRE.
- So, we need to run `POST /Agents/sync` once to pull the agents in the core API over to SPECTRE.
- Now Spectre's `GET /Agents` will show you the same agents from ghosts Core API.
- You can now run a browse recommendations job.
