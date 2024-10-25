// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using ghosts.api.Infrastructure.Models;
using Newtonsoft.Json;

namespace ghosts.api.Infrastructure.ContentServices;

public class GenericContentHelpers
{
    public static string GetFlattenedNpc(NpcRecord agent)
    {
        // squish parts of the json that are irrelevant for LLM & to keep tokens/costs down
        agent.Campaign = null;
        agent.Enclave = null;
        agent.Team = null;
        agent.NpcProfile.Rank = null;
        agent.NpcProfile.CAC = null;
        agent.NpcProfile.Unit = null;

        var settings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
        var flattenedAgent = JsonConvert.SerializeObject(agent, settings);
        flattenedAgent = flattenedAgent.Replace("{", "").Replace("}", "").Replace("[", "").Replace("]", "").Replace(" \"", "").Replace("\"", "");

        return flattenedAgent;
    }
}
