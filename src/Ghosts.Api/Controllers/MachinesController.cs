// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Models;
using Ghosts.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ghosts.Api.Controllers
{
    [Produces("application/json")]
    [Route("api/[controller]")]
    [ResponseCache(Duration = 5)]
    public class MachinesController : Controller
    {
        private readonly IMachineService _service;

        public MachinesController(IMachineService service)
        {
            _service = service;
        }

        /// <summary>
        /// Gets machines matching the provided query value
        /// </summary>
        /// <param name="q">Query</param>
        /// <param name="ct">Cancellation Token</param>
        /// <returns>Machine information</returns>
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
        [HttpGet]
        [Route("list")]
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
        /// <param name="machine">The machine record to update</param>
        /// <param name="ct">Cancellation Token</param>
        /// <returns>The updated machine record</returns>
        [HttpPut("{id}")]
        public async Task<IActionResult> PutMachine([FromBody] Machine machine, CancellationToken ct)
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
        [HttpPost]
        public async Task<IActionResult> PostMachine([FromBody] Machine machine, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var id = await _service.CreateAsync(machine, ct);

            return CreatedAtAction("GetMachine", new {id}, machine);
        }

        /// <summary>
        /// Deletes a machine (warning: If the machine later checks in, the record will be re-created)
        /// </summary>
        /// <param name="id">The Id of the machine to delete</param>
        /// <param name="ct">Cancellation Token</param>
        /// <returns>204 No Content</returns>
        [HttpDelete("{id}")]
        [ResponseCache(Duration = 0)]
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
                return Json(exc);
            }
        }
    }
}