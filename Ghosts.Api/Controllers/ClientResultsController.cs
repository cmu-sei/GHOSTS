// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading;
using Ghosts.Api.Infrastructure;
using Ghosts.Api.Models;
using Ghosts.Api.Services;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Ghosts.Domain.Messages.MesssagesForServer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using NLog;

namespace Ghosts.Api.Controllers
{
    [Produces("application/json")]
    [Route("api/[controller]")]
    public class ClientResultsController : Controller
    {
        private static readonly Logger log = LogManager.GetCurrentClassLogger();
        private readonly IBackgroundQueue _service;

        public ClientResultsController(IBackgroundQueue service)
        {
            _service = service;
        }

        /// <summary>
        ///     Clients post an encrypted timeline or health payload to this endpoint
        /// </summary>
        /// <param name="transmission">The encrypted timeline or health log payload</param>
        /// <param name="ct">Cancellation Token</param>
        /// <returns>204 No Content on success</returns>
        [HttpPost("secure")]
        public IActionResult Secure([FromBody] EncryptedPayload transmission, CancellationToken ct)
        {
            string raw;

            try
            {
                var key = Request.Headers["ghosts-name"].ToString();
                //decrypt
                transmission.Payload = Crypto.Base64Decode(transmission.Payload);
                raw = Crypto.DecryptStringAes(transmission.Payload, key);
            }
            catch (Exception exc)
            {
                log.Trace(exc);
                throw new Exception("Malformed data");
            }

            //deserialize
            var value = JsonConvert.DeserializeObject<TransferLogDump>(raw);

            return Process(HttpContext, Request, value, ct);
        }

        /// <summary>
        ///     Clients post a timeline or health payload to this endpoint
        /// </summary>
        /// <param name="value">Timeline or health log payload</param>
        /// <param name="ct">Cancellation Token</param>
        /// <returns>204 No Content on success</returns>
        [HttpPost]
        public IActionResult Index([FromBody] TransferLogDump value, CancellationToken ct)
        {
            return Process(HttpContext, Request, value, ct);
        }

        // ReSharper disable once UnusedParameter.Local
        private IActionResult Process(HttpContext context, HttpRequest request, TransferLogDump value, CancellationToken ct)
        {
            var id = request.Headers["ghosts-id"];

            //log.Trace($"Request by {id}");

            var m = WebRequestReader.GetMachine(HttpContext);

            if (!string.IsNullOrEmpty(id))
            {
                m.Id = new Guid(id);
            }
            else
            {
                if (!m.IsValid())
                    return StatusCode(StatusCodes.Status401Unauthorized, "Invalid machine request");
            }

            if (!string.IsNullOrEmpty(value.Log))
            {
                log.Trace($"payload received: {value.Log}");

                _service.Enqueue(
                    new QueueEntry
                    {
                        Payload =
                            new MachineQueueEntry
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