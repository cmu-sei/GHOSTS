# Quick Start

???+ info "No Compilers Necessary"
    This section details the installation and configuration of GHOSTS from precompiled binaries for both the server and the client.

## GHOSTS API Server

Easy installation requires 🐳 &nbsp; [Docker](https://docs.docker.com/install/) and [Docker Compose](https://docs.docker.com/compose/install/). Using [this docker-compose.yml file](https://github.com/cmu-sei/GHOSTS/blob/master/src/Ghosts.Api/docker-compose.yml), the required containers will be downloaded and configured automatically. The command to stand up the GHOSTS containers is simply (in the directory where you placed the docker-compose file:

$ `docker-compose up -d`

Once the command completes, if you open [http://localhost:5000/api/home](http://localhost:5000/api/home) in your browser, you should see the initial API page outlining the version of the install, and a few test machine entries. If this page renders, your API is up, running, and available.

[You will still need to set up Grafana](https://github.com/cmu-sei/GHOSTS/wiki/Configuring-Grafana-Reporting). Beware that you must often `chown` the host location of the container as listed in the docker-compose file or the container will just continually restart in error due to insufficient permissions.

For those installing on bare metal, a Postgresql server is required for the database backend. [Then you'll need version 6.0 of the dotnet framework runtime](https://dotnet.microsoft.com/download). Then install [the latest zip of the GHOSTS API](https://cmu.box.com/s/gz62rltw8ajkhomt3c2kvbtpw56yikd1).

The API generally has good defaults to get you up and running quickly, but there are some considerations in the `appconfig.json` file:

```json
    "ClientSettings": {
        "OfflineAfterMinutes": 30, ...
```

"MatchMachinesBy": null,
Can be fqdn|host|resolvedhost|null - null tells the API to match incoming requests with machine records by the machine name. For installations where multiple domains are reporting into the same API, you probably want to use "fqdn" in order to avoid machines being duplicated.

"QueueSyncDelayInSeconds": 10,
"NotificationsQueueSyncDelayInSeconds": 10,
This is how often the synch job runs. Incoming machine requests are not real-time in order to best bundle like records together.

## GHOSTS Clients

For any of the clients utilizing the browser, an automation driver is necessary to be included in the same folder as the GHOSTS binary. For Firefox, [download the appropriate 🦎&nbsp; Geckodriver for your version of the browser here](https://github.com/mozilla/geckodriver/releases). For Chrome, [download the appropriate Chromedriver for your version of the browser here](https://chromedriver.chromium.org/downloads).

### Windows Client

* Your client Windows machine will need to have (at least) the [Microsoft DotNet 4.6.1 runtime installed](https://dotnet.microsoft.com/download/dotnet-framework/net47). Again, note that you only need the runtime, not the full SDK.

* [Download 6.2.0 Win x64 (for use with Office x64)](https://cmu.box.com/s/3g8x4sowss1u4ngm4vy68obdsq4ir354).

* [Download 6.2.0 Win x32 (for use with Office x32)](https://cmu.box.com/s/ip8xuntjbsalarb4kblswyne1hhcfo4n).

    Unzip to your client machine in a directory such as `c:\exercise\ghosts`. You will need to adjust configuration in `config/application.json` in order for your client to talk to your already installed API server above.

### Linux Client

* Your client Linux machine will need to have the latest [Microsoft dotnetcore runtime](https://dotnet.microsoft.com/download) installed (Note that for the GHOSTS client, there are versions for dotnetcoreapp3.1 - this will eventually go away — and dotnet6.0, which is LTS and should stick around for a while). Again, note that you only need the runtime installed, not the full SDK.

* [Download the current Linux client](https://cmu.box.com/s/onpe9b8dzw3ysk4h577wferfyhsrus1w) zip file. Unzip to a folder such as ~/ghosts for the user that you want GHOSTS to run as.

    Note that on Linux machines running the client as root and utilizing web browsing may result in failures due to Gecko/Chromedriver display issues. The GHOSTS client should typically be run as a user, and not as root.
