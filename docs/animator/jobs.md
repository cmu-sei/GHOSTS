# Animation Workflows

So, now we have Animator-generated NPCs, and they have profile information and preferences.

Animator uses n8n workflows to enable dynamic NPC behaviors. These workflows allow us to push our simulation further by addressing:

- What _motivates_ an agent?
- What does an agent _know_ and how did they _learn_ that? How does their knowledge grow over time?
- What _relationships_ does an agent have off-network? How might this influence what they do on the computer?
- What does an agent _believe_? How did they come to that belief?

Workflows operate on a "per cycle" or "step" basis. For each cycle, the workflow processes a list of agents, and the actions or determinations programmed for each. Using n8n provides a visual, drag-and-drop interface for building complex animation logic without writing code.

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

## Animation Workflows with n8n

???+ info "Workflows are now built with n8n"
    As of GHOSTS v9.0, animation jobs have been reimplemented as n8n workflows. This provides a visual, flexible, and extensible approach to building complex NPC behaviors. Base workflow templates are provided in the repository and can be customized through the n8n interface.

There are several workflow types currently available within the Animator system.

### Social Graph

This workflow is responsible for creating and maintaining the social graph of the agents. It creates the initial graph, and then updates it as agents interact with each other and learn different knowledge types.

### Social Belief

This workflow is responsible for creating and maintaining the bayesian social belief of the agents. It creates the initial belief, and then updates it as agents interact with each other and learn different knowledge types.

???+ danger "The following are still early beta"
Here be dragons

The following run with Animator's use of faker to generate content, but really begin to shine when hooked to a Large Language Model (LLM), either hosted as a cloud service or locally.

### Social Sharing

This workflow enables agents to create and post content on social media. It generates initial shares based on the agent and their history. After creating content that the agent would realistically post on social media, the workflow sends an activity to the GHOSTS API to send down to the agent to execute.

### Chat

This workflow enables agents to chat with each other on an OSS chat platform. It generates chat messages based on the agent and their history. After creating a realistic message, the workflow sends an activity to the GHOSTS API to send down to the agent to execute.

1. Ensure ollama is running a chat model locally on localhost:11434
2. Setup a mattermost chat server: `docker run --name mattermost-preview -d --publish 8065:8065 mattermost/mattermost-preview`
3. Login to that server with the values in ./config/chat.json and create a team.
4. From the system console, make that team joinable and allow agents to join the server without invites.

### Full Autonomy

This workflow enables agents to get their next instruction directly from an LLM, based on who they are and their history. While GHOSTS can execute many of these activities, some activity generated will be beyond the scope of this project, but its inclusion can provide rich histories from which to generate future activities. With a powerful LLM, this generates exceptionally realistic activities, but can also be harder to control for the training and exercise audience.

Initial work in LLM-driven autonomous GHOSTS agents was documented in our technical report titled :material-file-document:[_Simulating Realistic Human Activity Using Large Language Model Directives_](https://insights.sei.cmu.edu/library/simulating-realistic-human-activity-using-large-language-model-directives/){:target="\_blank"}.

## Setting Up n8n Workflows

Animator workflows are built using [n8n](https://n8n.io), a workflow automation platform. The GHOSTS stack includes n8n configured with base workflow templates.

**Accessing n8n:**

1. Ensure the GHOSTS stack is running (via docker-compose)
2. Navigate to the n8n interface (typically at `http://localhost:5678`)
3. Browse the pre-configured Animator workflows
4. Customize workflows by adding nodes, configuring triggers, and connecting to the GHOSTS API

**Base Workflows Included:**

- Social Graph Animation
- Social Belief Evolution
- Social Sharing (with LLM integration)
- Chat Interaction
- Full Autonomy

Each workflow can be customized through the n8n visual interface without writing code. For advanced use cases, custom JavaScript or Python nodes can be added to workflows.

## Getting Started With LLM-Driven GHOSTS NPCs

The following is a quick start guide to getting LLM-driven GHOSTS NPCs up and running. This guide assumes you have already installed the GHOSTS API with n8n, and have a working LLM. If you do not have an LLM, you might consider [Ollama](https://ollama.ai) — which is very easy to setup and run on Apple Silicon (and where most reasonable models run very fast).

The process to stand up and use Ollama with n8n workflows:

- Download and install Ollama. Get familiar with creating your own custom models.
- Create a model for the workflow you want to run. Base model configurations are available in the GHOSTS repository.
- Run the commands to create the applicable model (chat for example):

```bash
cd chat
ollama create chat
ollama run chat
```

- You can test the model right in the terminal by interrogating it with questions that an NPC might generate.
- Note that Ollama automatically creates an API endpoint for the LLM at http://localhost:11434. This is the endpoint your n8n workflows will call for content generation.
- In the n8n interface, configure the HTTP Request nodes in your workflows to reference your LLM:
  - **Host**: `http://localhost:11434`
  - **Model**: `chat` (or your custom model name)
  - **Source**: `ollama`

**Configuring LLM Integration in n8n:**

1. Open the desired animation workflow in n8n
2. Locate the "LLM Content Generation" node
3. Update the HTTP Request settings to point to your Ollama instance
4. Test the workflow to ensure connectivity
5. Activate the workflow for continuous operation

You can run multiple models at the same time, but this may be a performance issue. You can also run multiple models on different machines, and point the n8n workflow HTTP nodes to the applicable machine.
