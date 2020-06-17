![GHOSTS Logo](https://github.com/cmu-sei/GHOSTS/blob/master/assets/ghosts-logo.jpg)

Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

# GHOSTS NPC AUTOMATION

GHOSTS is a framework for highly-complex, realistic non-player character (NPC) orchestration. It essentially realistically mimics the behavior of the different types of people you might encounter on any array of different typical office or enterprise networks. The system makes it possible for cybersecurity experts to test their skills and realistically train to defend real networks with real NPC players operating on those networks doing the things we might expect them to do: Create documents, access systems, browse the web, click, run commands, and so on.

As a result of the system checks required in order for NPCs to be situationally aware, GHOSTS also does health reporting for all configured clients on a given instance.

## Key Links

[Installation and configuration information is maintained on our wiki](https://github.com/cmu-sei/GHOSTS/wiki)

[Don't hesitate to submit issues and feature requests here](https://github.com/cmu-sei/GHOSTS/issues)

## Platform Components

### Ghosts.Client (Windows)
.NET Console app (but built as forms app so that it is hidden) - requires .NET framework v4.6.1 or higher. Client works on both Windows 7 and Windows 10.

### Ghosts.Client (Linux)
dotnetcore app built to run silently. Client tested on centos, alpine and kali distributions. We typically use this for red teaming and "outside" traffic generation or administration simulation.

### Ghosts.Api
.NET Core 2.0 MVC Code-First containing both the api calls for the client (and corresponding api calls the ui needs) in one. 

Uses postgres on the backend because there is not much that postgres can't do.

## LEGAL

[DISTRIBUTION STATEMENT A] This material has been approved for public release and unlimited distribution.
