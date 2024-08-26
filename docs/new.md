# What's New 🆕 in GHOSTS v8 👻

???+ info "Welcome to GHOSTS"
    Welcome to what's new in the GHOSTS framework. Use this page to review the latest changes.

## What's new in 8.2

- [GHOSTS now has a UI](core/ui.md)! Manage machines and machine groups, deploy new timelines, and view activities. 😍
- [GHOSTS Shadows](shadows/index.md) provides integrations with an LLM for the purposes of GHOSTS agents. It provides multiple models for activities, chat, content, social, and whatever else you may need. 👻
- [GHOSTS LITE](core/lite.md) is a stripped-down version of GHOSTS that is designed to be more lightweight and require less resources to run certain activities in training, exercises, and simulations. 👀
- Fixes #385 default guid 00000000-0000-0000-0000-000000000000 animator NPC bug. 🐛
- Fixes #384 client path bug. 🐛
- Fixes animation cancellation token bugs (chat). 🐛
- Updated documentation for Animations (Thank you to the SEI's TMR team). 🤙

## What's new in 8.1

- GHOSTS LITE BETA - when you want to conserve resources needed and participants won't access that machine directly.
- API clean up, particularly machine updates and groups.
- Made sample json objects easier to understand and submit the swagger generated defaults
- API robustness
- MachineGroups cleanup
- Timeline delivery by machine and by group

## What's new in 8.0

### Enhancements:

- ANIMATOR and SPECTRE functionality merged into the GHOSTS API proper. Those projects are now archived. 📁
- We have moved off mongo — all that data is now stored in Postgres ❤️.
- General docker-compose cleanup in hopes install and initial configuration problems are greatly minimized. 🧹
- Animator job management is now done through the UI. 🖥️
- This release is the beginning of moving from web requests to websockets — NPCs are now "always connected" 📶 and do not need to "check in" or poll the server in order to execute activities, although these are still being built out, and the old system where clients poll the API server is still in place.
- Adds ability to configure random timespans to a timeline for delay before or delay after some activity is executed. ⏳
- Cleans up and simplifies configuration on client and server systems (breaking change). 🤖
- "One docker-compose To Rule Them All" 💍 — we were seeing installs fail because of piecemeal installation of various ghosts modules. Now, there is a single docker-compose file that will install all of the necessary components for a GHOSTS system.
- API endpoints have been re-organized in a more logical fashion. 🗂️
- Added a favicon. 💅

### Bug Fixes:

- Updates Grafana docker compose to not use root. 🚫👤
- Cleans up containers and ensures all are amd64 (not arm!). 💻
- Fixes a bug to get the cmd window to stay open after running a command, despite attempts to use parameters to get it to stay open. 🪟
- Cmd now stays open, but this can have different outcomes based on the types of commands passed in, plus ghosts will reap windows in order to save on machine resources, so a used command window won't always be there later. ⚙️
- New improved RDP, fixes an array of different connectivity bugs. 🌐
- Fixes bugs in delay before and after with fixed integer/long values over int maximum. 🔢
- Adds "log" level to application logs. 📝
- Fixes documentation bug in machine group timelines. 🐛
