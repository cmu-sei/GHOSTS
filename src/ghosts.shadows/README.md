# GHOSTS SHADOWS

Shadows provides access to a locally-hosted large language model (LLM) for GHOSTS agents, featuring multiple interfaces:

- **REST API**: Designed for GHOSTS agents.
- **Web UI**: Intended for testing and demonstration purposes.

## API Endpoints

- **Activities**: Answers the question, "What should an NPC do next?"
- **Chat**: Supplies content for NPC interactions with players or other NPCs.
- **Content**: Delivers a variety of content for documents created by NPCs.
- **Social**: Provides content for NPCs to post on social media platforms like GHOSTS Socializer.
- **Lessons**: Offers educational content for NPCs to utilize in their interactions.

We expect to expand the features in the future. Swagger documentation for the API is available at `/docs`.

## Running via Docker

The easiest way to manage deployment is to set the environment variable `GHOSTS_OLLAMA_URL` in the container:

```bash
export GHOSTS_OLLAMA_URL=http://localhost:11434
```

*(Note: 5900 is the API port, and 7860 is the UI port.)*

```bash
docker run -d --name shadows -p 5900:5900 -p 7860:7860 dustinupdyke/ghosts-shadows
```

## Running on Bare Metal

Setting up the Shadows stack requires three terminal windows. We plan to streamline this process in the future.

1. **Initialize Ollama**:

   ```bash
   cd content-models/content
   ollama create content
   ollama run content
   ```

2. **Run the servers** in separate terminals:

   ```bash
   python api.py
   python ui.py
   ```

3. **Set up additional models**. You can choose between two methods:

   - Use the provided script: [Model script](src/ghosts.shadows/content-models/create-models.py). A detailed README is available at [README.md](src/ghosts.shadows/content-models/readme.md).
   - Alternatively, execute the following commands for each model:

   ```bash
   cd content-models/activity
   ollama create activity

   cd content-models/chat
   ollama create chat

   cd content-models/social
   ollama create social

   cd content-models/lessons
   ollama create lessons

   cd content-models/web_content
   ollama create web_content

   cd content-models/img_content
   ollama create img_content

   cd content-models/excel_content
   ollama create excel_content

   ollama serve
   ```

To make the service available beyond `localhost`, run:

```bash
OLLAMA_HOST=0.0.0.0:11434 ollama serve
```

This will launch all models concurrently, allowing the API server to provide access to each.

## Documentation

- [GHOSTS Documentation](https://cmu-sei.github.io/GHOSTS/)
- [Submit issues and feature requests](https://github.com/cmu-sei/GHOSTS/issues)

## License

[DISTRIBUTION STATEMENT A] This material has been approved for public release and unlimited distribution. Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md for terms.
