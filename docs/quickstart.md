# **Quick Start**

### 📌 **No Compilers Necessary**

This section provides a straightforward guide to installing and configuring GHOSTS using precompiled binaries for both the server and the client. If you're exploring GHOSTS for the first time and want to see NPCs in action, jump to the [client section](#ghosts-clients). For a complete setup, it’s recommended to first install the API and then connect a client to this API instance.

## :material-api: **GHOSTS API Server**

**Easy installation involves the following steps:**

1. **Install Docker:**

   Follow the [Docker installation guide](https://docs.docker.com/install/) to get Docker up and running on your system.

2. **Install Docker Compose:**

   Refer to the [Docker Compose installation guide](https://docs.docker.com/compose/install/) for instructions on installing Docker Compose.

3. **Download and Run the GHOSTS API Containers:**

   Use the provided `docker-compose.yml` file to set up the GHOSTS API containers. Execute the following commands to download the necessary files and start the containers:

   ```bash
   mkdir ghosts-api
   cd ghosts-api
   curl -O https://raw.githubusercontent.com/cmu-sei/GHOSTS/master/src/Ghosts.Api/docker-compose.yml
   docker-compose up -d
   ```

   - `mkdir ghosts-api`: Creates a directory for the API setup.
   - `cd ghosts-api`: Navigates into the directory.
   - `curl -O https://raw.githubusercontent.com/cmu-sei/GHOSTS/master/src/Ghosts.Api/docker-compose.yml`: Downloads the Docker Compose file.
   - `docker-compose up -d`: Starts the containers in detached mode.

4. **Verify the API Installation:**

   After the command completes, open [http://localhost:5000/](http://localhost:5000/) in your browser. You should see the initial API page displaying the version and some test machine entries. If the page loads, your API is successfully running. If not, consult the [API troubleshooting section](core/api.md#troubleshooting) for help.

   **Note:** You will also need to configure Grafana. Ensure you set the correct ownership for the Grafana data directory (`g_data`) as specified in the Docker Compose file. Insufficient permissions often cause the Grafana container to restart repeatedly due to errors (see [API troubleshooting](core/api.md#troubleshooting) for details).

## :material-account: **GHOSTS Clients**

To use GHOSTS clients that require a browser, you need to include an automation driver in the same folder as the GHOSTS binary. 

- **For Firefox:** [Download the appropriate Geckodriver](https://github.com/mozilla/geckodriver/releases) for your browser version.
- **For Chrome:** [Download the appropriate Chromedriver](https://chromedriver.chromium.org/downloads) for your browser version.

Additional setup may be required for [Outlook email automation](core/client.md).

**Clients should generally be run as a specific user, not as an administrator or root account.**

### :material-microsoft-windows: **Windows Client**

1. Ensure that the [Microsoft .NET Framework 4.6.1 runtime](https://dotnet.microsoft.com/download/dotnet-framework/net47) is installed on your client machine. You do not need the full SDK.
2. [Download the latest Windows client](https://github.com/cmu-sei/GHOSTS/releases/latest).
3. Extract the contents to a directory, such as `c:\exercise\ghosts`. Update the `config/application.json` file to connect to the previously installed API server.

### :material-linux: **Linux Client**

1. Install the .NET Core runtime version 6 on your Linux machine. Only the runtime is required, not the full SDK. Follow the instructions [here](https://dotnet.microsoft.com/download) for installation.
2. [Download the latest Linux client](https://github.com/cmu-sei/GHOSTS/releases/latest) zip file. Unzip it to a directory like `~/ghosts` for the user you intend to run GHOSTS.

**Note:** Running the Linux client as root while using web browsing may cause display issues with Gecko/Chromedriver.
