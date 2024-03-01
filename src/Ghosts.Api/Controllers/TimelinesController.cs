// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading;
using System.Threading.Tasks;
using ghosts.api.Infrastructure.Models;
using ghosts.api.Infrastructure.Services;
using Ghosts.Api.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace ghosts.api.Controllers
{
    /// <summary>
    /// Get or update a machine timeline via the API
    /// </summary>
    public class TimelinesController : Controller
    {
        private readonly ITimelineService _timelineService;
        private readonly IMachineTimelinesService _machineTimelinesService;

        public TimelinesController(ITimelineService timelineService, IMachineTimelinesService machineTimelinesService)
        {
            _timelineService = timelineService;
            _machineTimelinesService = machineTimelinesService;
        }

        /// <summary>
        /// This returns all timelines for a requested machine. If all or a specific timeline is not available,
        /// a MachineUpdate request can be made to retrieve the machine timelines   
        /// </summary>
        /// <param name="machineId">Machine Guid</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>MachineTimelines</returns>
        [ProducesResponseType(typeof(MachineTimeline), 200)]
        [HttpGet("timelines/{machineId}")]
        public async Task<IActionResult> Timeline([FromRoute] Guid machineId, CancellationToken ct)
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
        [HttpGet("timelines/{machineId}/{timelineId}")]
        public async Task<IActionResult> TimelineById([FromRoute] Guid machineId, [FromRoute] Guid timelineId, CancellationToken ct)
        {
            return Ok(await _machineTimelinesService.GetByMachineIdAndTimelineIdAsync(machineId, timelineId, ct));
        }
        
        /// <summary>
        /// Send a new timeline to a particular machine
        /// </summary>
        /// <param name="machineUpdate">The update to send</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>204 No content</returns>
        [HttpPost("timelines")]
        // [ProducesResponseType(typeof(Task<IActionResult>), (int) HttpStatusCode.NoContent)] Swagger hates this https://stackoverflow.com/questions/35605427/swagger-ui-freezes-after-api-fetch-and-browser-crashes
        [SwaggerOperation(OperationId = "createTimeline")]
        public async Task<IActionResult> Timeline([FromBody] MachineUpdateViewModel machineUpdate, CancellationToken ct)
        {
            await _timelineService.UpdateAsync(machineUpdate, ct);
            return NoContent();
        }
        
        [HttpPost("timelines/{machineId}/{timelineId}/stop")]
        [SwaggerOperation(OperationId = "stopTimeline")]
        public async Task<IActionResult> Timeline([FromRoute] Guid machineId, [FromRoute] Guid timelineId, CancellationToken ct)
        {
            await _timelineService.StopAsync(machineId, timelineId, ct);
            return NoContent();
        }

        /// <summary>
        /// Send a new timeline to an entire group of machines
        /// </summary>
        /// <param name="groupId">Group Guid</param>
        /// <param name="machineUpdate">The update to send</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>204 No content</returns>
        [HttpPost("timelines/bygroup/{groupId}")]
        // [ProducesResponseType(typeof(Task<IActionResult>), (int) HttpStatusCode.NoContent)] Swagger hates this
        [SwaggerOperation(OperationId = "createTimelineForGroup")]
        public async Task<IActionResult> GroupTimeline([FromRoute] int groupId, [FromBody] MachineUpdateViewModel machineUpdate, CancellationToken ct)
        {
            await _timelineService.UpdateGroupAsync(groupId, machineUpdate, ct);
            return NoContent();
        }
    }
}