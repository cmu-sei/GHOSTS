// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ghosts.api.Infrastructure.Models;
using ghosts.api.Infrastructure.Services;
using Ghosts.Api.ViewModels;
using Ghosts.Domain;
using Microsoft.AspNetCore.Mvc;
using NLog;

namespace Ghosts.Api.Controllers
{
    /// <summary>
    /// Enter a machine command, so that the next time a machine checks in,
    /// it executes the indicated MachineUpdate.Type command
    /// </summary>
    [Produces("application/json")]
    [Route("api/[controller]")]
    public class MachineUpdatesController : Controller
    {
        private static readonly Logger log = LogManager.GetCurrentClassLogger();
        private readonly IBackgroundQueue _queue;
        private readonly IMachineUpdateService _updateService;

        public MachineUpdatesController(IMachineUpdateService updateService, IBackgroundQueue queue)
        {
            _updateService = updateService;
            _queue = queue;
        }

        /// <summary>
        /// Sends a command for machine to perform,
        /// e.g. health or timeline updates, or post back current timeline
        /// </summary>
        /// <returns>The saved MachineUpdate record</returns>
        [HttpPost]
        [ProducesResponseType(200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Create([FromBody] MachineUpdate machineUpdate, CancellationToken ct)
        {
            var o = await _updateService.CreateAsync(machineUpdate, ct);
            return Ok(o);
        }
        
        /// <summary>
        /// Send a new timeline to an entire group of machines
        /// </summary>
        /// <param name="groupId">Group Id</param>
        /// <param name="machineUpdate">The update to send</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>204 No content</returns>
        [HttpPost("group/{groupId}")]
        public async Task<IActionResult> GroupUpdate([FromRoute] int groupId, [FromBody] MachineUpdateViewModel machineUpdate, CancellationToken ct)
        {
            await this._updateService.UpdateGroupAsync(groupId, machineUpdate, ct);
            return NoContent();
        }
        
        [HttpGet("{updateId}")]
        public async Task<MachineUpdate> GetById([FromRoute] int updateId, CancellationToken ct)
        {
            return await this._updateService.GetById(updateId, ct);
        }

        [HttpGet("machine/{machineId}")]
        public async Task<IEnumerable<MachineUpdate>> GetByMachineId([FromRoute] Guid machineId, CancellationToken ct)
        {
            return await this._updateService.GetByMachineId(machineId, ct);
        }
        
        [HttpGet("type/{type}")]
        public async Task<IEnumerable<MachineUpdate>> GetByType([FromRoute] UpdateClientConfig.UpdateType type, CancellationToken ct)
        {
            return await this._updateService.GetByType(type, ct);
        }
        
        [HttpGet("status/{status}")]
        public async Task<IEnumerable<MachineUpdate>> GetByStatus([FromRoute] StatusType status, CancellationToken ct)
        {
            return await this._updateService.GetByStatus(status, ct);
        }
    }
}