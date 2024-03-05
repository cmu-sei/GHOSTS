// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Ghosts.Animator;
using Ghosts.Animator.Models;
using ghosts.api.Areas.Animator.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace ghosts.api.Areas.Animator.Controllers.Api;

[ApiController]
[Produces("application/json")]
[Route("api/[controller]")]
public class NpcsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public NpcsController(ApplicationDbContext context)
    {
        _context = context;
    }
        
    /// <summary>
    /// Returns all generated NPCs in the system (caution, could return a large amount of data)
    /// </summary>
    /// <returns>IEnumerable&lt;NpcProfile&gt;</returns>
    [ProducesResponseType(typeof(IEnumerable<NpcRecord>), (int) HttpStatusCode.OK)]
    [SwaggerResponse((int) HttpStatusCode.OK, Type = typeof(IEnumerable<NpcRecord>))]
    [SwaggerOperation("getNPCs")]
    [HttpGet]
    public IEnumerable<NpcRecord> Get()
    {
        return this._context.Npcs.ToList();
    }
        
    /// <summary>
    /// Returns name and Id for all NPCs in the system (caution, could return a large amount of data)
    /// </summary>
    /// <returns>IEnumerable&lt;NpcNameId&gt;</returns>
    [ProducesResponseType(typeof(IEnumerable<NpcNameId>), (int) HttpStatusCode.OK)]
    [SwaggerResponse((int) HttpStatusCode.OK, Type = typeof(IEnumerable<NpcNameId>))]
    [SwaggerOperation("getNPCList")]
    [HttpGet("list")]
    public IEnumerable<NpcNameId> List()
    {
        return this._context.Npcs.Select(item => new NpcNameId() { Id = item.Id, Name = item.NpcProfile.Name.ToString() }).ToList();
    }
        
    /// <summary>
    /// Get NPC by specific Id
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [ProducesResponseType(typeof(NpcRecord), (int) HttpStatusCode.OK)]
    [SwaggerResponse((int) HttpStatusCode.OK, Type = typeof(NpcRecord))]
    [SwaggerOperation("getNPCById")]
    [HttpGet("{id:guid}")]
    public NpcRecord GetById(Guid id)
    {
        return this._context.Npcs.FirstOrDefault(x => x.Id == id);
    }
        
    /// <summary>
    /// Delete NPC by specific Id
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [ProducesResponseType((int) HttpStatusCode.OK)]
    [SwaggerResponse((int) HttpStatusCode.OK)]
    [SwaggerOperation("deleteNPCById")]
    [HttpDelete("{id:guid}")]
    public async Task DeleteById(Guid id)
    {
        var o = await this._context.Npcs.FindAsync(id);
        this._context.Npcs.Remove(o);
        await this._context.SaveChangesAsync();
    }
        
    /// <summary>
    /// Get NPC photo by specific Id
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [ProducesResponseType(typeof(IActionResult), (int) HttpStatusCode.OK)]
    [SwaggerResponse((int) HttpStatusCode.OK, Type = typeof(IActionResult))]
    [SwaggerOperation("getNpcAvatarById")]
    [HttpGet("{id:guid}/photo")]
    public IActionResult GetPhotoById(Guid id)
    {
        //get npc and find image
        var npc = this._context.Npcs.FirstOrDefault(x => x.Id == id);
        if (npc == null) return NotFound();
        //load image as stream
        var stream = new FileStream(npc.NpcProfile.PhotoLink, FileMode.Open);
        return File(stream, "image/jpg", $"{npc.NpcProfile.Name.ToString().Replace(" ", "_")}.jpg");
    }
        
    /// <summary>
    /// Create one NPC (handy for syncing up from ghosts core api)
    /// </summary>
    /// <returns>NPC Profile</returns>
    [ProducesResponseType(typeof(NpcRecord), (int) HttpStatusCode.OK)]
    [SwaggerResponse((int) HttpStatusCode.OK, Type = typeof(NpcRecord))]
    [SwaggerOperation("createNpc")]
    [HttpPost]
    public async Task<NpcRecord> Create(NpcProfile npcProfile, bool generate)
    {
        NpcRecord npc;
        if (generate)
        {
            npc = NpcRecord.TransformToNpc(Npc.Generate(MilitaryUnits.GetServiceBranch()));
            npc.NpcProfile.Name = npcProfile.Name;
            npc.NpcProfile.Email = npcProfile.Email;
        }
        else
        {
            npc = NpcRecord.TransformToNpc(npcProfile);
        }
        
        npc.NpcProfile.Id = Guid.NewGuid();
        npc.NpcProfile.Created = DateTime.UtcNow;
        npc.Id = npc.NpcProfile.Id;
        
        this._context.Npcs.Add(npc);
        await this._context.SaveChangesAsync();
        return npc;
    }
        
    /// <summary>
    /// Get a subset of details about a specific NPC
    /// </summary>
    /// <param name="npcId"></param>
    /// <param name="fieldsToReturn"></param>
    /// <returns></returns>
    [HttpPost("npc/{npcId:guid}")]
    public object GetNpcReduced(Guid npcId, [FromBody] string[] fieldsToReturn)
    {
        var npc = this._context.Npcs.FirstOrDefault(x => x.Id == npcId);
        return new NPCReduced(fieldsToReturn, npc).PropertySelection;
    }
}