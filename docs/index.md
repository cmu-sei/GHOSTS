# The GHOSTS NPC Framework

Developed by Carnegie Mellon University's Software Engineering Institute (SEI), The GHOSTS Framework is an open-source (OSS) software framework that enables creating, deploying, and orchestrating complex non-player character (NPC) activity within training, exercise, simulation, and educational environments.

If you've never seen GHOSTS in action, watch this quick three-minute introductory video on YouTube:

<iframe width="1000" height="563" src="https://www.youtube.com/embed/EkwK-cqwjjA" title="GHOSTS Intro on YouTube" frameborder="0" allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share" allowfullscreen></iframe>

## :material-file-document: Documentation

This is the [GHOSTS documentation site](https://cmu-sei.github.io/GHOSTS/) for the framework and all of its components. Each major component's detail is accessible from the main navigation. If anything is unclear or you still have questions, please do not hesitate to start a [discussion](https://github.com/cmu-sei/GHOSTS/discussions){:target="_blank"} — our community is growing and eager to help!

## :material-cog: Cyber Ranges and Crucible

GHOSTS is typically run on machines within a virtualized network, often referred to as "the range". This network can be as simple or as complex as required for training, exercise, modeling, or simulation purposes.

CERT's Cyber Mission Readiness (CMR) team has a great deal of experience in building cyber ranges for training, exercise, and simulation — captured in our technical report :material-file-document:[_Foundation of Cyber Ranges_](https://resources.sei.cmu.edu/library/asset-view.cfm?assetid=734198){:target="_blank"}. The report details the design considerations and execution plan for building high-fidelity, realistic virtual cyber ranges that deliver maximum training and exercise value for cyberwarfare participants.

???+ tip "Run GHOSTS on the Crucible Framework"
    ![Crucible Logo](assets/img/crucible-icon-c-alpha.svg)
    GHOSTS runs very well within the SEI's Crucible Framework ([Source Code](https://github.com/cmu-sei/crucible){:target="_blank"} and [Docs](https://cmu-sei.github.io/crucible/){:target="_blank"}) - which we use extensively for training and exercise here at [Carnegie Mellon University](https://www.cmu.edu){:target="_blank"} (CMU)'s [Software Engineering Institute](https://sei.cmu.edu/){:target="_blank"}, and particularly, within [CERT](https://cert.org){:target="_blank"}.

    Building a sufficiently complex range for training and exercise purposes is often challenging. Crucible is a modular framework for creating, deploying, and managing virtual environments to support training, education, and exercises. Crucible is designed to be easy to use, extensible, and customizable to meet the needs of a wide variety of use cases.

## :material-lightbulb: Philosophy

GHOSTS evolved in our quest to create more realistic NPCs within cyberwarfare training and exercise. In 2018, we outlined our thoughts in a technical report entitled [_GHOSTS in the Machine: A Framework for Cyber-Warfare Exercise NPC Simulation_](https://resources.sei.cmu.edu/library/asset-view.cfm?assetid=534316){:target="_blank"}.[^1] In that report, we outline how the GHOSTS framework accomplishes the creation of simulations in which NPCs realistically represent a vast array of possible encounters and outcomes. We have continued to follow our initial path since. The litmus has always been that if we looked over someone's shoulder while they were using a computer, that is what GHOSTS should look like.[^2]

Since then the framework has expanded to include tools that [serve content in simulated environments](content/index.md), [create NPCs with details about them that we can programmatically use to drive their decision-making](animator/index.md), and [machine learning agent preference engines](spectre/index.md). If it is related to replicating human behavior in terms of computing, we are interested in how GHOSTS can contribute within that space. Although these efforts listed here began separately, they are now all part of the GHOSTS framework proper.

## :material-bug: Reporting Bugs

Found a bug? Please report all bugs - including bugs for the individual components - in the [cmu-sei/ghosts issue tracker](https://github.com/cmu-sei/ghosts/issues){:target="_blank"}. Include as much detail as possible including steps to reproduce, the specific components involved, and any error messages you may have found.

## :material-new-box: Requesting Features

Have a good idea for a new feature? Submit all new feature requests through the [cmu-sei/ghosts issue tracker](https://github.com/cmu-sei/ghosts/issues){:target="_blank"}. Include the reasons why you're requesting the new feature and how it might benefit other users.

## :material-license: License

[DISTRIBUTION STATEMENT A] This material has been approved for public release and unlimited distribution.

Copyright 2018 Carnegie Mellon University. See the [LICENSE.md](https://github.com/cmu-sei/GHOSTS/blob/master/LICENSE.md){:target="_blank"} file for details.

[^1]: This paper is influenced by our previous paper :material-file-document:[_R-EACTR: A Framework for Designing Realistic Cyber Warfare Exercises_](https://resources.sei.cmu.edu/library/asset-view.cfm?assetid=505224){:target="_blank"} which outlines a design framework for cyber warfare exercises. It ensures that designs of team-based exercises factor realism into all aspects of the participant experience. Both of these papers are natural extensions to :material-file-document:[_The CERT Approach to Cybersecurity Workforce Development_](https://resources.sei.cmu.edu/library/asset-view.cfm?assetid=9697){:target="_blank"}.

[^2]: There is also a :material-video:[GHOSTS video presentation](https://resources.sei.cmu.edu/library/asset-view.cfm?assetid=735300){:target="_blank"} from FloCon 2021 that provides a general introduction to the framework.
