# Animation Jobs

So, now we have Animator-generated NPCs, and they have profile information and preferences.

Animator has a job system that might enables us to push our simulation further:

- What _motivates_ an agent?
- What does an agent _know_ and how did they _learn_ that? How does their knowledge grow over time?
- What _relationships_ does an agent have off-network? How might this influence what they do on the computer?
- What does an agent _believe_? How did they come to that belief?

Jobs operate on a "per cycle" or "step" basis. For each cycle, the job processes a list of agents, and the actions or determinations programmed for each.

## Decision-Making Framework

We can use any combination of the following to drive agent decision-making:

### Motivation

We implement the [Reiss Motivational Profile](https://www.reissmotivationprofile.com/) (RMP) - which is a mathematical framework for reasoning about agent comparative motivations - agent A is twice as motivated by X than agent B - that is baselined every few years.

### Relationships

Agents build relationships with other agents in the cohort. These get better or worse over time.

How this works is that each agent has the potential to interact with _n_ other agents (they can also potentially transfer knowledge as a result). The more an agent knows about a particular subject, maybe the more likely they are to transfer information to another agent.

### Knowledge

Agents build knowledge across an array of subjects that may alter their preferences. Within Animator, there are two main ways to learn:

- Independently through study or by utilizing resources such as books or videos
- Through relationships with others at the coffee counter, through mentorship, or group-based learning (a classroom or team for example). Here NPCs learn via interactions with other agents, and the system tracks what was learned and from whom.

### Belief

What an agent believes can directly influence their behavior. Beliefs shape understanding of the world and guide decision-making and problem-solving. Agents come to belief utilizing Bayes Theorem, which is a mathematical framework for reasoning about probability of evidence.

---

So what does this all mean? Here is an example where an agent shares bits of information on social media:

Some tweets contain no insight about the agent. Some disclose some bit of information:

- Agent knows X fact
- Agent interacted with Y agent
- Agent decided to disclose some personal detail Z

Other agents — and adversaries — can see and infer from this information!

## Animator Jobs

There are several jobs currently configurable within the Animator system.

### Social Graph

This job is responsible for creating and maintaining the social graph of the agents. It is responsible for creating the initial graph, and then updating it as agents interact with each other, and learn different knowledge types.

### Social Belief

This job is responsible for creating and maintaining the bayesian social belief of the agents. It is responsible for creating the initial belief, and then updating it as agents interact with each other, and learn different knowledge types.

???+ danger "The following are still early beta"
Here be dragons

The following run with Animator's use of faker to generate content, but really begin to shine when hooked to a Large Language Model (LLM), either hosted as a cloud service or locally.

### Social Sharing

This job has agents create and post content on social media. It is responsible for creating the initial share based on the agent and their history. After creating a reasonable thing that agent would say on social media, Animator sends an activity to the GHOSTS API proper to send down to the agent to execute.

### Chat

This job has agents chat with each other on an OSS chat platform. It is responsible for creating the initial chat based on the agent and their history. After creating a reasonable thing that agent would say on social media, Animator sends an activity to the GHOSTS API proper to send down to the agent to execute.

1. Ensure ollama is running a chat model locally on localhost:11434
2. Setup a mattermost chat server: `docker run --name mattermost-preview -d --publish 8065:8065 mattermost/mattermost-preview`
3. Login to that server with the values in ./config/chat.json and create a team.
4. From the system console, make that team joinable and allow agents to join the server without invites.

### Full Autonomy

This has agents get their next instruction directly from an LLM, based on who they are and their history. While GHOSTS can execute many of these activities, some activty generated will be beyond the scope of this project, but its inclusion can provide rich histories from which to generate future activities. With a powerful LLM, this generates some exceptionally real activities, but can also be hard to control for the training and exercise audience.

Initial work in LLM-driven autonomous GHOSTS agents was documented in our technical report titled :material-file-document:[_Simulating Realistic Human Activity Using Large Language Model Directives_](https://insights.sei.cmu.edu/library/simulating-realistic-human-activity-using-large-language-model-directives/){:target="\_blank"}.

## Getting Started With LLM-Driven GHOSTS NPCs

The following is a quick start guide to getting LLM-driven GHOSTS NPCs up and running. This guide assumes you have already installed the GHOSTS API and Animator, and have a working LLM. If you do not have an LLM, you might consider [Ollama](https://ollama.ai) — which is very easy to setup and run on Apple Silicon (and where most reasonable models run very fast).

The process to stand up and use Ollama is:

- Download and install Ollama. Get familiar with creating your own custom models.
- Create a model for the job you want to run. These are stored in [content-models within the Animator project](https://github.com/cmu-sei/GHOSTS-ANIMATOR/tree/master/content-models).
- Run the commands to create the applicable model (chat for example):

```
cd chat
ollama create chat
ollama run chat
```

- You can test the model right in the terminal by interrogating it with quesitons that an NPC might generate.
- But also note that Ollama automatically creates an API enpoint for the LLM at http://localhost:11434. This is the endpoint Animator will call for content.
- Ensure your content settings for the applicable job reference your newly running model:

```json
    "ContentEngine": {
        "Source": "ollama",
        "Host": "http://localhost:11434",
        "Model": "chat"
    }
```

- You can run multiple models at the same time, but this may be a performance issue. You can also run multiple models on different machines, and point the Animator content settings to the applicable machine.
