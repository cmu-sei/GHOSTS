// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Models;
using Ghosts.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ghosts.Api.Controllers
{
    [Produces("application/json")]
    [Route("api/group-health")]
    [ResponseCache(Duration = 5)]
    public class GroupHealthController : Controller
    {
        private readonly IMachineGroupService _service;
        private readonly IMachineService _serviceMachine;

        public GroupHealthController(IMachineGroupService service, IMachineService serviceMachine)
        {
            _service = service;
            _serviceMachine = serviceMachine;
        }

        /// <summary>
        /// Endpoint returns health records for all of the machines in a group
        /// </summary>
        /// <param name="id">Group Id</param>
        /// <param name="ct">Cancellation Token</param>
        /// <returns>Health records for machines in the group</returns>
        [HttpGet("{id}")]
        public async Task<IActionResult> Index([FromRoute] int id, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var list = new List<Machine.MachineHistoryItem>();

            var group = await _service.GetAsync(id, ct);

            foreach (var machine in group.GroupMachines) list.AddRange(await _serviceMachine.GetMachineHistory(machine.MachineId, ct));

            return Ok(list.OrderByDescending(o => o.CreatedUtc));
        }
    }
}