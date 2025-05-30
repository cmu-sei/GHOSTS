// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Services.ClientServices;
using Ghosts.Domain.Messages.MesssagesForServer;
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
    public class ClientSurveyController(IClientSurveyService surveyService) : Controller
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Clients post an encrypted survey results to this endpoint
        /// </summary>
        /// <param name="transmission">The encrypted survey result</param>
        /// <param name="ct">Cancellation Token</param>
        /// <returns>204 No Content on success</returns>
        [SwaggerOperation("ClientSurveyCreateSecure")]
        [HttpPost("secure")]
        public async Task<IActionResult> Index([FromBody] Survey transmission, CancellationToken ct)
        {
            var ok = await surveyService.ProcessSurveyAsync(HttpContext, transmission, ct);
            return ok ? NoContent() : Unauthorized("Invalid survey request");
        }

        /// <summary>
        /// Clients post survey results to this endpoint
        /// </summary>
        /// <param name="value">The client survey result</param>
        /// <param name="ct">Cancellation Token</param>
        /// <returns>204 No Content on success</returns>
        [SwaggerOperation("ClientSurveyCreate")]
        [HttpPost]
        public async Task<IActionResult> Secure([FromBody] EncryptedPayload value, CancellationToken ct)
        {
            var ok = await surveyService.ProcessEncryptedSurveyAsync(HttpContext, value, ct);
            return ok ? NoContent() : BadRequest("Malformed or unauthorized encrypted survey");
        }
    }
}
