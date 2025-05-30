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
    public class ClientUpdatesController(IClientUpdateService updateService) : Controller
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Clients use this endpoint to check for updates for them to download
        /// </summary>
        /// <param name="ct">Cancellation Token</param>
        /// <returns>404 if no update, or a json payload of a particular update</returns>
        /// <response code="200">Returns json payload of a particular update</response>
        /// <response code="401">Unauthorized or incorrectly formatted machine request</response>
        /// <response code="404">No Update</response>
        [ProducesResponseType(200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(404)]
        [SwaggerOperation("ClientUpdatesCreate")]
        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            var (success, update, code, error) = await updateService.GetUpdateAsync(HttpContext, ct);
            return code switch
            {
                StatusCodes.Status200OK => Json(update),
                StatusCodes.Status401Unauthorized => StatusCode(code, error),
                StatusCodes.Status404NotFound => NotFound(error),
                _ => BadRequest()
            };
        }
    }
}
