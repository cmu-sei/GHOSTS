// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Threading;
using System.Threading.Tasks;
using ghosts.api.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
    public class ClientIdController(IMachineService service) : Controller
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private readonly IMachineService _service = service;

        /// <summary>
        /// Clients use this endpoint to get their unique GHOSTS system ID
        /// </summary>
        /// <param name="ct">Cancellation Token</param>
        /// <returns>A client's particular unique GHOSTS system ID (GUID)</returns>
        [SwaggerOperation("ClientIdGet")]
        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            if (!Request.Headers.TryGetValue("ghosts-id", out var id))
            {
                id = string.Empty;
            }

            _log.Info($"Request by {id}");

            var findMachineResponse = await _service.FindOrCreate(HttpContext, ct);
            if (!findMachineResponse.IsValid())
            {
                _log.Error($"FindOrCreate failed for {id}: {findMachineResponse.Error}");
                return StatusCode(StatusCodes.Status401Unauthorized, findMachineResponse.Error);
            }

            var machineId = findMachineResponse.Machine.Id;

            //client saves this for future calls
            return Ok(machineId);
        }
    }
}
