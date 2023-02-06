# Quick Start

???+ info "No Compilers Necessary"
    This section details the installation and configuration of GHOSTS from precompiled binaries for both the server and the client.

If you are just checking out the project for the first time and want to see NPCs performing activities, skip to the [client section below](#ghosts-clients). Otherwise, it is easier to install the API first and then a client that will connect to that installed API instance.

## :material-api: GHOSTS API Server

Easy installation requires:

1. Install 🐳 [Docker](https://docs.docker.com/install/){:target="_blank"}
2. Install [Docker Compose](https://docs.docker.com/compose/install/){:target="_blank"}
3. We'll use [this docker-compose.yml file](https://raw.githubusercontent.com/cmu-sei/GHOSTS/master/src/Ghosts.Api/docker-compose.yml){:target="_blank"} in the following command block which will download the required containers will automatically. The commands to stand up the GHOSTS API containers is:

    ```cmd
    mkdir ghosts-api
    cd ghosts-api
    curl https://raw.githubusercontent.com/cmu-sei/GHOSTS/master/src/Ghosts.Api/docker-compose.yml -o docker-compose.yml
    docker-compose up -d
    ```

4. Once the command completes, if you open [http://localhost:5000/api/home](http://localhost:5000/api/home){:target="_blank"} in your browser, you should see the initial API page outlining the version of the install, and a few test machine entries. If this page renders, your API is up, running, and available. If the page does not render, follow the advice in the [API troubleshooting section](core/api.md#troubleshooting).

[You will still need to set up Grafana](core/api.md#configuring-grafana). Beware that you must often `chown g_data`, which is the host location for the Graphana container as listed in the docker-compose file. Otherwise, the Grafana container will just continually restart in error due to insufficient permissions (detailed in [API troubleshooting](core/api.md#troubleshooting)).

## :material-account: GHOSTS Clients

For any of the clients utilizing the browser, an automation driver is necessary to be included in the same folder as the GHOSTS binary. For Firefox, [download the appropriate 🦎 Geckodriver for your version of the browser here](https://github.com/mozilla/geckodriver/releases){:target="_blank"}. For Chrome, [download the appropriate :material-google-chrome: Chromedriver for your version of the browser here](https://chromedriver.chromium.org/downloads){:target="_blank"}.

Note there are [additional configuration steps for :material-microsoft-outlook: Outlook email automation](core/client.md).

The GHOSTS client should typically be run as a specific user, and not as an administrator or root account.

### :material-microsoft-windows: Windows Client

1. Your client machine will need to have (at least) the [Microsoft DotNet 4.6.1 runtime installed](https://dotnet.microsoft.com/download/dotnet-framework/net47){:target="_blank"}. You do not need the full SDK.
2. [Download the latest Windows client](https://github.com/cmu-sei/GHOSTS/releases/latest){:target="_blank"}.
3. Unzip to your client machine in a directory such as `c:\exercise\ghosts`. You will need to adjust the configuration in `config/application.json` for your client to talk to the already installed API server from above.

### :material-linux: Linux Client

1. Your client Linux machine will need to have the latest [Microsoft dotnetcore runtime](https://dotnet.microsoft.com/download){:target="_blank"} installed (Note that for the GHOSTS client, there are versions for dotnetcoreapp3.1 - this will eventually go away — and dotnet6.0, which is LTS and should stick around for a while). Again, note that you only need the runtime installed, not the full SDK.
2. [Download the latest Linux client](https://github.com/cmu-sei/GHOSTS/releases/latest){:target="_blank"} zip file. Unzip to a folder such as ~/ghosts for the user that you want GHOSTS to run as.

Note that on Linux machines running the client as root and utilizing web browsing may result in failures due to Gecko/Chromedriver display issues.
