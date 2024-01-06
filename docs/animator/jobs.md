# Animation Jobs

So, now we have Animator-generated NPCs, and they have profile information and preferences.

Animator has a job system that might enables us to push our simulation further:

- What *motivates* an agent?
- What does an agent *know* and how did they *learn* that? How does their knowledge grow over time?
- What *relationships* does an agent have off-network? How might this influence what they do on the computer?
- What does an agent *believe*? How did they come to that belief?

Jobs operate on a "per cycle" or "step" basis. For each cycle, the job processes a list of agents, and the actions or determinations programmed for each.

## Decision-Making Framework

We can use any combination of the following to drive agent decision-making:

### Motivation

We implement the [Reiss Motivational Profile](https://www.reissmotivationprofile.com/) (RMP) - which is a mathematical framework for reasoning about agent comparative motivations - agent A is twice as motivated by X than agent B - that is baselined every few years. 

### Relationships

Agents build relationships with other agents in the cohort. These get better or worse over time.

How this works is that each agent has the potential to interact with *n* other agents (they can also potentially transfer knowledge as a result). The more an agent knows about a particular subject, maybe the more likely they are to transfer information to another agent.

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

### Full Autonomy

This has agents get their next instruction directly from an LLM, based on who they are and their history. While GHOSTS can execute many of these activities, some activty generated will be beyond the scope of this project, but its inclusion can provide rich histories from which to generate future activities. With a powerful LLM, this generates some exceptionally real activities, but can also be hard to control for the training and exercise audience. 

Initial work in LLM-driven autonomous GHOSTS agents was documented in our technical report titled :material-file-document:[_Simulating Realistic Human Activity Using Large Language Model Directives_](https://insights.sei.cmu.edu/library/simulating-realistic-human-activity-using-large-language-model-directives/){:target="_blank"}.

