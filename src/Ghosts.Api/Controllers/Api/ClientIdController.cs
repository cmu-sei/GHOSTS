// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Services.ClientServices;
using Microsoft.AspNetCore.Http;
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
    public class ClientIdController(IClientIdService clientIdService) : Controller
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Clients use this endpoint to get their unique GHOSTS system ID
        /// </summary>
        /// <param name="ct">Cancellation Token</param>
        /// <returns>A client's particular unique GHOSTS system ID (GUID)</returns>
        [SwaggerOperation("ClientIdGet")]
        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            var (success, machineId, error) = await clientIdService.GetMachineIdAsync(HttpContext, ct);
            return success ? Ok(machineId) : StatusCode(StatusCodes.Status401Unauthorized, error);
        }
    }
}
