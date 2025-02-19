// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.ComponentModel.DataAnnotations;
using System.Net;
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
    public class TimelinesController(
        ITimelineService timelineService, IMachineTimelinesService machineTimelinesService) : Controller
    {
        /// <summary>
        /// Helper method to return a standardized NotFound response.
        /// </summary>
        private IActionResult NotFoundResponse(string message) => NotFound(new { success = false, message });

        /// <summary>
        /// Helper method to return a standardized BadRequest response.
        /// </summary>
        private IActionResult BadRequestResponse(string message) => BadRequest(new { success = false, message });

        /// <summary>
        /// Helper method to return a standardized success response.
        /// </summary>
        private IActionResult SuccessResponse(string message) => Ok(new { success = true, message });

        /// <summary>
        /// This returns all timelines for a requested machine. If all or a specific timeline is not available,
        /// a MachineUpdate request can be made to retrieve the machine timelines
        /// </summary>
        /// <param name="machineId">The unique identifier of the machine.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A list of timelines for the given machine.</returns>
        [ProducesResponseType(typeof(MachineTimeline), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(object), (int)HttpStatusCode.NotFound)]
        [SwaggerOperation(nameof(TimelinesGetByMachineId))]
        [HttpGet("{machineId}")]
        public async Task<IActionResult> TimelinesGetByMachineId([FromRoute] Guid machineId, CancellationToken ct)
        {
            var timelines = await machineTimelinesService.GetByMachineIdAsync(machineId, ct);
            return timelines != null ? Ok(timelines) : NotFoundResponse($"No timelines found for Machine ID: {machineId}");
        }

        /// <summary>
        /// This returns a specific timeline for a requested machine. If the timeline is not available,
        /// a MachineUpdate request can be made to retrieve the machine timeline
        /// </summary>
        /// <param name="machineId">The unique identifier of the machine.</param>
        /// <param name="timelineId">The unique identifier of the timeline.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The requested machine timeline.</returns>
        [ProducesResponseType(typeof(MachineTimeline), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(object), (int)HttpStatusCode.NotFound)]
        [SwaggerOperation(nameof(TimelinesGetByMachineIdAndTimelineId))]
        [HttpGet("{machineId}/{timelineId}")]
        public async Task<IActionResult> TimelinesGetByMachineIdAndTimelineId([FromRoute] Guid machineId, [FromRoute] Guid timelineId, CancellationToken ct)
        {
            var timeline = await machineTimelinesService.GetByMachineIdAndTimelineIdAsync(machineId, timelineId, ct);
            return timeline != null ? Ok(timeline) : NotFoundResponse($"Timeline ID {timelineId} not found for Machine ID {machineId}");
        }

        /// <summary>
        /// Send a new timeline to a machine
        /// </summary>
        /// <param name="machineUpdate">The timeline update data.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Success message if update is processed.</returns>
        [HttpPost]
        [ProducesResponseType(typeof(object), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(object), (int)HttpStatusCode.BadRequest)]
        [SwaggerOperation(nameof(TimelinesCreate))]
        public async Task<IActionResult> TimelinesCreate([FromBody, Required] MachineUpdateViewModel machineUpdate, CancellationToken ct)
        {
            await timelineService.UpdateAsync(machineUpdate, ct);
            return SuccessResponse("Timeline updated successfully");
        }

        /// <summary>
        /// Stops a specific timeline for a machine.
        /// </summary>
        /// <param name="machineId">The unique identifier of the machine.</param>
        /// <param name="timelineId">The unique identifier of the timeline.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Success message if the timeline is stopped, or an error message.</returns>
        [HttpPost("{machineId}/{timelineId}/stop")]
        [ProducesResponseType(typeof(object), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(object), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(object), (int)HttpStatusCode.BadRequest)]
        [SwaggerOperation(nameof(TimelinesStop))]
        public async Task<IActionResult> TimelinesStop([FromRoute] Guid machineId, [FromRoute] Guid timelineId, CancellationToken ct)
        {
            await timelineService.StopAsync(machineId, timelineId, ct);
            return SuccessResponse("Timeline stopped successfully");
        }
    }
}
