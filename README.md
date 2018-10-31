Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

# Ghosts NPC Automation

GHOSTS is a framework for cyber exercise NPC automation. As a result of the system checks required in order for NPCs to be situationally aware, GHOSTS also does health reporting for all configured clients on a given instance.

Formally, GHOSTS is an acronym representing a framework for thinking about fully autonomous exercise delivery:

### (G)ENERAL HOSTS
The system handles a great deal of variable activity. E.g. an actor could be friendly, entirely neutral, or openly hostile - the system makes provisions for the existence of all types of actors.

## PLATFORM ELEMENTS

### Ghosts.Domain
DTO objects and shared functionality that get passed back and forth between client and api. This project or dll is not installed separately.

### Ghosts.Client (Windows)
.NET Console app (but built as forms app so that it is hidden) - requires .NET framework v4.6.1 or higher. Client works on both Windows 7 and Windows 10.

### Ghosts.Api

.NET Core 2.0 MVC Code-First containing both the api calls for the client (and corresponding api calls the ui needs) in one. 

Uses postgres on the backend because there is not much that postgres can't do.

### Ghosts.App
The Angular C2 app. Calls the above API.

## Legal

[DISTRIBUTION STATEMENT A] This material has been approved for public release and unlimited distribution.