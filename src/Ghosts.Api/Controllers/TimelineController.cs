// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Models;
using Ghosts.Api.Services;
using Ghosts.Api.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Swashbuckle.AspNetCore.Annotations;

namespace ghosts.api.Controllers
{
    /// <summary>
    /// Get or update a machine timeline via the API
    /// </summary>
    public class TimelineController : Controller
    {
        private readonly ITimelineService _timelineService;
        private readonly IMachineTimelineService _machineTimelineService;

        public TimelineController(ITimelineService timelineService, IMachineTimelineService machineTimelineService)
        {
            _timelineService = timelineService;
            _machineTimelineService = machineTimelineService;
        }

        /// <summary>
        /// This returns the timeline for a requested machine. If the timeline is not available,
        /// a MachineUpdate request can be made to retrieve the machine timeline   
        /// </summary>
        /// <param name="machineId">Machine Guid</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>MachineTimeline</returns>
        [ProducesResponseType(typeof(MachineTimeline), 200)]
        [HttpGet("timeline/{machineId}")]
        public async Task<IActionResult> Timeline([FromRoute] Guid machineId, CancellationToken ct)
        {
            return Ok(await _machineTimelineService.GetByMachineIdAsync(machineId, ct));
        }
        
        /// <summary>
        /// Send a new timeline to a particular machine
        /// </summary>
        /// <param name="machineUpdate">The update to send</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>204 No content</returns>
        [HttpPost("timeline")]
        // [ProducesResponseType(typeof(Task<IActionResult>), (int) HttpStatusCode.NoContent)] Swagger hates this https://stackoverflow.com/questions/35605427/swagger-ui-freezes-after-api-fetch-and-browser-crashes
        [SwaggerOperation(OperationId = "createTimeline")]
        public async Task<IActionResult> Timeline([FromBody] MachineUpdateViewModel machineUpdate, CancellationToken ct)
        {
            await _timelineService.UpdateAsync(machineUpdate, ct);
            return NoContent();
        }

        /// <summary>
        /// Send a new timeline to an entire group of machines
        /// </summary>
        /// <param name="groupId">Group Guid</param>
        /// <param name="machineUpdate">The update to send</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>204 No content</returns>
        [HttpPost("timeline/bygroup/{groupId}")]
        // [ProducesResponseType(typeof(Task<IActionResult>), (int) HttpStatusCode.NoContent)] Swagger hates this
        [SwaggerOperation(OperationId = "createTimelineForGroup")]
        public async Task<IActionResult> GroupTimeline([FromRoute] int groupId, [FromBody] MachineUpdateViewModel machineUpdate, CancellationToken ct)
        {
            await _timelineService.UpdateGroupAsync(groupId, machineUpdate, ct);
            return NoContent();
        }
    }
}