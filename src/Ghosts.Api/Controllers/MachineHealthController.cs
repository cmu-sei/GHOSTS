// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ghosts.Api.Controllers
{
    [Authorize(Policy = "ApiUser")]
    [Produces("application/json")]
    [Route("api/machine-health")]
    [ResponseCache(Duration = 5)]
    public class MachineHealthController : Controller
    {
        private readonly IMachineService _service;

        public MachineHealthController(IMachineService service)
        {
            _service = service;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Index([FromRoute] Guid id, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var list = await _service.GetMachineHistoryHealth(id, ct);

            return Ok(list);
        }
    }
}