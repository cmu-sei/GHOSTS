# GHOSTS NPC Framework

GHOSTS simulates user activity on a computer, like creating documents, browsing websites, and downloading files. It drives various popular applications on both Windows and Linux machines. Whether you're a friendly administrator or a cyber adversary, GHOSTS can replicate your expected behavior.

GHOSTS has many use cases in cyber experimentation, training and exercise, most notably for bringing non-player characters (NPCs) to life. However, it can also be used for other purposes requiring realistic activity on a computer.

There is a [short demonstration video available on YouTube](https://www.youtube.com/watch?v=EkwK-cqwjjA) (3:03).

---

## Key Links

- [Quick Start: Installation from distribution binaries](https://cmu-sei.github.io/GHOSTS/quickstart/)
- [GHOSTS Documentation](https://cmu-sei.github.io/GHOSTS/)
- [Submit issues and feature requests](https://github.com/cmu-sei/GHOSTS/issues)

---

**Version 8.2 is here!** 

GHOSTS has undergone major updates, including breaking changes introduced in version 8.0. Version 8 absorbed the [ANIMATOR](https://github.com/cmu-sei/GHOSTS-ANIMATOR) and [SPECTRE](https://github.com/cmu-sei/GHOSTS-SPECTRE) modules, simplifying the installation, configuration, and administration of a GHOSTS instance, while bringing further capabilities to core agents by integrating previously segregated data. With version 8.2, more improvements and features are introduced, making it easier and more powerful to use.

### Breaking Changes in Version 8.0:
1. Configuration file changes.
2. Database changes.
3. API endpoint changes.

There is no upgrade path from previous versions — a fresh install is required for version 8 and beyond.

---

## What's New in GHOSTS v8.2 🆕
Welcome to the latest version of GHOSTS! Here’s a look at what’s new and improved in v8.2:

- [GHOSTS now has a UI](src/ghosts.ui) — Manage machines, machine groups, deploy new timelines, and view activities through a sleek interface. 😍
- [GHOSTS Shadows](src/ghosts.shadows) now integrates with large language models (LLMs) for GHOSTS agents, offering various models for activities, chat, content generation, social interactions, and more. 👻
- [GHOSTS LITE](src/Ghosts.Client.Lite) is a lightweight version of GHOSTS that requires fewer resources, making it ideal for training, exercises, and simulations on minimal hardware. 👀
- Fixes #385: Default GUID issue with Animator NPCs.
- Fixes #384: Client path bug.
- Resolved various animation cancellation token bugs in chat features.
- Updated documentation for Animations (with contributions from SEI's TMR team). 🤙

## What's New in GHOSTS v8.1:
- GHOSTS LITE BETA: A lighter version designed for scenarios where participants won't directly access the machine.
- API cleanup, particularly around machine updates and groups.
- Simplified sample JSON objects for better understanding and easier submission.
- Improved API robustness and machine group management.
- Enhanced timeline delivery per machine and group.

## What's New in GHOSTS v8.0:
### Enhancements:
- Merged ANIMATOR and SPECTRE functionalities into the core GHOSTS API. Both projects are now archived.
- Migrated all data from MongoDB to PostgreSQL for improved performance and management.
- Cleaned up the Docker Compose setup to minimize installation and configuration issues.
- Animator job management can now be handled directly through the UI.
- Transitioning from HTTP requests to WebSockets: NPCs are "always connected" and no longer need to poll the server to execute activities.
- Added ability to configure random delays (before or after an activity) within timelines.
- Simplified configuration for both client and server systems (breaking change).
- Consolidated installation into a single `docker-compose` file, streamlining the process.
- Reorganized API endpoints for improved structure and accessibility.
- Added a favicon for UI polish.

### Bug Fixes:
- Grafana Docker Compose no longer uses the root user.
- Ensured all Docker containers are `amd64` (not `arm`).
- Fixed issues with command windows staying open after execution.
- Improved RDP functionality, fixing connectivity bugs.
- Resolved issues with delay values exceeding integer limits.
- Added "log" level to application logs for better insight into operations.
- Fixed documentation bug related to machine group timelines.

---

## Platform Components

### Ghosts Clients (Windows & Linux)
GHOSTS clients simulate user-like behavior on a machine. Configurable tasks include:
- Web browsing
- Document creation and editing
- Email sending and responding
- Running terminal commands
- And more.

### Ghosts API Server
The API server enables clients to interact with GHOSTS and provides the following functionality:
- Manage clients, add/remove groups, etc.
- Retrieve/manage client activity data.
- Orchestrate new tasks for clients to perform.

### Ghosts UI
The UI offers an interface for managing machines, groups, and deploying timelines, providing an easier way to interact with the GHOSTS system.

### Ghosts Lite
A minimalistic version of the Windows GHOSTS client, suitable for running on systems with limited resources.

### Pandora Content Server
A server that provides requested content to GHOSTS clients, dynamically generating responses or serving static content for training, exercises, and red-teaming.

### Pandora Socializer Server
This server allows GHOSTS clients to interact with simulated social media content, enhancing the realism of cyber scenarios.

---

## License
[DISTRIBUTION STATEMENT A] This material has been approved for public release and unlimited distribution.
Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.
