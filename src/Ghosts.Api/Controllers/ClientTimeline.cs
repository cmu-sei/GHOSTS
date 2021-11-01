// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure;
using Ghosts.Api.Services;
using Ghosts.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using NLog;

namespace ghosts.api.Controllers
{
    /// <summary>
    /// GHOSTS CLIENT CONTROLLER
    /// These endpoints are typically only used by GHOSTS Clients installed and configured to use the GHOSTS C2
    /// </summary>
    [Produces("application/json")]
    [Route("api/[controller]")]
    public class ClientTimelineController : Controller
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private readonly IMachineTimelinesService _service;
        private readonly IMachineService _machineService;

        public ClientTimelineController(IMachineTimelinesService service, IMachineService machineService)
        {
            _service = service;
            _machineService = machineService;
        }

        /// <summary>
        /// Clients post their timelines here, so that the C2 knows what a particular agent is doing
        /// </summary>
        /// <param name="timeline">The client's current timeline</param>
        /// <param name="ct">Cancellation Token</param>
        /// <returns>The saved timeline record</returns>
        [HttpPost]
        public async Task<IActionResult> Index([FromBody] string timeline, CancellationToken ct)
        {
            var id = Request.Headers["ghosts-id"];

            _log.Trace($"Request by {id}");

            var m = WebRequestReader.GetMachine(HttpContext);

            if (!string.IsNullOrEmpty(id))
            {
                m.Id = new Guid(id);
                await _machineService.CreateAsync(m, ct);
            }
            else
            {
                if (!m.IsValid())
                {
                    return StatusCode(StatusCodes.Status401Unauthorized, "Invalid machine request");
                }
            }

            Timeline tl; 
            
            try
            {
                tl = JsonConvert.DeserializeObject<Timeline>(timeline);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return StatusCode(StatusCodes.Status400BadRequest, "Invalid timeline file");
            }

            return Ok(await _service.CreateAsync(m, tl, ct));
        }
    }
}