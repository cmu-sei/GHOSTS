// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure;
using Ghosts.Api.Models;
using Ghosts.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NLog;

namespace Ghosts.Api.Controllers
{
    /// <summary>
    /// GHOSTS CLIENT CONTROLLER
    /// These endpoints are typically only used by GHOSTS Clients installed and configured to use the GHOSTS C2
    /// </summary>
    [Produces("application/json")]
    [Route("api/[controller]")]
    public class ClientIdController : Controller
    {
        private static readonly Logger log = LogManager.GetCurrentClassLogger();
        private readonly IMachineService _service;

        public ClientIdController(IMachineService service)
        {
            _service = service;
        }

        /// <summary>
        /// Clients use this endpoint to get their unique GHOSTS system ID
        /// </summary>
        /// <param name="ct">Cancellation Token</param>
        /// <returns>A client's particular unique GHOSTS system ID (GUID)</returns>
        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            var id = Request.Headers["ghosts-id"];
            log.Trace($"Request by {id}");

            var m = new Machine();
            if (!string.IsNullOrEmpty(id)) m = await _service.GetByIdAsync(new Guid(id), ct);

            if (m == null || !m.IsValid()) m = await _service.FindByValue(WebRequestReader.GetMachine(HttpContext), ct);

            if (m == null || !m.IsValid())
            {
                m = WebRequestReader.GetMachine(HttpContext);

                m.History.Add(new Machine.MachineHistoryItem {Type = Machine.MachineHistoryItem.HistoryType.Created});
                await _service.CreateAsync(m, ct);
            }

            if (!m.IsValid()) return StatusCode(StatusCodes.Status401Unauthorized, "Invalid machine request");

            m.History.Add(new Machine.MachineHistoryItem {Type = Machine.MachineHistoryItem.HistoryType.RequestedId});
            await _service.UpdateAsync(m, ct);

            //client saves this for future calls
            return Json(m.Id);
        }
    }
}