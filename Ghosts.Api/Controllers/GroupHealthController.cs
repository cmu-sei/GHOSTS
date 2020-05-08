// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Models;
using Ghosts.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ghosts.Api.Controllers
{
    [Authorize(Policy = "ApiUser")]
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