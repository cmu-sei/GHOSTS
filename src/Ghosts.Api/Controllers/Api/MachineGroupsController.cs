// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using ghosts.api.Infrastructure.Models;
using ghosts.api.Infrastructure.Services;
using Ghosts.Api.Infrastructure.Extensions;
using Microsoft.AspNetCore.Mvc;
using NLog;
using Swashbuckle.AspNetCore.Annotations;

namespace ghosts.api.Controllers.Api
{
    [Produces("application/json")]
    [Route("api/[controller]")]
    [ResponseCache(Duration = 5)]
    public class MachineGroupsController(IMachineGroupService service, IMachineService machineService) : Controller
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private readonly IMachineGroupService _service = service;
        private readonly IMachineService _serviceMachine = machineService;

        /// <summary>
        /// Gets the group information and the machines contained therein based on the provided query
        /// </summary>
        /// <param name="q">Query</param>
        /// <param name="ct">Cancellation Token</param>
        /// <returns>Group information</returns>
        [SwaggerOperation("MachineGroupsGetByQuery")]
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
        [SwaggerOperation("MachineGroupsGetById")]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetMachineGroup([FromRoute] int id, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state");
                return BadRequest(ModelState);
            }

            var machineGroup = await _service.GetAsync(id, ct);
            if (machineGroup == null)
            {
                _log.Info($"Group with id {id} not found");
                return NotFound();
            }

            return Ok(machineGroup);
        }

        /// <summary>
        /// Updates a group's information
        /// </summary>
        /// <param name="id"></param>
        /// <param name="model">The group to update</param>
        /// <param name="ct">Cancellation Token</param>
        /// <returns>The updated group</returns>
        [SwaggerOperation("MachineGroupsUpdate")]
        [HttpPut("{id}")]
        public async Task<IActionResult> PutMachineGroup(string id, [FromBody] Group model, CancellationToken ct)
        {
            if (!ModelState.IsValid || model.ContainsInvalidUnicode())
            {
                _log.Warn("Invalid model state");
                return BadRequest(ModelState);
            }

            // if trying to update something that doesn't exist, create it instead
            if (await _service.GetAsync(model.Id, ct) == null)
            {
                var createId = await _service.CreateAsync(model, ct);
                return CreatedAtAction(nameof(GetMachineGroup), new { createId }, model);
            }

            await _service.UpdateAsync(model, ct);
            _log.Info($"Group with id {model.Id} updated");

            return Ok(model);
        }

        /// <summary>
        /// Adds a single machine to a machine group
        /// </summary>
        /// <param name="machineId"></param>
        /// <param name="ct">Cancellation Token</param>
        /// <param name="groupId"></param>
        /// <returns>The updated group</returns>
        [SwaggerOperation("MachineGroupsAddMachine")]
        [HttpPost("{groupId:int}/{machineId:guid}")]
        public async Task<IActionResult> AddMachineToGroup([FromRoute] int groupId, [FromRoute] Guid machineId, CancellationToken ct)
        {
            return Ok(await _service.AddMachineToGroup(groupId, machineId, ct));
        }

        /// <summary>
        /// Removes a single machine from a machine group
        /// </summary>
        /// <param name="machineId"></param>
        /// <param name="ct">Cancellation Token</param>
        /// <param name="groupId"></param>
        /// <returns>The updated group</returns>
        [SwaggerOperation("MachineGroupsRemoveMachine")]
        [HttpDelete("{groupId:int}/{machineId:guid}")]
        public async Task<IActionResult> RemoveMachineFromGroup([FromRoute] int groupId, [FromRoute] Guid machineId, CancellationToken ct)
        {
            return Ok(await _service.RemoveMachineFromGroup(groupId, machineId, ct));
        }

        /// <summary>
        /// Create new group
        /// </summary>
        /// <param name="model">The new group to add</param>
        /// <param name="ct">Cancellation Token</param>
        /// <returns>The saved group</returns>
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [SwaggerResponse((int)HttpStatusCode.OK)]
        [SwaggerOperation("MachineGroupsCreate")]
        [HttpPost]
        public async Task<IActionResult> PostMachineGroup([FromBody] Group model, CancellationToken ct)
        {
            if (!ModelState.IsValid
                || model.ContainsInvalidUnicode()
                || model.Status == StatusType.Deleted
                || model.GroupMachines == null)
            {
                _log.Warn("Invalid model state");
                return BadRequest(ModelState);
            }

            // does group exist?
            if (await _service.GetAsync(model.Id, ct) != null)
                return CreatedAtAction(nameof(GetMachineGroup), new { model.Id }, model);

            var id = await _service.CreateAsync(model, ct);
            _log.Info($"Group with id {id} created");

            return CreatedAtAction(nameof(GetMachineGroup), new { id }, model);
        }

        /// <summary>
        /// Deletes a specific group
        /// </summary>
        /// <param name="id">The group to delete</param>
        /// <param name="ct">Cancellation Token</param>
        /// <returns>204 No Content</returns>
        [SwaggerOperation("MachineGroupsDeleteById")]
        [HttpDelete("{id}")]
        [ResponseCache(Duration = 0)]
        public async Task<IActionResult> DeleteMachineGroup([FromRoute] int id, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state");
                return BadRequest(ModelState);
            }

            await _service.DeleteAsync(id, ct);
            _log.Info($"Group with id {id} deleted");

            return NoContent();
        }

        /// <summary>
        /// Gets the activity for a group of machines
        /// </summary>
        /// <param name="id">Group ID</param>
        /// <param name="skip">How many records to skip for pagination</param>
        /// <param name="take">How many records to return</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>The activity for the group</returns>
        [SwaggerOperation("MachineGroupsGetActivityById")]
        [HttpGet("{id}/activity")]
        public async Task<IActionResult> Activity([FromRoute] int id, int skip, int take, CancellationToken ct)
        {
            try
            {
                var response = await _service.GetActivity(id, skip, take, ct);
                return Ok(response);
            }
            catch (Exception exc)
            {
                _log.Error(exc, $"Error getting activity for group {id}");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Endpoint returns health records for all of the machines in a group
        /// </summary>
        /// <param name="id">Group Id</param>
        /// <param name="ct">Cancellation Token</param>
        /// <returns>Health records for machines in the group</returns>
        [SwaggerOperation("MachineGroupsGetHealthById")]
        [HttpGet("{id}/health")]
        public async Task<IActionResult> GetGroup([FromRoute] int id, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state");
                return BadRequest(ModelState);
            }

            var group = await _service.GetAsync(id, ct);
            if (group == null)
            {
                _log.Info($"Group with id {id} not found");
                return NotFound();
            }

            var list = new List<Machine.MachineHistoryItem>();
            foreach (var machine in group.GroupMachines)
            {
                list.AddRange(await _serviceMachine.GetMachineHistory(machine.MachineId, ct));
            }

            _log.Info($"Health records retrieved for group {id}");

            return Ok(list.OrderByDescending(o => o.CreatedUtc));
        }
    }
}
