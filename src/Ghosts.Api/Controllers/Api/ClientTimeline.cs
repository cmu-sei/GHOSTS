// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading;
using System.Threading.Tasks;
using ghosts.api.Infrastructure.Services;
using Ghosts.Api.Infrastructure;
using Ghosts.Domain;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using NLog;
using Swashbuckle.AspNetCore.Annotations;

namespace ghosts.api.Controllers.Api
{
    /// <summary>
    /// GHOSTS CLIENT CONTROLLER
    /// These endpoints are typically only used by GHOSTS Clients installed and configured to use the GHOSTS C2
    /// </summary>
    [ApiExplorerSettings(IgnoreApi = true)]
    [Produces("application/json")]
    [Route("api/[controller]")]
    public class ClientTimelineController(IMachineTimelinesService service, IMachineService machineService) : Controller
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private readonly IMachineTimelinesService _service = service;
        private readonly IMachineService _machineService = machineService;

        /// <summary>
        /// Clients post their timelines here, so that the C2 knows what a particular agent is doing
        /// </summary>
        /// <param name="timeline">The client's current timeline</param>
        /// <param name="ct">Cancellation Token</param>
        /// <returns>The saved timeline record</returns>
        [SwaggerOperation("ClientTimelineCreate")]
        [HttpPost]
        public async Task<IActionResult> Index([FromBody] string timeline, CancellationToken ct)
        {
            if (!Request.Headers.TryGetValue("ghosts-id", out var id))
            {
                _log.Warn("Request missing ghosts-id header");
                return Unauthorized("Missing ghosts-id header");
            }

            _log.Info($"Request by {id}");

            var m = WebRequestReader.GetMachine(HttpContext);

            if (!string.IsNullOrEmpty(id))
            {
                m.Id = new Guid(id);
                await _machineService.CreateAsync(m, ct);
            }
            else if (!m.IsValid())
            {
                return Unauthorized("Invalid machine request");
            }

            Timeline tl;

            try
            {
                tl = JsonConvert.DeserializeObject<Timeline>(timeline);
            }
            catch (Exception e)
            {
                _log.Error(e, "Invalid timeline file");
                return BadRequest("Invalid timeline file");
            }

            var createdTimeline = await _service.CreateAsync(m, tl, ct);
            return Ok(createdTimeline);
        }
    }
}
