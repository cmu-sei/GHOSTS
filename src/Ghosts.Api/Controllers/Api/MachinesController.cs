// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading;
using System.Threading.Tasks;
using ghosts.api.Infrastructure.Models;
using ghosts.api.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using NLog;
using Swashbuckle.AspNetCore.Annotations;

namespace ghosts.api.Controllers.Api
{
    [Produces("application/json")]
    [Route("api/[controller]")]
    [ResponseCache(Duration = 5)]
    public class MachinesController(IMachineService service) : Controller
    {
        private readonly IMachineService _service = service;
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Gets machines matching the provided query value
        /// </summary>
        /// <param name="q">Query</param>
        /// <param name="ct">Cancellation Token</param>
        /// <returns>Machine information</returns>
        [SwaggerOperation("MachinesGetByQuery")]
        [HttpGet]
        public async Task<IActionResult> GetMachines(string q, CancellationToken ct)
        {
            var list = await _service.GetAsync(q, ct);
            if (list == null) return NotFound();
            return Ok(list);
        }

        /// <summary>
        /// Gets all machines in the system
        /// (warning: this may be a large amount of data based on the size of your range)
        /// </summary>
        /// <param name="ct">Cancellation Token</param>
        /// <returns>All machine records</returns>
        [SwaggerOperation("MachinesGet")]
        [HttpGet("list")]
        public IActionResult GetList(CancellationToken ct)
        {
            return Ok(_service.GetList());
        }

        /// <summary>
        /// Gets a specific machine by its Guid
        /// </summary>
        /// <param name="id">Machine Guid</param>
        /// <param name="ct">Cancellation Token</param>
        /// <returns>Machine record</returns>
        [SwaggerOperation("MachinesGetById")]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetMachine([FromRoute] Guid id, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var machine = await _service.GetByIdAsync(id, ct);

            if (machine.Id == Guid.Empty) return NotFound();

            return Ok(machine);
        }

        /// <summary>
        /// Updates a machine's information
        /// </summary>
        /// <param name="id"></param>
        /// <param name="machine">The machine record to update</param>
        /// <param name="ct">Cancellation Token</param>
        /// <returns>The updated machine record</returns>
        [SwaggerOperation("MachinesUpdate")]
        [HttpPut("{id}")]
        public async Task<IActionResult> PutMachine(string id, [FromBody] Machine machine, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (machine.Id == Guid.Empty) return BadRequest();

            await _service.UpdateAsync(machine, ct);
            return NoContent();
        }

        /// <summary>
        /// Create a machine on the range
        /// (warning: GHOSTS cannot control this machine unless its client later checks in with the same information created here)
        /// </summary>
        /// <param name="machine">The machine to create</param>
        /// <param name="ct">Cancellation Token</param>
        /// <returns></returns>
        [SwaggerOperation("MachinesCreate")]
        [HttpPost]
        public async Task<IActionResult> PostMachine([FromBody] Machine machine, CancellationToken ct)
        {
            if (!ModelState.IsValid || !machine.IsValid()) return BadRequest(ModelState);

            var id = await _service.CreateAsync(machine, ct);

            return CreatedAtAction(nameof(GetMachine), new { id }, machine);
        }

        /// <summary>
        /// Deletes a machine (warning: If the machine later checks in, the record will be re-created)
        /// </summary>
        /// <param name="id">The Id of the machine to delete</param>
        /// <param name="ct">Cancellation Token</param>
        /// <returns>204 No Content</returns>
        [ResponseCache(Duration = 0)]
        [SwaggerOperation("MachinesDeleteById")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMachine([FromRoute] Guid id, CancellationToken ct)
        {
            if (!ModelState.IsValid || id == Guid.Empty) return BadRequest(ModelState);

            await _service.DeleteAsync(id, ct);
            return NoContent();
        }

        /// <summary>
        /// Lists the activity for a given machine
        /// </summary>
        /// <param name="id">The machine to get activity for</param>
        /// <param name="skip">How many records to skip for pagination</param>
        /// <param name="take">How many records to return</param>
        /// <param name="ct">Cancellation Token</param>
        /// <returns>The activity history for the requested machine</returns>
        [SwaggerOperation("MachinesGetActivityById")]
        [HttpGet("{id}/activity")]
        public async Task<IActionResult> Activity([FromRoute] Guid id, int skip, int take, CancellationToken ct)
        {
            if (!ModelState.IsValid || id == Guid.Empty) return BadRequest(ModelState);

            try
            {
                var response = await _service.GetActivity(id, skip, take, ct);
                return Ok(response);
            }
            catch (Exception exc)
            {
                // Log the exception and return a 500 status code
                _log.Error(exc, $"An error occurred while fetching activity for machine {id}");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Gets the health for a particular machine
        /// </summary>
        /// <param name="id">Machine Guid</param>
        /// <param name="ct">Cancellation Token</param>
        /// <returns>Health records for the machine</returns>
        [SwaggerOperation("MachinesGetHealthById")]
        [HttpGet("{id}/health")]
        public async Task<IActionResult> Health([FromRoute] Guid id, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var list = await _service.GetMachineHistoryHealth(id, ct);

            return Ok(list);
        }
    }
}
