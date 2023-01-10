# GHOSTS Core API Overview

The GHOSTS API enables the programmatic orchestration of all agents you have deployed to your range.

## Installation

1. Install 🐳 [Docker](https://docs.docker.com/install/) :material-open-in-new:
2. Install [Docker Compose](https://docs.docker.com/compose/install/) :material-open-in-new:
3. Run the following commands - we'll use [this docker-compose.yml file](https://github.com/cmu-sei/GHOSTS/blob/master/src/Ghosts.Api/docker-compose.yml)

```cmd
mkdir ghosts
cd ghosts
curl https://github.com/cmu-sei/GHOSTS/blob/master/src/Ghosts.Api/docker-compose.yml -o docker-compose.yml
docker-compose up -d
```

The required containers will be downloaded and configured automatically.

Once the last command completes, if you open [http://localhost:5000/api/home](http://localhost:5000/api/home) in your browser, you should see the initial API page outlining the version of the install, and a few test machine entries. If this page renders, your API is up, running, and available.

You will still need to set up Grafana. Beware that you must often `chown` the host location of the container as listed in the docker-compose file or the container will just continually restart in error due to insufficient permissions.

## Configuring the API

The API generally has good defaults to get you up and running quickly, but there are some considerations in the `appconfig.json` file:

```json
    "ClientSettings": {
        "OfflineAfterMinutes": 30, ...
        "MatchMachinesBy": null,
```

Can be fqdn|host|resolvedhost|null - null tells the API to match incoming requests with machine records by the machine name. For installations where multiple domains are reporting into the same API, you probably want to use "fqdn" in order to avoid machines being duplicated.


```json
"QueueSyncDelayInSeconds": 10,
"NotificationsQueueSyncDelayInSeconds": 10,
```

This is how often the synch job runs. Incoming machine requests are not real-time in order to best bundle like records together.

## Configuring Grafana

- Grafana will be running (if containerized) at :3000, and we can access it via the same url we use for the API, but on port :3000 in a web browser.
- Default login is admin/admin.
- First step is to setup a datasource named "ghosts" to the ghosts postgres database.
- Now import your choice of the [grafana json files](https://github.com/cmu-sei/GHOSTS/tree/master/configuration/grafana) in this repository. It creates the default GHOSTS dashboard.
