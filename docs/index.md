# The GHOSTS NPC Framework

Developed by Carnegie Mellon University's Software Engineering Institute (SEI), The GHOSTS Framework is an open-source (OSS) software framework that enables creating, deploying, and orchestrating complex non-player character (NPC) activity within training, exercise, simulation, and educational environments. Within GHOSTS there are several systems and applications, all of which are modular and extendable to fit a wide array of use cases and needs.

GHOSTS is typically run on machines within a virtualized network, often referred to as "the range". This network can be as simple or as complex as required for training and exercise purposes.

???+ tip "Run GHOSTS on the Crucible Framework"
    ![Crucible Logo](assets/img/crucible-icon-c-alpha.svg)
    GHOSTS runs very well within the SEI's Crucible Framework ([Source Code](https://github.com/cmu-sei/crucible) and [Docs](https://cmu-sei.github.io/crucible/)) - which we use extensively for training and exercise here at [Carnegie Mellon University](https://www.cmu.edu) (CMU)'s [Software Engineering Institute](https://sei.cmu.edu/), and particularly, [CERT](https://cert.org)

## Documentation

You can find documentation on The GHOSTS Framework and all of its components [here](https://cmu-sei.github.io/ghosts/).

## GHOSTS Core

???+ info "GHOSTS Source Code"
    The [GHOSTS Source Code Repository](https://github.com/cmu-sei/GHOSTS) is hosted on GitHub

### Core Client

GHOSTS clients simulate what anyone might do at a computer given their particular role or membership within some team. It creates documents, browses websites, downloads files, and uses all sorts of popular applications on many versions of Windows and Linux machines. Whether you're a friendly administrator or a powerful cyber adversary, GHOSTS can replicate your expected behavior.

![Types of NPCs](assets/img/npc-types.png)

GHOSTS has many use cases in cyber training and exercises, most notably for bringing non-player characters (NPCs) to life, but GHOSTS can be used for many other purposes where realistic activity on a computer or network is needed as well: Testing, or generating network or activity datasets, for example.

???+ warning "Do I need the API?"
    Although clients are fully functional without the API server, the latter enables logging, reports, and remote administration capabilities. Without the API, changes to clients must be managed in some other manner.

### Core API Server

The GHOSTS API enables the control and orchestration of non-player characters (NPCs) within a deployment. It supports logging, reporting, and managing individual, groups of, or entire deployments of client installs.

## ANIMATOR

At its core, Animator is a realistic user details generator. Its primary function is to create NPC identities and accompanying verbose portfolios of personal information. Each generated NPC has over 25 categories of details associated with them, and over a hundred pieces of metadata defining who they are. Each piece of information is generated using sourced datasets in an attempt to distribute characteristics realistically.

???+ info "GHOSTS ANIMATOR Source Code"
    The [GHOSTS ANIMATOR Source Code Repository](https://github.com/cmu-sei/GHOSTS-ANIMATOR) is hosted on GitHub

## SPECTRE

SPECTRE is our attempt to reduce that agent browsing patterns appear as "computer random". This module modifies user web browser timelines so that they more closely match that agent's preferences over time. We expect this simple model of ML to be used for other types of agent activity in the future.

???+ info "GHOSTS SPECTRE Source Code"
    The [GHOSTS SPECTRE Source Code Repository](https://github.com/cmu-sei/GHOSTS-SPECTRE) is hosted on GitHub

## Content Servers

Many ranges are air-gapped, which means they have no access to the wider internet. In these cases, recreating a reasonable facsimile of the internet is key to the training experience. While there are many systems that do this well, we often want to augment the scenario with a wider array of URL traffic, or we want to introduce more of certain kinds of content going across the wire. PANDORA was created to address these concerns. Soon afterwards, we added a social server as well.

???+ info "GHOSTS PANDORA Source Code"
    The [GHOSTS PANDORA Source Code Repository](https://github.com/cmu-sei/GHOSTS/tree/master/src/ghosts.pandora) is hosted on GitHub

### PANDORA

PANDORA is a python-based web server that responds to all sorts of content requests on-the-fly, and supports a very broad array of URLs. Basically, any request made to a PANDORA server should return a response that corresponds to the request.

### PANDORA Social

In the spirit of the original PANDORA, this server also responds to a very broad array of URLs but enables clients to POST/PUT/DELETE to it as well.

## Reporting Bugs

Found a bug? Please report all bugs - including bugs for the individual applications - in the [cmu-sei/ghosts issue tracker](https://github.com/cmu-sei/ghosts/issues). Include as much detail as possible including steps to reproduce, the specific app involved, and any error messages you may have received.

## Requesting Features

Have a good idea for a new feature? Submit all new feature requests through the [cmu-sei/ghosts issue tracker](https://github.com/cmu-sei/ghosts/issues). Include the reasons why you're requesting the new feature and how it might benefit other users.

## License

[DISTRIBUTION STATEMENT A] This material has been approved for public release and unlimited distribution.

Copyright 2018 Carnegie Mellon University. See the [LICENSE.md](https://github.com/cmu-sei/GHOSTS/blob/master/LICENSE.md) file for details.
