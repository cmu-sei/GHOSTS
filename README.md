Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

# Ghosts NPC Automation

GHOSTS is a framework for cyber exercise NPC automation. As a result of the system checks required in order for NPCs to be situationally aware, GHOSTS also does health reporting for all configured clients on a given instance.

Formally, GHOSTS is an acronym representing a framework for thinking about fully autonomous exercise delivery:

### (G)ENERAL HOSTS
The system handles a great deal of variable activity. E.g. an actor could be friendly, entirely neutral, or
openly hostile - the system makes provisions for the existence of all types of actors.

------------------

# PLATFORM ELEMENTS

## Ghosts.Domain
DTO objects and shared functionality that get passed back and forth between client and api. This project or dll is not installed separately.

## Ghosts.Client
.NET Console app (but built as forms app so that it is hidden)

Client is currently targeting windows only

## Ghosts.Api

.NET Core 2.0 MVC Code-First containing both the api calls for the client (and corresponding api calls the ui needs) in one. 

Uses postgres on the backend because there is not much that postgres can't do.

## Ghosts.App
The Angular C2 app. Calls the above API.

------------------

# INSTALLATION

Docker setup should be done in the following order for simplicity:

dotnet publish -c Release -o bin/publish

docker build . -t ghosts/api

docker network create -d bridge ghosts-network

docker volume create ghosts-storage

docker run —-network ghosts-network -p 127.0.0.1:5432:5432 --name ghosts-postgres -e POSTGRES_PASSWORD=<yourpassword> -v ghosts-storage:/var/lib/postgresql/data -d postgres

psql -h 127.0.0.1 -p 5432 -U postgres

psql> create user ghosts with password '<yourpassword>' ;
psql> create database ghosts owner ghosts;
psql> \c ghosts

docker run -d -p 8080:80 --network ghosts-network --name ghosts-api ghosts/api

--for grafana --

docker volume create grafana-storage
docker run —network ghosts-network -d -p 3000:3000 —-name=ghosts-grafana -v grafana-storage:/var/lib/grafana grafana/grafana

## App

* Node and nom
* npm install
* Ng serve

## Client(s)

It is recommended you verify one working client before deploying to multiple machines. You will need the base url from the API above.

ghosts.client.exe.config

One file that should NOT be copied is config.json - this generated file stores the ID of the client instance.

### HTTPS certificates

By default, connections to the C2 server are done using HTTPS. The certificate used by the server must be installed and trusted by the clients or no updates or results will be sent back.

### .NET framework

ghosts requires .NET framework v4.6.1 or higher.

### Firewall holes

Any in-game traffic on ports 80 and/or 443 must be allowed to the control server from the clients. Similar considerations may need to be made when using vtunnel, though this is only in cases where the firewall's default setup is extremely restrictive.

### Windows versions

Ghosts works on both Windows 7 and Windows 10.



# [DISTRIBUTION STATEMENT A] This material has been approved for public release and unlimited distribution.