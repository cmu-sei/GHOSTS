// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Api.Infrastructure.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ghosts.Api.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class HypothesesController(ApplicationDbContext context, ILogger<HypothesesController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool? active, CancellationToken ct)
    {
        try
        {
            var query = context.Hypotheses.AsQueryable();
            if (active.HasValue)
                query = query.Where(h => h.IsActive == active.Value);

            return Ok(await query.OrderBy(h => h.Name).ToListAsync(ct));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting hypotheses");
            return StatusCode(500, new { error = "Error retrieving hypotheses" });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        try
        {
            var hypothesis = await context.Hypotheses.FindAsync([id], ct);
            if (hypothesis == null) return NotFound();
            return Ok(hypothesis);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting hypothesis {Id}", id);
            return StatusCode(500, new { error = "Error retrieving hypothesis" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateHypothesisDto dto, CancellationToken ct)
    {
        try
        {
            var hypothesis = new Hypothesis
            {
                Name = dto.Name,
                Keywords = dto.Keywords ?? string.Empty,
                DefaultLikelihood = dto.DefaultLikelihood ?? 0.6m,
                IsActive = true,
                CreatedUtc = DateTime.UtcNow
            };

            context.Hypotheses.Add(hypothesis);
            await context.SaveChangesAsync(ct);

            return CreatedAtAction(nameof(GetById), new { id = hypothesis.Id }, hypothesis);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating hypothesis");
            return StatusCode(500, new { error = "Error creating hypothesis" });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateHypothesisDto dto, CancellationToken ct)
    {
        try
        {
            var hypothesis = await context.Hypotheses.FindAsync([id], ct);
            if (hypothesis == null) return NotFound();

            if (dto.Name != null) hypothesis.Name = dto.Name;
            if (dto.Keywords != null) hypothesis.Keywords = dto.Keywords;
            if (dto.DefaultLikelihood.HasValue) hypothesis.DefaultLikelihood = dto.DefaultLikelihood.Value;
            if (dto.IsActive.HasValue) hypothesis.IsActive = dto.IsActive.Value;

            await context.SaveChangesAsync(ct);
            return Ok(hypothesis);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating hypothesis {Id}", id);
            return StatusCode(500, new { error = "Error updating hypothesis" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try
        {
            var hypothesis = await context.Hypotheses.FindAsync([id], ct);
            if (hypothesis == null) return NotFound();

            context.Hypotheses.Remove(hypothesis);
            await context.SaveChangesAsync(ct);
            return NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting hypothesis {Id}", id);
            return StatusCode(500, new { error = "Error deleting hypothesis" });
        }
    }
}

public record CreateHypothesisDto(string Name, string Keywords, decimal? DefaultLikelihood);
public record UpdateHypothesisDto(string Name, string Keywords, decimal? DefaultLikelihood, bool? IsActive);
