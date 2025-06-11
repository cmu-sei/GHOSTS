// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Services.ClientServices;
using Microsoft.AspNetCore.Mvc;
using NLog;
using Swashbuckle.AspNetCore.Annotations;

namespace Ghosts.Api.Controllers.Api
{
    /// <summary>
    /// GHOSTS CLIENT CONTROLLER
    /// These endpoints are typically only used by GHOSTS Clients installed and configured to use the GHOSTS C2
    /// </summary>
    [ApiExplorerSettings(IgnoreApi = true)]
    [Produces("application/json")]
    [Route("api/[controller]")]
    public class ClientTimelineController(IClientTimelineService timelineService) : Controller
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

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
            var (success, result, error) = await timelineService.ProcessTimelineAsync(HttpContext, timeline, ct);
            return success ? Ok(result) : BadRequest(error);
        }
    }
}
