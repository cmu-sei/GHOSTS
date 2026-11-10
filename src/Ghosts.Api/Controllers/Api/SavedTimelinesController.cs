// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Api.Infrastructure.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ghosts.Api.Controllers.Api;

/// <summary>
/// The timeline editor's library: timelines kept on the server so they can be shared and run on any machine
/// </summary>
[ApiController]
[Produces("application/json")]
[Route("api/[controller]")]
public class SavedTimelinesController(ApplicationDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var timelines = await context.SavedTimelines.OrderBy(t => t.Name).ToListAsync(ct);
        return Ok(timelines.Select(ToViewModel).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var timeline = await context.SavedTimelines.FindAsync([id], ct);
        return timeline == null ? NotFound() : Ok(ToViewModel(timeline));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SavedTimelineRequest request, CancellationToken ct)
    {
        if (Invalid(request, out var error)) return BadRequest(new { error });

        var now = DateTime.UtcNow;
        var timeline = new SavedTimeline
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            TimeLineHandlers = request.TimeLineHandlers.ToString(Formatting.None),
            CreatedUtc = now,
            UpdatedUtc = now
        };
        context.SavedTimelines.Add(timeline);
        await context.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Get), new { id = timeline.Id }, ToViewModel(timeline));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SavedTimelineRequest request, CancellationToken ct)
    {
        if (Invalid(request, out var error)) return BadRequest(new { error });

        var timeline = await context.SavedTimelines.FindAsync([id], ct);
        if (timeline == null) return NotFound();

        timeline.Name = request.Name.Trim();
        timeline.TimeLineHandlers = request.TimeLineHandlers.ToString(Formatting.None);
        timeline.UpdatedUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);

        return Ok(ToViewModel(timeline));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var timeline = await context.SavedTimelines.FindAsync([id], ct);
        if (timeline == null) return NotFound();

        context.SavedTimelines.Remove(timeline);
        await context.SaveChangesAsync(ct);
        return NoContent();
    }

    private static bool Invalid(SavedTimelineRequest request, out string error)
    {
        error = string.IsNullOrWhiteSpace(request?.Name) ? "A timeline needs a name"
            : request.TimeLineHandlers == null ? "A timeline needs a timeLineHandlers array"
            : null;
        return error != null;
    }

    private static SavedTimelineViewModel ToViewModel(SavedTimeline t) =>
        new(t.Id, t.Name, JArray.Parse(t.TimeLineHandlers ?? "[]"), t.CreatedUtc, t.UpdatedUtc);
}

public record SavedTimelineRequest(string Name, JArray TimeLineHandlers);
public record SavedTimelineViewModel(Guid Id, string Name, JArray TimeLineHandlers, DateTime CreatedUtc, DateTime UpdatedUtc);
