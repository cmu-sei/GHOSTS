# GHOSTS Core API Overview

???+ info "GHOSTS Source Code"
    The [GHOSTS Source Code Repository](https://github.com/cmu-sei/GHOSTS) is hosted on GitHub.
    
The GHOSTS API enables the control and orchestration of non-player characters (NPCs) within a deployment. It supports logging, reporting, and managing individual, groups of, or entire deployments of client installs.

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

## Webhooks

The GHOSTS API provides webhook callbacks based on the configuration on the endpoint: `/api/webhooks`. The payload for creating a webhook is in the format:

```json
{
  "status": 0,
  "description": "some description",
  "postbackUrl": "http://localhost/endpoint:port",
  "postbackMethod": 0, (0 == get, 1 == post)
  "postbackFormat": "see below"
}
```

Payloads can be any format — here is a sample:

```json
{
 'machine':'[machinename]',
 'created':'[datetime.utcnow]',
 'type':'[messagetype]',
 'payload':'[messagepayload]'
}
```

On send, the payload will be converted into correct json format:

```json
{
 "machine":"some_guid",
 "created":"some_datetime",
 "type":"some_message",
 "payload":"some_payload"
}
```

If the postback method is POST, the payload will be sent as the message body. If the postback method is get, the payload will be sent as part of the querystring value ?message=`payload`.

The following events are reported via webhooks:

1. Timeline delivered (with the timeline that was delivered as payload) to machine via API (original API posting of timeline only holds timeline in wait - client still must check-in in order for that timeline to be delivered)
2. Machine requested updates ("checked in") from API
3. Machine posted results to API

## Troubleshooting

> Is the API up and running?

- Go to `/api/home` in the browser, it should return the current api version and number of machines and groups under management. If it says relationship not found, restart the API application and it should create the database automatically.
- Run `docker ps --all` and see that all containers are running normally. If one or more is not running, look at the logs for that machine via `docker logs [machine name]`.
