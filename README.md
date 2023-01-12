# ![GHOSTS Logo](https://github.com/cmu-sei/GHOSTS/blob/master/assets/ghosts.png) GHOSTS NPC AUTOMATION

GHOSTS simulates what anyone might do at a computer all day long. It creates documents, browses websites, and downloads files. It uses all sorts of popular applications on many versions of Windows and Linux machines. Whether you're a friendly administrator or a powerful cyber-adversary, GHOSTS can replicate your expected behaviors.

GHOSTS has many use-cases in cyber training and exercises, most notably for bringing non-player characters (NPCs) to life, but GHOSTS can be used for many other purposes where realistic activity on a computer is needed as well.



[Version 6.1](https://github.com/cmu-sei/GHOSTS/wiki/v6.1.0-RELEASE-Notes) is now available, and adds significant improvements and new features. [The quick start is available here](https://github.com/cmu-sei/GHOSTS/wiki/Installation-from-distribution-binaries). Be sure to check out other parts of the GHOSTS framework, [ANIMATOR](https://github.com/cmu-sei/GHOSTS-ANIMATOR) and [SPECTRE](https://github.com/cmu-sei/GHOSTS-SPECTRE).

GHOSTS is a framework for highly-complex, realistic non-player character (NPC) orchestration. It essentially realistically mimics the behavior of the different types of people you might encounter on  typical office or enterprise networks. The system makes it possible for cybersecurity experts to test their skills and realistically train to defend real networks with real NPC players operating on those networks doing the things we might expect them to do: Create documents, access systems, browse the web, click, run commands, and so on.

As a result of the system checks required for NPCs to be situationally aware, GHOSTS also does health reporting for all configured clients on a given instance.

## Key Links

* [Quick start: Installation from distribution binaries](https://github.com/cmu-sei/GHOSTS/wiki/Installation-from-distribution-binaries)

* [Detailed installation and configuration information](https://github.com/cmu-sei/GHOSTS/wiki)

* [Don't hesitate to submit issues and feature requests](https://github.com/cmu-sei/GHOSTS/issues)

## Platform Components

### Ghosts Clients (Windows & Linux)

GHOSTS clients simulate users on a machine doing "user-like" things. They [can be configured](https://github.com/cmu-sei/GHOSTS/wiki/Configuring-the-Windows-Client) to perform actions including:

* Browse the web
* Create and edit office documents
* Send and respond to email
* Run terminal commands
* Etc.

### Ghosts API Server

The API server is a RESTful web service that provides a way for clients to interact with the GHOSTS system and its clients. It can:

* Manage clients, add/remove them from groups, etc.
* Get/manage information from clients with regards to their activity, current activities, etc.
* Orchestrate new activities for particular clients to perform

---

[DISTRIBUTION STATEMENT A] This material has been approved for public release and unlimited distribution.

Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.
