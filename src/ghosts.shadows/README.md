# GHOSTS SHADOWS

**(Shadows is depreciated. See [RangerAI](https://github.com/cmu-sei/rangerai) for all new GHOSTS AI efforts going forward)**.

---

Shadows provides access to a locally-hosted llm for the purposes of GHOSTS agents.

It provides multiple interfaces:

- A REST API for the purposes of ghosts agents
- A UI web interface for testing and demo purposes

The API endpoints are:

- **Activities**: Answers the question of "what should an NPC do next?"
- **Chat**: Provides content for an NPC to chat with a player or other NPC
- **Content**: Provides a richer array of content for the docuements created by NPCs in a range
- **Social**: Provides content for an NPC to post on a social media system such as GHOSTS Socializer

We suspect there will be many more in the future.

## Running via Docker

Typically, the easiest way to manage deployment is to pass the env var "GHOSTS_OLLAMA_URL" into the container.

    EXPORT GHOSTS_OLLAMA_URL=http://localhost:11434

(where 5900 is the api port and 7860 is the ui port)
`docker run -d --name shadows -p 5900:5900 -p 7860:7860 dustinupdyke/ghosts-shadows`

## Running on bare metal

Standing up the Shadows stack is currently (you'll need three terminal windows. We'll clean this up eventually):

Get Ollama up and running:

```bash
cd content-models/content
ollama create content
ollama run content
```
Now run the two servers (in separate terminals):

```bash
python api.py
python ui.py
```

Eventually ollama will serve multiple models all the time.

So, this loop for ["content", "social", "chat", "activities"] will be:

```bash
cd content-models/activities
ollama create activities

cd content-models/chat
ollama create chat

cd content-models/social
ollama create social

ollama serve
```

If you want this to be available beyond localhost, you'd need to run:

    OLLAMA_HOST=0.0.0.0:11434 ollama serve

Now ollama is running all four models concurrently. The API server provides access into each.

## Documentation

- [GHOSTS Documentation](https://cmu-sei.github.io/GHOSTS/)
- [Don't hesitate to submit issues and feature requests](https://github.com/cmu-sei/GHOSTS/issues)

## License

[DISTRIBUTION STATEMENT A] This material has been approved for public release and unlimited distribution. Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.