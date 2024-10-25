// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading;
using System.Threading.Tasks;
using ghosts.api.Infrastructure;
using ghosts.api.Infrastructure.Models;
using ghosts.api.Infrastructure.Services;
using Ghosts.Api.ViewModels;
using Ghosts.Domain;
using Microsoft.AspNetCore.Mvc;
using NLog;
using Swashbuckle.AspNetCore.Annotations;
using Swashbuckle.AspNetCore.Filters;

namespace ghosts.api.Controllers.Api
{
    /// <summary>
    /// Enter a machine command, so that the next time a machine checks in,
    /// it executes the indicated MachineUpdate.Type command
    /// </summary>
    [Produces("application/json")]
    [Route("api/[controller]")]
    public class MachineUpdatesController(IMachineUpdateService updateService) : Controller
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private readonly IMachineUpdateService _updateService = updateService;

        /// <summary>
        /// Sends a command for machine to perform,
        /// e.g. health or timeline updates, or post back current timeline
        /// </summary>
        /// <returns>The saved MachineUpdate record</returns>
        [ProducesResponseType(200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(404)]
        [SwaggerRequestExample(typeof(MachineUpdate), typeof(MachineUpdateExample))]
        [SwaggerOperation("MachineUpdatesCreate")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] MachineUpdate machineUpdate, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state");
                return BadRequest(ModelState);
            }

            try
            {
                var result = await _updateService.CreateAsync(machineUpdate, ct);
                _log.Info($"Machine update created with ID {result.Id}");
                return Ok(result);
            }
            catch (Exception e)
            {
                _log.Error(e, "Error creating machine update");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Send a new timeline to an entire group of machines
        /// </summary>
        /// <param name="groupId">Group Id</param>
        /// <param name="machineUpdate">The update to send</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>204 No content</returns>
        [SwaggerOperation("MachineUpdateGroupUpdate")]
        [HttpPost("group/{groupId}")]
        public async Task<IActionResult> GroupUpdate([FromRoute] int groupId, [FromBody] MachineUpdateViewModel machineUpdate, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state");
                return BadRequest(ModelState);
            }

            try
            {
                await _updateService.UpdateGroupAsync(groupId, machineUpdate, ct);
                _log.Info($"Group update sent to group ID {groupId}");
                return NoContent();
            }
            catch (Exception e)
            {
                _log.Error(e, $"Error updating group ID {groupId}");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Gets a machine update by its ID
        /// </summary>
        /// <param name="updateId">Update ID</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>The machine update</returns>
        [SwaggerOperation("MachineUpdatesGetById")]
        [HttpGet("{updateId}")]
        public async Task<IActionResult> GetById([FromRoute] int updateId, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state");
                return BadRequest(ModelState);
            }

            var update = await _updateService.GetById(updateId, ct);
            if (update == null)
            {
                _log.Info($"Machine update with ID {updateId} not found");
                return NotFound();
            }

            return Ok(update);
        }

        /// <summary>
        /// Gets machine updates by machine ID
        /// </summary>
        /// <param name="machineId">Machine ID</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>List of machine updates</returns>
        [SwaggerOperation("MachineUpdatesGetByMachineId")]
        [HttpGet("machine/{machineId}")]
        public async Task<IActionResult> GetByMachineId([FromRoute] Guid machineId, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state");
                return BadRequest(ModelState);
            }

            var updates = await _updateService.GetByMachineId(machineId, ct);
            return Ok(updates);
        }

        /// <summary>
        /// Gets machine updates by type
        /// </summary>
        /// <param name="type">Update type</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>List of machine updates</returns>
        [SwaggerOperation("MachineUpdatesGetByType")]
        [HttpGet("type/{type}")]
        public async Task<IActionResult> GetByType([FromRoute] UpdateClientConfig.UpdateType type, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state");
                return BadRequest(ModelState);
            }

            var updates = await _updateService.GetByType(type, ct);
            return Ok(updates);
        }

        /// <summary>
        /// Gets machine updates by status
        /// </summary>
        /// <param name="status">Update status</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>List of machine updates</returns>
        [SwaggerOperation("MachineUpdatesGetByStatus")]
        [HttpGet("status/{status}")]
        public async Task<IActionResult> GetByStatus([FromRoute] StatusType status, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state");
                return BadRequest(ModelState);
            }

            var updates = await _updateService.GetByStatus(status, ct);
            return Ok(updates);
        }
    }
}
