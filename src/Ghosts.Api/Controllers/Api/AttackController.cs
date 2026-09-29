// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Linq;
using Ghosts.Api.Infrastructure.ScenarioDocuments;
using Microsoft.AspNetCore.Mvc;

namespace Ghosts.Api.Controllers.Api;

/// <summary>
/// Read-only lookups against the ATT&amp;CK index the validator already carries as an embedded resource
/// (schemas/scenario-document/corpus/attack-index.json). It exists so that an authoring agent has
/// somewhere to resolve a technique id other than its own memory — the failure mode both Step 0 runs
/// hit — and so the answer comes from the same index tier 2 validates against. One index, one verdict:
/// a tool that carried its own copy could tell an author a technique is fine and then watch the
/// validator reject it.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AttackController : ControllerBase
{
    /// <summary>
    /// Techniques matching an exact id, an id prefix, or a fragment of a name. Revoked and deprecated
    /// techniques are in the results, flagged: the caller needs to know the id it was about to use is
    /// dead, which is the whole reason to ask.
    /// </summary>
    // GET: api/attack/techniques?q=phishing&take=10
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
}
