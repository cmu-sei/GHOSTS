# GHOSTS UI

A UI for the [GHOSTS](https://github.com/cmu-sei/GHOSTS) API implemented as a Next.js web app.

## Overview

The GHOSTS UI is a web application that provides a user interface for the GHOSTS API. The GHOSTS UI is implemented as a Next.js web app that is served by a Node.js server. The GHOSTS UI is a single page application that uses the GHOSTS API to interact with the GHOSTS backend. It provides support for the following features:

- Machines
- Machine Groups
- TimeLines
- Npcs

For any machine or machine group, we can use the UI to deploy new timelines, or view activities. For machine groups, we can create or manage the machines in the group. For timelines, we can view the activities in the timeline, or deploy new activities. It also provides a management interface for timelines as well.

## Install

An easy way to run the GHOSTS UI is to use Docker. The following is an example we would add to the ghosts `docker-compose.yml` file that will run the GHOSTS UI and the GHOSTS API together:

```yaml
ghostsui:
    image: dustinupdyke/ghosts-ui
    container_name: ghosts-ui
    ports:
      - '8080:8080'
    networks:
      - ghosts-network
    environment:
      GHOSTS_API_URL: http://ghosts-api:5000
```

## Documentation

- [GHOSTS Documentation](https://cmu-sei.github.io/GHOSTS/)
- [Don't hesitate to submit issues and feature requests](https://github.com/cmu-sei/GHOSTS/issues)

## Thanks

Many thanks to the UI team for their hard work on this project. You know who you are — stay safe out there.

## License

[DISTRIBUTION STATEMENT A] This material has been approved for public release and unlimited distribution.
Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.
