# GHOSTS NPC Framework

GHOSTS simulates what anyone might do at a computer, creating documents, browsing websites, and downloading files. GHOSTS drives all sorts of popular applications on many versions of Windows and Linux machines. Whether you're a friendly administrator or a powerful cyber adversary, GHOSTS can replicate your expected behavior.

GHOSTS has many use cases in cyber training and exercises, most notably for bringing non-player characters (NPCs) to life, but GHOSTS can be used for many other purposes where realistic activity on a computer is needed as well.

Version 7:

- It depreciates TCP communication between agents and the API
- Bumps all software and dependencies to the latest versions
- Adds sending users specific commands to execute (namely social media posts)

Be sure to check out other parts of the GHOSTS framework, [ANIMATOR](https://github.com/cmu-sei/GHOSTS-ANIMATOR) and [SPECTRE](https://github.com/cmu-sei/GHOSTS-SPECTRE).

There is a [short demonstration video available on YouTube](https://www.youtube.com/watch?v=EkwK-cqwjjA) (3:03).

## Key Links

- [Quick Start: Installation from distribution binaries](https://cmu-sei.github.io/GHOSTS/quickstart/)
- [GHOSTS Documentation](https://cmu-sei.github.io/GHOSTS/)
- [Don't hesitate to submit issues and feature requests](https://github.com/cmu-sei/GHOSTS/issues)

## Platform Components

### Ghosts Clients (Windows & Linux)

GHOSTS clients simulate users on a machine doing "user-like" things. They can be configured to:

- Browse the web
- Create and edit office documents
- Send and respond to email
- Run terminal commands
- Etc.

### Ghosts API Server

The API server provides a way for clients to interact with the GHOSTS system and its clients. It can:

- Manage clients, add/remove them from groups, etc.
- Get/manage information from clients regarding their previous or current activities, etc.
- Orchestrate new activities for particular clients to perform

## License

[DISTRIBUTION STATEMENT A] This material has been approved for public release and unlimited distribution.
Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.
