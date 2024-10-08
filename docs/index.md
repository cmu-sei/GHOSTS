# GHOSTS NPC Framework

Developed by Carnegie Mellon University's Software Engineering Institute (SEI), the **GHOSTS Framework** is an open-source software framework designed for creating, deploying, and orchestrating complex non-player character (NPC) activity within training, exercises, simulations, and educational environments.

**Watch GHOSTS in action**:  
Check out this quick three-minute introductory video on YouTube:

<iframe width="1000" height="563" src="https://www.youtube.com/embed/EkwK-cqwjjA" title="GHOSTS Intro on YouTube" frameborder="0" allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share" allowfullscreen></iframe>

At its core, GHOSTS consists of a **client** and an **API server**. The client runs on simulated user devices (e.g., workstations), while the API server is managed centrally. Clients simulate user activities based on **timelines** and **NPCs**:

- **Timelines**: Preconfigured scripts that define user behavior (tasks, operations, and durations).
- **NPCs**: Autonomous actors that simulate human-like interactions, working independently or in groups, and can be reprogrammed via the API.

## :material-file-document: Documentation

Visit the official [GHOSTS documentation](https://cmu-sei.github.io/GHOSTS/) for detailed information on setup, usage, and configuration. If you need further help, feel free to ask questions in our [community discussions](https://github.com/cmu-sei/GHOSTS/discussions).

## :material-bookmark: Glossary

- **NPCs** (Non-Player Characters): Simulated users with personalities, jobs, and behaviors, driven by GHOSTS' Animator. NPCs perform activities such as browsing, document creation, or sending emails.
- **Timeline**: A sequence of tasks defining actions a machine will perform (e.g., start time, duration, operation type).
- **Machine**: Any device running the GHOSTS client, assigned to carry out activities according to its timeline.
- **Machine Group**: A collection of machines. Timelines assigned to a group apply to all machines within it.

## :material-cog: Cyber Ranges and Crucible

GHOSTS is commonly deployed in **virtualized networks** (cyber ranges) used for training, exercises, and simulations. For more on building high-fidelity, realistic cyber ranges, check out CERT's report [_Foundation of Cyber Ranges_](https://resources.sei.cmu.edu/library/asset-view.cfm?assetid=734198).

???+ tip "Run GHOSTS on the Crucible Framework"
    ![Crucible Logo](assets/img/crucible-icon-c-alpha.svg)
    GHOSTS runs efficiently on the SEI's **Crucible Framework**, a modular system for creating and managing virtual environments for training and exercises. Find more on [Crucible's source code](https://github.com/cmu-sei/crucible) and [Crucible Docs](https://cmu-sei.github.io/crucible/).

## :material-lightbulb: Philosophy

The GHOSTS framework originated from SEI's desire to create **realistic NPCs** for cyber warfare training. Our initial technical report, [_GHOSTS in the Machine: A Framework for Cyber-Warfare Exercise NPC Simulation_](https://resources.sei.cmu.edu/library/asset-view.cfm?assetid=534316), outlines how GHOSTS replicates user behavior as observed in real-life scenarios.

Since its inception, GHOSTS has expanded to include features like:

- **Content serving** for realistic environments
- Detailed NPC creation with customizable behaviors
- **Machine learning** engines for decision-making

These features, initially separate, have now been fully integrated into the GHOSTS framework.

## :material-bug: Reporting Bugs

Found a bug? Please report it through the [GHOSTS issue tracker](https://github.com/cmu-sei/ghosts/issues). Include detailed steps for reproducing the issue and any relevant error messages.

## :material-new-box: Requesting Features

Have a feature request? Submit new ideas through the [GHOSTS issue tracker](https://github.com/cmu-sei/ghosts/issues) with a description of how it could benefit the community.

## :material-license: License

[DISTRIBUTION STATEMENT A] This material has been approved for public release and unlimited distribution.  
Copyright 2018 Carnegie Mellon University. See the [LICENSE.md](https://github.com/cmu-sei/GHOSTS/blob/master/LICENSE.md) for more details.

---

[^1]: This framework is built on earlier work from SEI’s technical report [_R-EACTR: A Framework for Designing Realistic Cyber Warfare Exercises_](https://resources.sei.cmu.edu/library/asset-view.cfm?assetid=505224), which outlines the principles behind realistic cyberwarfare simulations.
[^2]: A [GHOSTS video presentation](https://resources.sei.cmu.edu/library/asset-view.cfm?assetid=735300) from FloCon 2021 provides a general overview of the framework.
