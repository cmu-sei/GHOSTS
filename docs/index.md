# The GHOSTS NPC Framework

Developed by Carnegie Mellon University's Software Engineering Institute (SEI), The GHOSTS Framework is an open-source (OSS) software framework that enables creating, deploying, and orchestrating complex non-player character (NPC) activity within training, exercise, simulation, and educational environments. Within GHOSTS there are several systems and applications, all of which are modular and extendable to fit a wide array of use cases and needs.

GHOSTS is typically run on machines within a virtualized network, often referred to as "the range". This network can be as simple or as complex as required for training and exercise purposes.

???+ tip "Run GHOSTS on the Crucible Framework"
    ![Crucible Logo](assets/img/crucible-icon-c-alpha.svg)
    GHOSTS runs very well within the SEI's Crucible Framework ([Source Code](https://github.com/cmu-sei/crucible) and [Docs](https://cmu-sei.github.io/crucible/)) - which we use extensively for training and exercise here at [Carnegie Mellon University](https://www.cmu.edu) (CMU)'s [Software Engineering Institute](https://sei.cmu.edu/), and particularly, within [CERT](https://cert.org).

## Documentation

We have a continually updated [GHOSTS documentation site](https://cmu-sei.github.io/ghosts/) for the framework and all of its components. Each major component's detail is accessible from the main navigation. If anything is unclear or you still have questions, please do not hesitate to start a [discussion](https://github.com/cmu-sei/GHOSTS/discussions) — our community is growing and eager to help!

## Philosophy

GHOSTS evolved in our quest to create more realistic NPCs within cyberwarfare training and exercise. In 2018, we outlined our thoughts in a whitepaper entitled [_GHOSTS in the Machine: A Framework for Cyber-Warfare Exercise NPC Simulation_](https://resources.sei.cmu.edu/library/asset-view.cfm?assetid=534316).[^1] In that report, we outline how the GHOSTS framework accomplishes the creation of simulations in which NPCs realistically represent a vast array of possible encounters and outcomes. We have continued to follow our initial path since. The litmus has always been that if we looked over someone's shoulder while they were using a computer, that's what GHOSTS should look like.

## Reporting Bugs

Found a bug? Please report all bugs - including bugs for the individual components - in the [cmu-sei/ghosts issue tracker](https://github.com/cmu-sei/ghosts/issues). Include as much detail as possible including steps to reproduce, the specific components involved, and any error messages you may have found.

## Requesting Features

Have a good idea for a new feature? Submit all new feature requests through the [cmu-sei/ghosts issue tracker](https://github.com/cmu-sei/ghosts/issues). Include the reasons why you're requesting the new feature and how it might benefit other users.

## License

[DISTRIBUTION STATEMENT A] This material has been approved for public release and unlimited distribution.

Copyright 2018 Carnegie Mellon University. See the [LICENSE.md](https://github.com/cmu-sei/GHOSTS/blob/master/LICENSE.md) file for details.

[^1]: This paper is heavily influenced by our previous paper [_R-EACTR: A Framework for Designing Realistic Cyber Warfare Exercises_](https://resources.sei.cmu.edu/library/asset-view.cfm?assetid=505224) which outlines a design framework for cyber warfare exercises. It ensures that designs of team-based exercises factor realism into all aspects of the participant experience.
