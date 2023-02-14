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
