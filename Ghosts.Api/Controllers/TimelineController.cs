// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Services;
using Ghosts.Api.ViewModels;
using Microsoft.AspNetCore.Mvc;
using NLog;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ghosts.api.Controllers
{
    /// <summary>
    /// for updating a machine(s) timeline via the API
    /// </summary>
    public class TimelineController : Controller
    {
        private static Logger log = LogManager.GetCurrentClassLogger();
        private readonly ITimelineService _timelineService;
        
        public TimelineController(ITimelineService timelineService, IBackgroundQueue queue)
        {
            _timelineService = timelineService;
        }

        [HttpPost("Timeline")]
        [ProducesResponseType(typeof(Task<IActionResult>), (int)HttpStatusCode.NoContent)]
        [SwaggerOperation(operationId: "createTimeline")]
        public async Task<IActionResult> Timeline([FromBody] MachineUpdateViewModel machineUpdate, CancellationToken ct)
        {
            await this._timelineService.UpdateAsync(machineUpdate, ct);
            return NoContent();
        }
        
        [HttpPost("Timeline/ByGroup/{groupId}")]
        [ProducesResponseType(typeof(Task<IActionResult>), (int)HttpStatusCode.NoContent)]
        [SwaggerOperation(operationId: "createTimelineForGroup")]
        public async Task<IActionResult> GroupTimeline([FromRoute] int groupId, [FromBody] MachineUpdateViewModel machineUpdate, CancellationToken ct)
        {
            await this._timelineService.UpdateGroupAsync(groupId, machineUpdate, ct);
            return NoContent();
        }
    }
}