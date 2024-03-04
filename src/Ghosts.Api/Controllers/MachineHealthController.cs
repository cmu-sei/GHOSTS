// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading;
using System.Threading.Tasks;
using ghosts.api.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ghosts.Api.Controllers
{
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

        /// <summary>
        /// Gets the health for a particular machine
        /// </summary>
        /// <param name="id">Machine Guid</param>
        /// <param name="ct">Cancellation Token</param>
        /// <returns>Health records for the machine</returns>
        [HttpGet("{id}")]
        public async Task<IActionResult> Index([FromRoute] Guid id, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var list = await _service.GetMachineHistoryHealth(id, ct);

            return Ok(list);
        }
    }
}