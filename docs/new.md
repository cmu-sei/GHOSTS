# What's New

Welcome to what's new in the GHOSTS framework. Use this page to review the latest changes.

## GHOSTS v8

- Beginning of moving to websockets — NPCs are now "always connected" and do not need to "check in" in order to execute activities, althought these are still being built out, and the old check-in system is still in place for the time being.
- Adds ability to configure random timespans to a timeline for delaybefore or delayafter some activity is executed.
- Cleans up and simplifies configuration on client and server systems (breaking change).
- Updates all framework and dependency versions to latest (.NET8).
  - NetOffice binary update, particularly negatively impacting Excel, forcing it to continually restart. Updated to latest (and new source) for NetOffice libraries across all Office products.

- "One docker-compose To Rule Them All" — we were seeing installs fail because of piecemeal installation of various ghosts modules. Now, there is a single docker-compose file that will install all of the necessary components for a GHOSTS system.
  - Animator and SPECTRE merged into ghosts api proper.
- Animator job management is now done through the UI.
- API endpoints have been re-organized in a more logical fashion.
- Added a favicon.

## Bug Fixes

- Updates Grafana docker compose to not use root.
- Cleans up containers and ensures all are amd64.
- Fixes a bug to get the cmd window to stay open after running a command, despite attempts to use parameters to get it to stay open.
  - Cmd now stays open, but this can have different outcomes based on the types of commands passed in, plus ghosts will reap windows in order to save on machine resources, so a used command window won't always be there later.
- New improved RDP, fixes an array of different connectivity bugs.
- Fixes bugs in delay before and after with fixed integer/long values over int maximum.
- Adds "log" level to application logs.
- Fixes documentation bug in machine group timelines.
