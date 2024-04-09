# What's New 🆕 in GHOSTS v8 👻

???+ info "Welcome to GHOSTS"
    Welcome to what's new in the GHOSTS framework. Use this page to review the latest changes.

## Enhancements:

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

## Bug Fixes:

- Updates Grafana docker compose to not use root. 🚫👤
- Cleans up containers and ensures all are amd64 (not arm!). 💻
- Fixes a bug to get the cmd window to stay open after running a command, despite attempts to use parameters to get it to stay open. 🪟
- Cmd now stays open, but this can have different outcomes based on the types of commands passed in, plus ghosts will reap windows in order to save on machine resources, so a used command window won't always be there later. ⚙️
- New improved RDP, fixes an array of different connectivity bugs. 🌐
- Fixes bugs in delay before and after with fixed integer/long values over int maximum. 🔢
- Adds "log" level to application logs. 📝
- Fixes documentation bug in machine group timelines. 🐛
