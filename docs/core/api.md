# Setting Up the GHOSTS API

_Updated on October 30, 2024_

The GHOSTS API enables centralized control and orchestration of non-player characters (NPCs) within a deployment. It provides logging, reporting, and management capabilities for individual machines, machine groups, or entire deployments.

## Architecture Overview

The GHOSTS API consists of four components, each running in its own Docker container:

- **GHOSTS API** - RESTful API for configuring and managing NPCs, machines, and timelines
- **GHOSTS UI** - Web interface for visualizing and managing your GHOSTS deployment
- **Postgres** - Database for storing all GHOSTS data (machines, NPCs, timelines, activities)
- **Grafana** - Dashboard for monitoring and analyzing NPC activities in real-time

## Installation Steps

### Step 1 — Choose Where to Host the API

Select an appropriate host for your GHOSTS API deployment:

- **Testing/Development**: Your local machine is sufficient
- **Training/Exercises**: Use a dedicated server, VM, or cloud instance
- **Production**: Consider container orchestration platforms like AWS ECS, Kubernetes, or Docker Swarm

**System Requirements:**

- Docker and Docker Compose installed
- Minimum 4GB RAM (8GB+ recommended for larger deployments)
- 20GB disk space
- Network access from client machines to API server

### Step 2 — Install Docker and Docker Compose

Install the required software on your API host:

1. **Install Docker**: Follow the [Docker installation guide](https://docs.docker.com/install/)
2. **Install Docker Compose**: Follow the [Docker Compose installation guide](https://docs.docker.com/compose/install/)

**Verify Installation:**

```bash
docker --version
docker-compose --version
```

Both commands should return version information if properly installed.

### Step 3 — Deploy the GHOSTS API

Create a directory for the GHOSTS deployment and download the Docker Compose configuration:

```bash
mkdir ghosts-api
cd ghosts-api
curl -O https://raw.githubusercontent.com/cmu-sei/GHOSTS/master/src/Ghosts.Api/docker-compose.yml
```

Start all containers:

```bash
docker-compose up -d
```

The `-d` flag runs containers in detached mode (in the background).

**Verify All Containers Are Running:**

```bash
docker ps -a
```

You should see four containers: `ghosts-api`, `ghosts-ui`, `ghosts-postgres`, and `ghosts-grafana`.

![Running Containers](../images/installing-the-api-running-containers.png)

### Step 4 — Verify the Installation

Test each component to ensure proper deployment:

**1. GHOSTS API (Port 5000)**

Open [http://localhost:5000](http://localhost:5000) in your browser. You should see the API home page displaying:

- API version information
- Several test machine entries
- Links to various API endpoints

![Success!](../images/installing-the-api-success.png)

**2. GHOSTS UI (Port 8080)**

Open [http://localhost:8080](http://localhost:8080) to access the web management interface. See the [UI documentation](ui.md) for usage details.

**3. Grafana Dashboard (Port 3000)**

Open [http://localhost:3000](http://localhost:3000) to access the monitoring dashboard. Default credentials are typically `admin/admin` (you'll be prompted to change the password on first login). See the [Grafana documentation](grafana.md) for configuration details.

## Managing the API

### Starting and Stopping

```bash
# Stop all containers
docker-compose down

# Start all containers
docker-compose up -d

# Restart a specific container
docker-compose restart ghosts-api

# View container logs
docker-compose logs -f ghosts-api
```

### Updating GHOSTS

To update to the latest version:

```bash
docker-compose down
docker-compose pull
docker-compose up -d
```

### Data Persistence

All data is stored in Docker volumes:
- `postgres_data` - Database containing machines, NPCs, timelines, and activities
- `grafana_data` - Grafana configuration and dashboards

These volumes persist across container restarts and updates.

## Troubleshooting

### API Home Page Shows Error

![API Home Page Error](../images/installing-the-api-error.png)

**Cause**: The Postgres database container is not running or not accessible.

**Solution**:

1. Check if the Postgres container is running:
   ```bash
   docker ps -a | grep postgres
   ```

2. Check Postgres logs for errors:
   ```bash
   docker logs ghosts-postgres
   ```

3. Restart the Postgres container:
   ```bash
   docker-compose restart ghosts-postgres
   ```

4. If the container repeatedly restarts, check for permission issues with the data volume.

### Social Graph Link Shows Error

![API Social Graph Page Error](../images/installing-the-api-social-error.png)

**Cause**: No social network has been created yet.

**Solution**: This is normal for new installations. Social networks are created when you generate NPCs using the Animator functionality. See the [Animator documentation](../animator/index.md) for details.

### Grafana Container Keeps Restarting

**Cause**: Insufficient permissions on the Grafana data directory.

**Solution**:

1. Check the ownership of the Grafana data directory:
   ```bash
   ls -la | grep grafana
   ```

2. Set the correct ownership (the exact user ID may vary based on your `docker-compose.yml`):
   ```bash
   sudo chown -R 472:472 grafana_data
   ```

3. Restart the Grafana container:
   ```bash
   docker-compose restart ghosts-grafana
   ```

### Cannot Connect from Client to API

**Cause**: Network connectivity or firewall issues.

**Solution**:

1. Verify the API is accessible from the client machine:
   ```bash
   curl http://YOUR-API-SERVER:5000/api
   ```

2. Check firewall rules on the API server to ensure ports 5000, 8080, and 3000 are accessible.

3. Ensure the client's `application.json` or `application.yaml` has the correct API URL:
   ```json
   {
     "ApiRootUrl": "http://YOUR-API-SERVER:5000/api"
   }
   ```

### Container Logs Show "Connection Refused" Errors

**Cause**: Containers are trying to communicate before all services are ready.

**Solution**: This is typically a timing issue during startup. Wait 30-60 seconds and check if the errors persist. If they do:

```bash
docker-compose restart
```

### Need to Reset the Database

**Warning**: This will delete all machines, NPCs, timelines, and activity data.

```bash
docker-compose down
docker volume rm ghosts-api_postgres_data
docker-compose up -d
```

## Next Steps

- Configure [client machines](client.md) to connect to your API
- Explore the [UI](ui.md) to manage machines and timelines
- Set up [Grafana dashboards](grafana.md) for monitoring
- Learn about [timeline configuration](api/timelines.md)
- Generate NPCs using [Animator](../animator/index.md)
