// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading;
using System.Threading.Tasks;
using ghosts.api.Infrastructure.Models;
using ghosts.api.Infrastructure.Services;
using Ghosts.Api.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace ghosts.api.Controllers.Api
{
    /// <summary>
    /// Get or update a machine timeline via the API
    /// </summary>
    [Produces("application/json")]
    [Route("api/[controller]")]
    public class TimelinesController(ITimelineService timelineService, IMachineTimelinesService machineTimelinesService) : Controller
    {
        private readonly ITimelineService _timelineService = timelineService;
        private readonly IMachineTimelinesService _machineTimelinesService = machineTimelinesService;

        /// <summary>
        /// This returns all timelines for a requested machine. If all or a specific timeline is not available,
        /// a MachineUpdate request can be made to retrieve the machine timelines   
        /// </summary>
        /// <param name="machineId">Machine Guid</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>MachineTimelines</returns>
        [ProducesResponseType(typeof(MachineTimeline), 200)]
        [SwaggerOperation("TimelinesGetByMachineId")]
        [HttpGet("{machineId}")]
        public async Task<IActionResult> TimelinesGetByMachineId([FromRoute] Guid machineId, CancellationToken ct)
        {
            return Ok(await _machineTimelinesService.GetByMachineIdAsync(machineId, ct));
        }

        /// <summary>
        /// This returns a specific timeline for a requested machine. If the timeline is not available,
        /// a MachineUpdate request can be made to retrieve the machine timeline   
        /// </summary>
        /// <param name="machineId">Machine Guid</param>
        /// /// <param name="timelineId">Timeline Id Guid</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>MachineTimeline</returns>
        [ProducesResponseType(typeof(MachineTimeline), 200)]
        [SwaggerOperation("TimelinesGetByMachineIdAndTimelineId")]
        [HttpGet("{machineId}/{timelineId}")]
        public async Task<IActionResult> TimelinesGetByMachineIdAndTimelineId([FromRoute] Guid machineId, [FromRoute] Guid timelineId, CancellationToken ct)
        {
            return Ok(await _machineTimelinesService.GetByMachineIdAndTimelineIdAsync(machineId, timelineId, ct));
        }

        /// <summary>
        /// Send a new timeline to a particular machine
        /// </summary>
        /// <param name="machineUpdate">The update to send</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>204 No content</returns>
        [HttpPost]
        // [ProducesResponseType(typeof(IActionResult), (int) HttpStatusCode.NoContent)] Swagger hates this https://stackoverflow.com/questions/35605427/swagger-ui-freezes-after-api-fetch-and-browser-crashes
        [SwaggerOperation("TimelinesCreate")]
        public async Task<IActionResult> TimelinesCreate([FromBody] MachineUpdateViewModel machineUpdate, CancellationToken ct)
        {
            await _timelineService.UpdateAsync(machineUpdate, ct);
            return NoContent();
        }

        [HttpPost("{machineId}/{timelineId}/stop")]
        [SwaggerOperation("TimelinesStop")]
        public async Task<IActionResult> TimelinesStop([FromRoute] Guid machineId, [FromRoute] Guid timelineId, CancellationToken ct)
        {
            await _timelineService.StopAsync(machineId, timelineId, ct);
            return NoContent();
        }
    }
}
