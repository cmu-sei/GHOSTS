// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Threading;
using System.Threading.Tasks;
using ghosts.api.Infrastructure.Models;
using ghosts.api.Infrastructure.Services;
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
    }
}