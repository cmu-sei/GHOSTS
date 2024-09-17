# **GHOSTS Shadows**

Shadows provides access to a locally-hosted LLM for various GHOSTS agent purposes. It offers multiple interfaces:

- **A REST API**: For GHOSTS agents.
- **A UI web interface**: For testing and demo purposes.

## **Default API Endpoints**

- **Activity**: Answers the question of "what should an NPC do next?"
- **Chat**: Provides content for an NPC to chat with a player or other NPC.
- **Excel Content**: Provides content for documents related to spreadsheets.
- **Image Content**: Provides content for documents related to images.
- **Lessons**: Provides content related to educational materials or lessons.
- **Social**: Provides content for an NPC to post on social media systems such as GHOSTS Socializer.
- **Web Content**: Provides content for documents related to web pages.

We anticipate that there will be many more endpoints in the future.

---

## **Running Shadows with Docker**

To run Shadows with Ollama in Docker, follow these steps:

### **1. Set Up and Run Ollama in Docker**

1. **Pull the Ollama Docker Image**:

   ```bash
   docker pull ollama/ollama:latest
   ```

2. **Run Ollama Container**: Start Ollama in a Docker container and bind it to port 11434.

   ```bash
   docker run -d --name ollama \
     -p 11434:11434 \
     ollama/ollama:latest \
     ollama serve --port 11434
   ```

   - **-p 11434:11434**: Maps port 11434 on your host to port 11434 in the container (Ollama's API port).

### **2. Run Shadows in Docker**

1. **Export the Environment Variable**: Define the `GHOSTS_OLLAMA_URL` environment variable to point Shadows to the Ollama container.

   ```bash
   export GHOSTS_OLLAMA_URL=http://localhost:11434
   ```

2. **Run Shadows Container**: Start Shadows in Docker and connect it to the running Ollama instance.

   ```bash
   docker run -d --name shadows \
     -p 5900:5900 \
     -p 7860:7860 \
     -e GHOSTS_OLLAMA_URL=http://localhost:11434 \
     dustinupdyke/ghosts-shadows
   ```

   - **-p 5900:5900**: Maps port 5900 on your host to port 5900 in the container (Shadows API).
   - **-p 7860:7860**: Maps port 7860 on your host to port 7860 in the container (Shadows UI).
   - **-e GHOSTS_OLLAMA_URL=http://localhost:11434**: Passes the Ollama URL to Shadows.

3. **Access Shadows**:
   - **API**: Available at `http://localhost:5900`.
   - **UI**: Available at `http://localhost:7860` for testing and demos.

### **Additional Notes**

- **Network Configuration**: Ensure that the Docker containers for Ollama and Shadows are on the same network. By default, Docker containers on the same host can communicate using `localhost`, but you can create a Docker network if needed.

  ```bash
  docker network create ghosts-network
  docker run -d --name ollama --network ghosts-network -p 11434:11434 ollama/ollama:latest ollama serve --port 11434
  docker run -d --name shadows --network ghosts-network -p 5900:5900 -p 7860:7860 -e GHOSTS_OLLAMA_URL=http://ollama:11434 dustinupdyke/ghosts-shadows
  ```

  In this setup:
  - Replace `http://localhost:11434` with `http://ollama:11434` to refer to the Ollama container by name within the Docker network.

- **Troubleshooting**: If you face issues, check the logs of each container:

  ```bash
  docker logs ollama
  docker logs shadows
  ```

- **Port Conflicts**: Ensure that ports 11434, 5900, and 7860 are not in use by other applications.

---

## **Using Docker Compose**

Docker Compose simplifies managing multiple Docker containers. Here’s how to use Docker Compose to run both Ollama and Shadows:

### **1. Create a Docker Compose File**

Create a file named `docker-compose.yml` in your project directory with the following content:

```yaml
version: '3.8'

services:
  ollama:
    image: ollama/ollama:latest
    container_name: ollama
    ports:
      - "11434:11434"
    command: ollama serve --port 11434
    networks:
      - ghosts-network

  shadows:
    image: dustinupdyke/ghosts-shadows
    container_name: shadows
    ports:
      - "5900:5900"
      - "7860:7860"
    environment:
      - GHOSTS_OLLAMA_URL=http://ollama:11434
    networks:
      - ghosts-network
    depends_on:
      - ollama

networks:
  ghosts-network:
    driver: bridge
```

### **2. Explanation of the Compose File**

- **version**: Specifies the version of the Docker Compose file format.
- **services**: Defines the different containers.
  - **ollama**:
    - **image**: Docker image for Ollama.
    - **container_name**: Name for the Ollama container.
    - **ports**: Maps port 11434.
    - **command**: Command to start Ollama.
    - **networks**: Connects to the specified network.
  - **shadows**:
    - **image**: Docker image for Shadows.
    - **container_name**: Name for the Shadows container.
    - **ports**: Maps ports 5900 and 7860.
    - **environment**: Sets environment variable for Ollama URL.
    - **networks**: Connects to the specified network.
    - **depends_on**: Ensures Ollama starts before Shadows.

- **networks**: Defines a custom network for communication.

### **3. Start the Services**

In your project directory, run:

```bash
docker-compose up -d
```

- **-d**: Runs containers in detached mode.

### **4. Access the Services**

- **Ollama**: Accessible at `http://localhost:11434`.
- **Shadows**: Accessible at `http://localhost:5900` (API) and `http://localhost:7860` (UI).

### **5. Manage the Containers**

- **Stop the services**:

  ```bash
  docker-compose down
  ```

- **View logs**:

  ```bash
  docker-compose logs
  ```

- **Rebuild the services**:

  ```bash
  docker-compose up -d --build
  ```

### **6. Troubleshooting**

- **Check Container Status**:

  ```bash
  docker-compose ps
  ```

- **Inspect Logs**:

  ```bash
  docker-compose logs ollama
  docker-compose logs shadows
  ```

---

## **Running Shadows on Bare Metal**

If you prefer to run Shadows on bare metal, follow these steps:

### **1. Get Ollama Up and Running**

In separate terminal windows, execute the following commands:

1. **Create and Run Models**:

   ```bash
   cd content-models/activity
   ollama create activity

   cd ../chat
   ollama create chat

   cd ../excel_content
   ollama create excel_content

   cd ../img_content
   ollama create img_content

   cd ../lessons
   ollama create lessons

   cd ../social
   ollama create social

   cd ../web_content
   ollama create web_content
   ```

2. **Run the API and UI Servers**:

   ```bash
   python api.py
   python ui.py
   ```

### **2. Run Multiple Models**

Eventually, Ollama will serve multiple models concurrently. Use the following loop to set up and start models:

```bash
cd content-models/activity
ollama create activity

cd ../chat
ollama create chat

cd ../excel_content
ollama create excel_content

cd ../img_content
ollama create img_content

cd ../lessons
ollama create lessons

cd ../social
ollama create social

cd ../web_content
ollama create web_content

ollama serve
```

### **3. Expose Ollama Beyond Localhost**

If you want Ollama to be available beyond localhost, use:

```bash
OLLAMA_HOST=0.0.0.0:11434 ollama serve
```

Now Ollama is running all the models concurrently. The API server provides access to each model.
