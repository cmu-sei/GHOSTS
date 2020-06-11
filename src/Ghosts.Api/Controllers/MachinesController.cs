// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Models;
using Ghosts.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ghosts.Api.Controllers
{
    //[Authorize(Policy = "ApiUser")]
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

        // GET: api/Machines
        [HttpGet]
        public async Task<IActionResult> GetMachines(string q, CancellationToken ct)
        {
            var list = await _service.GetAsync(q, ct);
            if (list == null) return NotFound();
            return Ok(list);
        }

        [HttpGet]
        [Route("list")]
        public IActionResult GetList(CancellationToken ct)
        {
            return Ok(_service.GetListAsync(ct));
        }

        // GET: api/Machines/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetMachine([FromRoute] Guid id, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var machine = await _service.GetByIdAsync(id, ct);

            if (machine.Id == Guid.Empty) return NotFound();

            return Ok(machine);
        }

        // PUT: api/Machines/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutMachine([FromBody] Machine machine, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (machine.Id == Guid.Empty) return BadRequest();

            await _service.UpdateAsync(machine, ct);
            return NoContent();
        }

        // POST: api/Machines
        [HttpPost]
        public async Task<IActionResult> PostMachine([FromBody] Machine machine, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var id = await _service.CreateAsync(machine, ct);

            return CreatedAtAction("GetMachine", new {id}, machine);
        }

        // DELETE: api/Machines/5
        [HttpDelete("{id}")]
        [ResponseCache(Duration = 0)]
        public async Task<IActionResult> DeleteMachine([FromRoute] Guid id, CancellationToken ct)
        {
            if (!ModelState.IsValid || id == Guid.Empty) return BadRequest(ModelState);

            await _service.DeleteAsync(id, ct);
            return NoContent();
        }

        [AllowAnonymous]
        [HttpPost("{id}/command")]
        public async Task<IActionResult> Command([FromRoute] Guid id, string command, CancellationToken ct)
        {
            if (!ModelState.IsValid || id == Guid.Empty) return BadRequest(ModelState);

            try
            {
                var response = _service.SendCommand(id, command, ct).Result;
                return Ok(response);
            }
            catch (Exception exc)
            {
                return Json(exc);
            }
        }

        [AllowAnonymous]
        [HttpGet("{id}/activity")]
        public async Task<IActionResult> Activity([FromRoute] Guid id, CancellationToken ct)
        {
            if (!ModelState.IsValid || id == Guid.Empty) return BadRequest(ModelState);

            try
            {
                var response = await _service.GetActivity(id, ct);
                return Ok(response);
            }
            catch (Exception exc)
            {
                return Json(exc);
            }
        }
    }
}