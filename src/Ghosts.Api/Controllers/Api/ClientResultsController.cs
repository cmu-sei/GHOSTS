// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading;
using ghosts.api.Infrastructure.Models;
using ghosts.api.Infrastructure.Services;
using Ghosts.Api.Infrastructure;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Ghosts.Domain.Messages.MesssagesForServer;
using Microsoft.AspNetCore.Http;
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
    public class ClientResultsController(IBackgroundQueue service) : Controller
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private readonly IBackgroundQueue _service = service;

        /// <summary>
        /// Clients post an encrypted timeline or health payload to this endpoint
        /// </summary>
        /// <param name="transmission">The encrypted timeline or health log payload</param>
        /// <param name="ct">Cancellation Token</param>
        /// <returns>204 No Content on success</returns>
        [SwaggerOperation("ClientResultsCreateSecure")]
        [HttpPost("secure")]
        public IActionResult Secure([FromBody] EncryptedPayload transmission, CancellationToken ct)
        {
            try
            {
                if (!Request.Headers.TryGetValue("ghosts-name", out var key))
                {
                    _log.Warn("Request missing ghosts-name header");
                    return BadRequest("Missing ghosts-name header");
                }

                // Decrypt
                transmission.Payload = Base64Encoder.Base64Decode(transmission.Payload);
                var raw = Crypto.DecryptStringAes(transmission.Payload, key);

                // Deserialize
                var value = JsonConvert.DeserializeObject<TransferLogDump>(raw);

                return Process(HttpContext, Request, value, ct);
            }
            catch (Exception exc)
            {
                _log.Error(exc, "Malformed data");
                return BadRequest("Malformed data");
            }
        }

        /// <summary>
        /// Clients post a timeline or health payload to this endpoint
        /// </summary>
        /// <param name="value">Timeline or health log payload</param>
        /// <param name="ct">Cancellation Token</param>
        /// <returns>204 No Content on success</returns>
        [SwaggerOperation("ClientResultsCreate")]
        [HttpPost]
        public IActionResult Index([FromBody] TransferLogDump value, CancellationToken ct)
        {
            return Process(HttpContext, Request, value, ct);
        }

        private IActionResult Process(HttpContext context, HttpRequest request, TransferLogDump value, CancellationToken ct)
        {
            if (!Request.Headers.TryGetValue("ghosts-id", out var id))
            {
                _log.Warn("Request missing ghosts-id header");
                return Unauthorized("Missing ghosts-id header");
            }

            var m = WebRequestReader.GetMachine(context);

            if (!string.IsNullOrEmpty(id))
            {
                m.Id = new Guid(id);
            }
            else if (!m.IsValid())
            {
                return Unauthorized("Invalid machine request");
            }

            if (value is not null && !string.IsNullOrEmpty(value.Log))
            {
                _log.Info($"Payload received: {value.Log}");

                _service.Enqueue(new QueueEntry
                {
                    Payload = new MachineQueueEntry
                    {
                        Machine = m,
                        LogDump = value,
                        HistoryType = Machine.MachineHistoryItem.HistoryType.PostedResults
                    },
                    Type = QueueEntry.Types.Machine
                });
            }

            return NoContent();
        }
    }
}
