# Intent template

Give this to a developer who wants a scenario but has no intent yet. **They own this artifact** — the
agent does not write it for them, because the whole loop rests on the intent being a person's statement
of what the exercise is for. An intent an agent wrote is an intent that agrees with the agent.

It is **a page at most**, and it reads like a commander's intent, because that is what it is. Anything
longer is the draft document arriving early; anything that reads like a form is a form.

## The shape

```markdown
# <Exercise name> — intent, v1

<N days/hours> of play · audience: <who they are, how many, how experienced>

<What happens: the adversary, what it does, when, and how far it gets. Two or three sentences.>

<What the audience is expected to do about it, and what counts as success — the standard, not the
aspiration.>

Terrain: <the base slice this starts from, plus what this exercise adds>.

Rules of engagement: <what the audience may do; what the adversary will not do>.
```

## What a filled-in one looks like

This is §4a's example, and it is the register to aim for — short enough to argue about in a meeting:

> **Substation Watch — intent, v4** · 11 days of play · audience: a nine-person utility SOC, first
> rotation. A state-sponsored actor phishes the billing staff on day 2 and, by day 5, holds credentialed
> access to an engineering workstation that reaches the OT network. The blue cell is expected to catch
> the lateral movement before the substation trips on day 9; success is a correct containment decision by
> day 7, not a clean network. Terrain: the *regional-utility* slice, plus two OT segments this exercise
> adds.

Note what that does in six sentences: it names the audience and their proficiency, the adversary and its
tempo, the decision the exercise exists to force, the deadline, the explicit statement that a clean
network is *not* the standard, and the terrain it starts from. Each of those closes one of the fourteen
checklist questions, and every one it leaves open is a question the agent will ask.

Note also what it does not do: no technique ids, no host names, no inject list, no times of day. Those
are the draft's job — orders of magnitude more detail, all of it generated, none of it trusted yet.

## The parts worth writing even when they feel obvious

Four of these are absent from almost every intent, including §4a's, and each one cost a Step 0 run a
guess:

- **What the audience may and may not do.** Isolate? Rebuild? Is live malware permitted? What is out of
  bounds for the red cell. A default here is a guess about someone else's authorities.
- **What happens if they succeed early.** If the phish is caught on day 2, does the exercise stop, or
  does the adversary try again? Both are defensible and they are different exercises.
- **Whether the adversary is real or fictional.** Naming a real APT imports real attribution into a
  training exercise; inventing one gives up the real TTP set. There is no neutral choice, so it is not
  the agent's to make.
- **Whether the clock is real time or compressed.** "11 days of play" does not say whether that is eleven
  real days or eleven exercise-days in a window, and the answer changes what the deadline means.

## Versioning it

Number the intent (`v1`, `v2`) and keep it in the working folder as `intent.md`. When a validator finding
keeps recurring, or a reviewer disputes the plan, the answer is usually that the intent was ambiguous —
not that the draft was weak. Edit the intent, bump the version, and re-draft: that is the loop closing,
and it is the reason the intent is a file rather than a conversation.
