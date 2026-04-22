using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ghosts.Api.Controllers.Api;

[Route("api/[controller]")]
[ApiController]
public class ExecutionMapController(IExecutionMapService mapService) : ControllerBase
{
    /// <summary>
    /// Returns available map layers and feature counts for an execution.
    /// </summary>
    [HttpGet("{executionId:int}/layers")]
    public async Task<IActionResult> GetLayers(int executionId, CancellationToken ct)
    {
        var layers = await mapService.GetLayersAsync(executionId, ct);
        return Ok(layers);
    }

    /// <summary>
    /// Returns all map features for an execution as a GeoJSON FeatureCollection.
    /// Supports optional time-window filtering for replay.
    /// </summary>
    [HttpGet("{executionId:int}/features")]
    public async Task<IActionResult> GetAllFeatures(
        int executionId,
        [FromQuery] DateTime? timeFrom,
        [FromQuery] DateTime? timeTo,
        CancellationToken ct)
    {
        var collection = await mapService.GetAllFeaturesAsync(executionId, timeFrom, timeTo, ct);
        return Ok(collection);
    }

    /// <summary>
    /// Returns map features filtered by layer/type for an execution as GeoJSON.
    /// </summary>
    [HttpGet("{executionId:int}/features/{featureType}")]
    public async Task<IActionResult> GetFeaturesByType(
        int executionId,
        string featureType,
        [FromQuery] DateTime? timeFrom,
        [FromQuery] DateTime? timeTo,
        [FromQuery] string status,
        [FromQuery] string team,
        CancellationToken ct)
    {
        var collection = await mapService.GetFeaturesAsync(executionId, featureType, timeFrom, timeTo, status, team, ct);
        return Ok(collection);
    }

    /// <summary>
    /// Returns connections/links between features as GeoJSON LineStrings.
    /// </summary>
    [HttpGet("{executionId:int}/connections")]
    public async Task<IActionResult> GetConnections(int executionId, CancellationToken ct)
    {
        var collection = await mapService.GetConnectionsAsync(executionId, ct);
        return Ok(collection);
    }

    /// <summary>
    /// Returns timeline metadata for the execution: time bounds, event buckets for the scrubber.
    /// </summary>
    [HttpGet("{executionId:int}/timeline")]
    public async Task<IActionResult> GetTimeline(
        int executionId,
        [FromQuery] int buckets = 20,
        CancellationToken ct = default)
    {
        var info = await mapService.GetTimelineAsync(executionId, buckets, ct);
        return Ok(info);
    }

    /// <summary>
    /// Searches map features by name, ID, hostname, or description.
    /// </summary>
    [HttpGet("{executionId:int}/search")]
    public async Task<IActionResult> Search(
        int executionId,
        [FromQuery] string q,
        CancellationToken ct)
    {
        var results = await mapService.SearchAsync(executionId, q, ct);
        return Ok(results);
    }

    /// <summary>
    /// Returns detailed information about a specific map entity including related entities and recent events.
    /// </summary>
    [HttpGet("{executionId:int}/entity/{featureType}/{entityId}")]
    public async Task<IActionResult> GetEntityDetail(
        int executionId,
        string featureType,
        string entityId,
        CancellationToken ct)
    {
        var detail = await mapService.GetEntityDetailAsync(executionId, featureType, entityId, ct);
        if (detail == null) return NotFound(new { error = "Entity not found" });
        return Ok(detail);
    }

    /// <summary>
    /// Creates a new map feature (point, event, connection, etc.).
    /// </summary>
    [HttpPost("features")]
    public async Task<IActionResult> CreateFeature([FromBody] CreateMapFeatureDto dto, CancellationToken ct)
    {
        var result = await mapService.CreateAsync(dto, ct);
        return Created($"/api/executionmap/features/{result.Id}", result);
    }

    /// <summary>
    /// Creates multiple map features in a single request.
    /// </summary>
    [HttpPost("features/bulk")]
    public async Task<IActionResult> BulkCreateFeatures([FromBody] List<CreateMapFeatureDto> dtos, CancellationToken ct)
    {
        var results = await mapService.BulkCreateAsync(dtos, ct);
        return Created("", results);
    }

    /// <summary>
    /// Updates an existing map feature.
    /// </summary>
    [HttpPut("features/{id:int}")]
    public async Task<IActionResult> UpdateFeature(int id, [FromBody] UpdateMapFeatureDto dto, CancellationToken ct)
    {
        var result = await mapService.UpdateAsync(id, dto, ct);
        if (result == null) return NotFound(new { error = "Feature not found" });
        return Ok(result);
    }

    /// <summary>
    /// Deletes a map feature.
    /// </summary>
    [HttpDelete("features/{id:int}")]
    public async Task<IActionResult> DeleteFeature(int id, CancellationToken ct)
    {
        var deleted = await mapService.DeleteAsync(id, ct);
        if (!deleted) return NotFound(new { error = "Feature not found" });
        return NoContent();
    }
}
