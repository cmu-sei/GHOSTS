// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Ghosts.Api.Controllers.Api
{
    [Produces("application/json")]
    [Route("api/[controller]")]
    [ResponseCache(Duration = 5)]
    public class TrackablesController(ITrackableService service) : Controller
    {
        private readonly ITrackableService _service = service;

        /// <summary>
        /// Gets all trackable history records in the system
        /// </summary>
        /// <param name="ct">Cancellation Token</param>
        /// <returns>List of HistoryTrackable records</returns>
        [SwaggerOperation("TrackablesGetAll")]
        [HttpGet]
        public async Task<IActionResult> GetTrackables(CancellationToken ct)
        {
            var list = await _service.GetAsync(ct);
            if (list == null) return NotFound();
            return Ok(list);
        }

        /// <summary>
        /// Gets trackable history for a specific trackable ID
        /// </summary>
        /// <param name="id">Trackable ID</param>
        /// <param name="ct">Cancellation Token</param>
        /// <returns>List of HistoryTrackable records for the specified ID</returns>
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
