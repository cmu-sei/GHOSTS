using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ghosts.Animator;
using Ghosts.Animator.Models;
using ghosts.api.Areas.Animator.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using NLog;

namespace ghosts.api.Areas.Animator.Infrastructure.Services;

public interface INpcService
{
    public Task<IEnumerable<NpcRecord>> GetAll();
    public Task<IEnumerable<NpcRecord>> GetEnclave(string campaign, string enclave);
    public Task<IEnumerable<NpcNameId>> GetListAsync();
    public Task<IEnumerable<NpcRecord>> GetTeam(string campaign, string enclave, string team);
    public Task<NpcRecord> GetById(Guid id);
    public Task<NpcRecord> Create(NpcProfile npcProfile, bool generate);
    public Task DeleteById(Guid id);
}

public class NpcService : INpcService
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private readonly ApplicationDbContext _context;

    public NpcService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<NpcRecord>> GetAll()
    {
        return await this._context.Npcs.ToListAsync();
    }
    
    public async Task<IEnumerable<NpcRecord>> GetEnclave(string campaign, string enclave)
    {
        return await _context.Npcs.Where(x => x.Campaign == campaign && x.Enclave == enclave).ToListAsync();
    }

    public async Task<IEnumerable<NpcNameId>> GetListAsync()
    {
        return await this._context.Npcs
            .Select(item => new NpcNameId
            {
                Id = item.Id,
                Name = $"{item.NpcProfile.Name.First} {item.NpcProfile.Name.Last}"
            })
            .ToListAsync();
    }
    
    public async Task<IEnumerable<NpcNameId>> GetListAsync(string campaign)
    {
        return await this._context.Npcs
            .Where(x=>x.Campaign == campaign)
            .Select(item => new NpcNameId
            {
                Id = item.Id,
                Name = $"{item.NpcProfile.Name.First} {item.NpcProfile.Name.Last}"
            })
            .ToListAsync();
    }

    public async Task<IEnumerable<NpcRecord>> GetTeam(string campaign, string enclave, string team)
    {
        return await this._context.Npcs.Where(x => x.Campaign == campaign && x.Enclave == enclave && x.Team == team).ToListAsync();
    }

    public async Task<NpcRecord> GetById(Guid id)
    {
        return await this._context.Npcs.FirstOrDefaultAsync(x => x.Id == id);
    }

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
    
    public async Task DeleteById(Guid id)
    {
        var o = await this._context.Npcs.FindAsync(id);
        if (o != null)
        {
            this._context.Npcs.Remove(o);
            await this._context.SaveChangesAsync();
        }
    }
}