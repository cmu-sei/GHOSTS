// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using NLog;

namespace Ghosts.Api.Controllers.Api
{
    [Route("api/attack")]
    [ApiController]
    public class AttackController(IScenarioEnrichmentService enrichmentService) : ControllerBase
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        [HttpGet("techniques")]
        public async Task<ActionResult> SearchTechniques([FromQuery] string q, [FromQuery] string tactic, CancellationToken ct)
        {
            try
            {
                var techniques = await enrichmentService.SearchTechniquesAsync(q, tactic, ct);
                var dtos = techniques.Select(t => new AttackTechniqueSummaryDto(
                    t.Id, t.Name, t.Tactics, t.IsSubtechnique)).ToList();
                return Ok(dtos);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error searching techniques");
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("techniques/{id}")]
        public async Task<ActionResult> GetTechnique(string id, CancellationToken ct)
        {
            try
            {
                var technique = await enrichmentService.GetTechniqueAsync(id, ct);
                return Ok(new AttackTechniqueDto(
                    technique.Id, technique.Name, technique.Description,
                    technique.Tactics, technique.Platforms, technique.Url,
                    technique.IsSubtechnique, technique.ParentId));
            }
            catch (InvalidOperationException)
            {
                return NotFound();
            }
        }

        [HttpGet("groups")]
        public async Task<ActionResult> SearchGroups([FromQuery] string q, CancellationToken ct)
        {
            try
            {
                var groups = await enrichmentService.SearchGroupsAsync(q, ct);
                var dtos = groups.Select(g => new AttackGroupSummaryDto(
                    g.Id, g.Name, g.Aliases, g.TechniqueUsages?.Count ?? 0)).ToList();
                return Ok(dtos);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error searching groups");
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("groups/{id}")]
        public async Task<ActionResult> GetGroup(string id, CancellationToken ct)
        {
            try
            {
                var group = await enrichmentService.GetGroupAsync(id, ct);
                var techniques = group.TechniqueUsages?.Select(tu =>
                    new AttackTechniqueSummaryDto(tu.Technique.Id, tu.Technique.Name,
                        tu.Technique.Tactics, tu.Technique.IsSubtechnique)).ToList();
                return Ok(new AttackGroupDto(
                    group.Id, group.Name, group.Aliases,
                    group.Description, group.Url, techniques));
            }
            catch (InvalidOperationException)
            {
                return NotFound();
            }
        }

        [HttpPost("import")]
        public async Task<ActionResult> ImportAttackData(CancellationToken ct)
        {
            try
            {
                var stixPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "AttackData", "enterprise-attack.json");
                if (!System.IO.File.Exists(stixPath))
                {
                    return BadRequest($"ATT&CK data file not found at {stixPath}");
                }

                await enrichmentService.ImportAttackDataAsync(stixPath, ct);
                return Ok(new { message = "ATT&CK data imported successfully" });
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error importing ATT&CK data");
                return StatusCode(500, ex.Message);
            }
        }
    }
}
