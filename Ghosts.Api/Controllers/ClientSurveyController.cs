using System;
using System.IO;
using System.Text;
using System.Threading;
using Ghosts.Api.Infrastructure;
using Ghosts.Api.Models;
using Ghosts.Api.Services;
using Ghosts.Domain.Code;
using Ghosts.Domain.Messages.MesssagesForServer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using NLog;

namespace ghosts.api.Controllers
{
    [Produces("application/json")]
    [Route("api/[controller]")]
    public class ClientSurveyController : Controller
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private readonly IBackgroundQueue _service;

        public ClientSurveyController(IBackgroundQueue service)
        {
            _service = service;
        }

        /// <summary>
        /// Clients post an encrypted survey result to this endpoint
        /// </summary>
        /// <param name="transmission">The encrypted survey result</param>
        /// <param name="ct">Cancellation Token</param>
        /// <returns>204 No Content on success</returns>
        [HttpPost("secure")]
        public IActionResult Secure([FromBody] EncryptedPayload transmission, CancellationToken ct)
        {
            string raw;

            try
            {
                var key = Request.Headers["ghosts-name"].ToString();
                //decrypt - the headers must be the same as encrypted with the client
                transmission.Payload = Crypto.Base64Decode(transmission.Payload);
                raw = Crypto.DecryptStringAes(transmission.Payload, key);
            }
            catch (Exception exc)
            {
                _log.Trace(exc);
                throw new Exception("Malformed data");
            }

            //deserialize
            var value = JsonConvert.DeserializeObject<Survey>(raw);

            return Process(HttpContext, Request, value, ct);
        }

        /// <summary>
        /// Clients post survey result to this endpoint
        /// </summary>
        /// <param name="value">The client survey result</param>
        /// <param name="ct">Cancellation Token</param>
        /// <returns>204 No Content on success</returns>
        [HttpPost]
        public IActionResult Index([FromBody] Survey value, CancellationToken ct)
        {
            return Process(HttpContext, Request, value, ct);
        }

        private IActionResult Process(HttpContext context, HttpRequest request, Survey value, CancellationToken ct)
        {
            var id = request.Headers["ghosts-id"];

            _log.Trace($"Request by {id}");

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

            value.MachineId = m.Id;
            if (value.Created == DateTime.MinValue)
                value.Created = DateTime.UtcNow;

            this._service.Enqueue(new QueueEntry { Type = QueueEntry.Types.Survey, Payload = value });

            return NoContent();
        }
    }
}