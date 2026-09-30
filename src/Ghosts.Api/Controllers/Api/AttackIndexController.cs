// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Linq;
using Ghosts.Api.Infrastructure.ScenarioDocuments;
using Microsoft.AspNetCore.Mvc;

namespace Ghosts.Api.Controllers.Api;

/// <summary>
/// Read-only lookups against the ATT&amp;CK indexes the validator already carries as embedded resources
/// (schemas/scenario-document/corpus/attack-index.json and attack-groups.json, built together at the
/// same MITRE bundle commit). It exists so that an authoring agent has somewhere to resolve a
/// technique id, or an adversary's real name, other than its own memory — the failure mode both
/// Step 0 runs hit for technique ids — and so the answer comes from the same index tier 2 validates
/// against. One index, one verdict: a tool that carried its own copy could tell an author an id is
/// fine and then watch the validator reject it.
/// </summary>
[ApiController]
[Route("api/attack/index")]
public class AttackIndexController : ControllerBase
{
    /// <summary>
    /// Techniques matching an exact id, an id prefix, or a fragment of a name. Revoked and deprecated
    /// techniques are in the results, flagged: the caller needs to know the id it was about to use is
    /// dead, which is the whole reason to ask.
    /// </summary>
    // GET: api/attack/index/techniques?q=phishing&take=10
    [HttpGet("techniques")]
    public IActionResult SearchTechniques([FromQuery] string q, [FromQuery] int take = 25)
    {
        var matches = AttackIndex.Search(q, take);

        return Ok(new
        {
            query = q,
            provenance = AttackIndex.Provenance,
            indexed = AttackIndex.Count,
            count = matches.Count,
            techniques = matches.Select(t => new
            {
                id = t.Id,
                name = t.Name,
                domains = t.Domains,
                revoked = t.Revoked,
                deprecated = t.Deprecated
            })
        });
    }

    /// <summary>
    /// Intrusion sets (groups) matching an exact id, an id prefix, a fragment of the primary name, or
    /// a fragment of a known alias — "Sandworm" and "Voodoo Bear" both find G0034. Naming a real
    /// adversary is a mission judgment (ELICITATION.md E3); this exists so the id attached to that
    /// name is looked up rather than recalled, the same reason api/attack/index/techniques exists.
    /// </summary>
    // GET: api/attack/index/groups?q=sandworm&take=10
    [HttpGet("groups")]
    public IActionResult SearchGroups([FromQuery] string q, [FromQuery] int take = 25)
    {
        var matches = AttackGroupIndex.Search(q, take);

        return Ok(new
        {
            query = q,
            provenance = AttackGroupIndex.Provenance,
            indexed = AttackGroupIndex.Count,
            count = matches.Count,
            groups = matches.Select(g => new
            {
                id = g.Id,
                name = g.Name,
                aliases = g.Aliases,
                domains = g.Domains,
                revoked = g.Revoked,
                deprecated = g.Deprecated
            })
        });
    }
}
