// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Services.ClientServices;
using Ghosts.Domain;
using Ghosts.Domain.Messages.MesssagesForServer;
using Microsoft.AspNetCore.Mvc;
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
    public class ClientResultsController(IClientResultsService clientResultsService) : Controller
    {
        /// <summary>
        /// Clients post an encrypted timeline or health payload to this endpoint
        /// </summary>
        /// <param name="transmission">The encrypted timeline or health log payload</param>
        /// <param name="ct">Cancellation Token</param>
        /// <returns>204 No Content on success</returns>
        [SwaggerOperation("ClientResultsCreateSecure")]
        [HttpPost("secure")]
        public async Task<IActionResult> Secure([FromBody] EncryptedPayload transmission, CancellationToken ct)
        {
            var success = await clientResultsService.ProcessEncryptedAsync(HttpContext, transmission, ct);
            return success ? NoContent() : Unauthorized("Invalid machine or payload");
        }

        /// <summary>
        /// Clients post a timeline or health payload to this endpoint
        /// </summary>
        /// <param name="value">Timeline or health log payload</param>
        /// <param name="ct">Cancellation Token</param>
        /// <returns>204 No Content on success</returns>
        [SwaggerOperation("ClientResultsCreate")]
        [HttpPost]
        public async Task<IActionResult> Index([FromBody] TransferLogDump value, CancellationToken ct)
        {
            var success = await clientResultsService.ProcessResultAsync(HttpContext, value, ct);
            return success ? NoContent() : Unauthorized("Invalid machine or payload");
        }
    }
}
