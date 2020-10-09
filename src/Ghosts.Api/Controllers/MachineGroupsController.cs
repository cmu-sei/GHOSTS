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
using Microsoft.AspNetCore.Mvc;
using NLog;

namespace Ghosts.Api.Controllers
{
    [Produces("application/json")]
    [Route("api/machinegroups")]
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

        /// <summary>
        /// Gets the group information and the machines contained therein based on the provided query
        /// </summary>
        /// <param name="q">Query</param>
        /// <param name="ct">Cancellation Token</param>
        /// <returns>Group information</returns>
        [HttpGet]
        public async Task<IEnumerable<Group>> GetMachineGroup(string q, CancellationToken ct)
        {
            return await _service.GetAsync(q, ct);
        }

        /// <summary>
        /// Gets the group information and the machines contained therein based on a specific group Id
        /// </summary>
        /// <param name="id">Group Id</param>
        /// <param name="ct">Cancellation Token</param>
        /// <returns>Group information</returns>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetMachineGroup([FromRoute] int id, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var machineGroup = await _service.GetAsync(id, ct);

            if (machineGroup == null) return NotFound();
            return Ok(machineGroup);
        }

        /// <summary>
        /// Updates a group's information
        /// </summary>
        /// <param name="model">The group to update</param>
        /// <param name="ct">Cancellation Token</param>
        /// <returns>The updated group</returns>
        [HttpPut("{id}")]
        public async Task<IActionResult> PutMachineGroup([FromBody] Group model, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            await _service.UpdateAsync(model, ct);

            return Ok(model);
        }

        /// <summary>
        /// Create new group
        /// </summary>
        /// <param name="model">The new group to add</param>
        /// <param name="ct">Cancellation Token</param>
        /// <returns>The saved group</returns>
        [HttpPost]
        public async Task<IActionResult> PostMachineGroup([FromBody] Group model, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var id = await _service.CreateAsync(model, ct);

            return CreatedAtAction("GetMachineGroup", new {id}, model);
        }

        /// <summary>
        /// Deletes a specific group
        /// </summary>
        /// <param name="id">The group to delete</param>
        /// <param name="ct">Cancellation Token</param>
        /// <returns>204 No Content</returns>
        [HttpDelete("{id}")]
        [ResponseCache(Duration = 0)]
        public async Task<IActionResult> DeleteMachineGroup([FromRoute] int id, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            await _service.DeleteAsync(id, ct);

            return NoContent();
        }

        /// <summary>
        /// Send a specific command to a group of machines
        /// </summary>
        /// <param name="id">Group ID</param>
        /// <param name="command">The command to execute</param>
        /// <param name="ct">Cancellation Token</param>
        /// <returns>The results of running the command on each machine</returns>
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

        /// <summary>
        /// Gets teh activity for a group of machines
        /// </summary>
        /// <param name="id">Group ID</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>The activity for the group</returns>
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