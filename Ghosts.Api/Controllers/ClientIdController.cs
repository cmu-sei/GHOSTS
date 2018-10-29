// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Code;
using Ghosts.Api.Models;
using Ghosts.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NLog;

namespace Ghosts.Api.Controllers
{
    [Produces("application/json")]
    [Route("api/[controller]")]
    public class ClientIdController : Controller
    {
        private static Logger log = LogManager.GetCurrentClassLogger();
        private readonly IMachineService _service;

        public ClientIdController(IMachineService service)
        {
            _service = service;
        }

        /// <summary>
        /// Clients post to this endpoint to get their unique GHOSTS system ID
        /// </summary>
        /// <returns>A client's particular unique GHOSTS system ID</returns>
        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            var id = Request.Headers["id"];
            log.Trace($"Request by {id}");

            var m = new Machine();
            if (!string.IsNullOrEmpty(id))
            {
                m = await this._service.GetByIdAsync(new Guid(id), ct);
            }
            
            if (Program.ClientConfig.IsMatchingIdByName && (m == null || !m.IsValid()))
            {
                m = await this._service.FindByName(Request.Headers["name"], ct);
            }
            
            if (m == null || !m.IsValid())
            {
                m = new Machine
                {
                    Name = Request.Headers["name"],
                    FQDN = Request.Headers["fqdn"],
                    HostIp = Request.Headers["ip"],
                    CurrentUsername = Request.Headers["user"],
                    IPAddress = HttpContext.Connection.RemoteIpAddress.ToString(),
                    StatusUp = Machine.UpDownStatus.Up,
                    ClientVersion = Request.Headers["version"]
                };

                m.History.Add(new Machine.MachineHistoryItem { Type = Machine.MachineHistoryItem.HistoryType.Created });
                await this._service.CreateAsync(m, ct);
            }

            if (!m.IsValid())
            {
                return StatusCode(StatusCodes.Status401Unauthorized, "Invalid machine request");
            }

            m.History.Add(new Machine.MachineHistoryItem { Type = Machine.MachineHistoryItem.HistoryType.RequestedId });
            await this._service.UpdateAsync(m, ct);

            //client saves this for future calls
            return Json(m.Id);
        }
    }
}