// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading;
using System.Threading.Tasks;
using ghosts.api.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace ghosts.api.Controllers.Api
{
    [Produces("application/json")]
    [Route("api/[controller]")]
    [ResponseCache(Duration = 5)]
    public class TrackablesController(ITrackableService service) : Controller
    {
        private readonly ITrackableService _service = service;

        /// <summary>
        /// Gets all trackables in the system
        /// </summary>
        /// <param name="ct">Cancellation Token</param>
        /// <returns>List of Trackables</returns>
        [SwaggerOperation("TrackablesGetAll")]
        [HttpGet]
        public async Task<IActionResult> GetTrackables(CancellationToken ct)
        {
            var list = await _service.GetAsync(ct);
            if (list == null) return NotFound();
            return Ok(list);
        }

        [SwaggerOperation("TrackablesGetHistoryById")]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetTrackableHistory([FromRoute] Guid id, CancellationToken ct)
        {
            var list = await _service.GetActivityByTrackableId(id, ct);
            if (list == null) return NotFound();
            return Ok(list);
        }
    }
}
