// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Models;
using Ghosts.Api.Services;
using Ghosts.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NLog;

namespace Ghosts.Api.Controllers
{
    //[Authorize(Policy = "ApiUser")]
    [Produces("application/json")]
    [Route("api/MachineGroups")]
    [ResponseCache(Duration = 5)]
    public class MachineGroupsController : Controller
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private readonly IMachineGroupService _service;
        private readonly IMachineService _serviceMachine;

        public MachineGroupsController(IMachineGroupService service, IMachineService machineService)
        {
            _service = service;
            _serviceMachine = machineService;
        }

        // GET: api/MachineGroups
        [HttpGet]
        public async Task<IEnumerable<Group>> GetMachineGroup(string q, CancellationToken ct)
        {
            return await _service.GetAsync(q, ct);
        }

        // GET: api/MachineGroups/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetMachineGroup([FromRoute] int id, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var machineGroup = await _service.GetAsync(id, ct);

            if (machineGroup == null) return NotFound();
            return Ok(machineGroup);
        }

        // PUT: api/MachineGroups/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutMachineGroup([FromBody] Group model, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            await _service.UpdateAsync(model, ct);

            return Ok(model);
        }

        // POST: api/MachineGroups
        [HttpPost]
        public async Task<IActionResult> PostMachineGroup([FromBody] Group model, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var id = await _service.CreateAsync(model, ct);

            return CreatedAtAction("GetMachineGroup", new {id}, model);
        }

        // DELETE: api/MachineGroups/5
        [HttpDelete("{id}")]
        [ResponseCache(Duration = 0)]
        public async Task<IActionResult> DeleteMachineGroup([FromRoute] int id, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            await _service.DeleteAsync(id, ct);

            return NoContent();
        }

        [AllowAnonymous]
        [HttpPost("{id}/command")]
        public async Task<IActionResult> SendCommand([FromRoute] int id, string command, CancellationToken ct)
        {
            var handlers = new List<TimelineHandler>();
            var machines = await _service.GetAsync(id, ct);
            if (machines == null)
            {
                _log.Error($"Machine group not found: {id}");
                throw new InvalidOperationException("Machine group not found");
            }

            try
            {
                foreach (var machine in machines.GroupMachines)
                    try
                    {
                        var response = await _serviceMachine.SendCommand(machine.MachineId, command, ct);
                        handlers.Add(response);
                    }
                    catch (Exception e)
                    {
                        _log.Trace(e);
                    }

                return Ok(handlers);
            }
            catch (Exception e)
            {
                var response = new HttpResponseMessage(HttpStatusCode.InternalServerError) {Content = new StringContent(e.Message)};
                return BadRequest(response);
            }
        }

        [AllowAnonymous]
        [HttpGet("{id}/activity")]
        public async Task<IActionResult> Activity([FromRoute] int id, CancellationToken ct)
        {
            try
            {
                var response = await _service.GetActivity(id, ct);
                return Ok(response);
            }
            catch (Exception exc)
            {
                return Json(exc);
            }
        }
    }
}