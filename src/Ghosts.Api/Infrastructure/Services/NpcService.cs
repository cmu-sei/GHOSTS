using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Animator;
using Ghosts.Animator.Models;
using Ghosts.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using NLog;

namespace Ghosts.Api.Infrastructure.Services;

public interface INpcService
{
    public Task<IEnumerable<NpcRecord>> GetAll();
    public Task<IEnumerable<NpcRecord>> GetEnclave(string campaign, string enclave);
    public Task<IEnumerable<NpcNameId>> GetListAsync();
    public Task SaveListAsync(Guid id, string username, string originUrl);
    public Task<IEnumerable<NpcRecord>> GetTeam(string campaign, string enclave, string team);
    public Task<NpcRecord> GetById(Guid id);
    public Task<IEnumerable<NpcActivity>> GetActivity(Guid id);
    public Task<NpcActivity> CreateActivity(Guid id, string activityType, string detail);
    public Task<IEnumerable<NpcPreference>> GetPreferences(Guid id);
    public Task<NpcPreference> CreatePreference(Guid id, Guid toNpcId, Guid fromNpcId, string name, long step, decimal weight, decimal strength);
    public Task<IEnumerable<NpcSocialConnection>> GetConnections(Guid id);
    public Task<NpcSocialConnection> CreateConnection(Guid id, Guid connectedNpcId, string name, string distance, int relationshipStatus);
    public Task<IEnumerable<NpcRecord>> Create(GenerationConfiguration config, CancellationToken ct);
    public Task<NpcRecord> CreateOne();
    Task<NpcRecord> CreateOne(NpcProfile npc);
    public Task DeleteById(Guid id);
    public Task<IEnumerable<string>> GetKeys(string key);
    public Task SyncWithMachineUsernames();
    public Task<IEnumerable<NpcRecord>> GetByScenarioId(int scenarioId);
}

public class NpcService(ApplicationDbContext context) : INpcService
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private readonly ApplicationDbContext _context = context;

    public async Task<IEnumerable<NpcRecord>> GetAll()
    {
        return await _context.Npcs
            .Include(n => n.Connections)
            .Include(n => n.Knowledge)
            .Include(n => n.Beliefs)
            .Include(n => n.Preferences)
            .ToListAsync();
    }

    public async Task<IEnumerable<NpcRecord>> GetEnclave(string campaign, string enclave)
    {
        return await _context.Npcs
            .Include(n => n.Connections)
            .Include(n => n.Knowledge)
            .Include(n => n.Beliefs)
            .Include(n => n.Preferences)
            .Where(x => x.Campaign == campaign && x.Enclave == enclave)
            .ToListAsync();
    }

    public async Task<IEnumerable<NpcNameId>> GetListAsync()
    {
        return await _context.Npcs
            .Select(item => new NpcNameId
            {
                Id = item.Id,
                Name = $"{item.NpcProfile.Name.First} {item.NpcProfile.Name.Last}"
            })
            .ToListAsync();
    }

    public async Task SaveListAsync(Guid id, string username, string originUrl)
    {
        var npc = NpcRecord.TransformToNpc(Npc.Generate(MilitaryUnits.GetServiceBranch()));
        npc.Id = id;
        npc.NpcProfile.Accounts = new List<AccountsProfile.Account> { new() { Username = username, Url = originUrl } };
        npc.CreatedUtc = DateTime.UtcNow;
        _context.Npcs.Add(npc);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<NpcNameId>> GetListAsync(string campaign)
    {
        return await _context.Npcs
            .Where(x => x.Campaign == campaign)
            .Select(item => new NpcNameId
            {
                Id = item.Id,
                Name = $"{item.NpcProfile.Name.First} {item.NpcProfile.Name.Last}"
            })
            .ToListAsync();
    }

    public async Task<IEnumerable<NpcRecord>> GetTeam(string campaign, string enclave, string team)
    {
        return await _context.Npcs
            .Include(n => n.Connections)
            .Include(n => n.Knowledge)
            .Include(n => n.Beliefs)
            .Include(n => n.Preferences)
            .Where(x => x.Campaign == campaign && x.Enclave == enclave && x.Team == team)
            .ToListAsync();
    }

    public async Task<NpcRecord> GetById(Guid id)
    {
        return await _context.Npcs
            .Include(n => n.Connections)
            .Include(n => n.Knowledge)
            .Include(n => n.Beliefs)
            .Include(n => n.Preferences)
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<IEnumerable<NpcActivity>> GetActivity(Guid id)
    {
        return await _context.NpcActivities
            .Where(x => x.NpcId == id)
            .OrderByDescending(x => x.CreatedUtc)
            .ToListAsync();
    }

    public async Task<NpcActivity> CreateActivity(Guid id, string activityType, string detail)
    {
        var npcActivity = new NpcActivity
        {
            NpcId = id,
            Detail = detail,
            CreatedUtc = DateTime.UtcNow
        };
        if (Enum.TryParse<NpcActivity.ActivityTypes>(activityType, true, out var value))
        {
            npcActivity.ActivityType = value;
        }

        _context.NpcActivities.Add(npcActivity);
        await _context.SaveChangesAsync();

        return npcActivity;
    }

    public async Task<IEnumerable<NpcPreference>> GetPreferences(Guid id)
    {
        return await _context.NpcPreferences
            .Where(x => x.NpcId == id)
            .OrderByDescending(x => x.CreatedUtc)
            .ToListAsync();
    }

    public async Task<NpcPreference> CreatePreference(Guid id, Guid toNpcId, Guid fromNpcId, string name, long step, decimal weight, decimal strength)
    {
        var npcPreference = new NpcPreference
        {
            NpcId = id,
            ToNpcId = toNpcId,
            FromNpcId = fromNpcId,
            Name = name,
            Step = step,
            Weight = weight,
            Strength = strength,
            CreatedUtc = DateTime.UtcNow
        };

        _context.NpcPreferences.Add(npcPreference);
        await _context.SaveChangesAsync();

        return npcPreference;
    }

    public async Task<IEnumerable<NpcSocialConnection>> GetConnections(Guid id)
    {
        return await _context.NpcSocialConnections
            .Where(x => x.NpcId == id)
            .OrderByDescending(x => x.UpdatedUtc)
            .ToListAsync();
    }

    public async Task<NpcSocialConnection> CreateConnection(Guid id, Guid connectedNpcId, string name, string distance, int relationshipStatus)
    {
        var npcConnection = new NpcSocialConnection
        {
            Id = Guid.NewGuid().ToString(),
            NpcId = id,
            ConnectedNpcId = connectedNpcId,
            Name = name,
            Distance = distance,
            RelationshipStatus = relationshipStatus,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };

        _context.NpcSocialConnections.Add(npcConnection);
        await _context.SaveChangesAsync();

        return npcConnection;
    }

    public async Task<NpcRecord> CreateOne()
    {
        var npc = NpcRecord.TransformToNpc(Npc.Generate(MilitaryUnits.GetServiceBranch()));
        npc.Id = npc.NpcProfile.Id;
        npc.CreatedUtc = DateTime.UtcNow;
        _context.Npcs.Add(npc);
        await _context.SaveChangesAsync();
        return npc;
    }

    public async Task<NpcRecord> CreateOne(NpcProfile npcProfile)
    {
        var npc = new NpcRecord
        {
            NpcProfile = npcProfile,
            CreatedUtc = DateTime.UtcNow
        };
        npc.Id = npc.NpcProfile.Id;

        // Check if ScenarioId is provided in Attributes dictionary
        if (npcProfile.Attributes != null &&
            npcProfile.Attributes.TryGetValue("ScenarioId", out var scenarioIdStr) &&
            int.TryParse(scenarioIdStr, out var scenarioId))
        {
            npc.ScenarioId = scenarioId;
        }

        _context.Npcs.Add(npc);
        await _context.SaveChangesAsync();
        return npc;
    }

    public async Task<IEnumerable<NpcRecord>> Create(GenerationConfiguration config, CancellationToken ct)
    {
        var t = new Stopwatch();
        t.Start();

        var createdNpcs = new List<NpcRecord>();
        foreach (var enclave in config.Enclaves)
        {
            if (enclave.Teams == null) continue;
            foreach (var team in enclave.Teams)
            {
                if (team.Npcs == null) continue;
                if (team.Npcs.Number > 25)
                {
                    _log.Warn("Cannot generate more than 25 NPCs at a time, sorry.");
                    team.Npcs.Number = 25;
                }
                for (var i = 0; i < team.Npcs.Number; i++)
                {
                    var last = t.ElapsedMilliseconds;
                    var branch = team.Npcs.Configuration?.Branch ?? MilitaryUnits.GetServiceBranch();
                    var npc = NpcRecord.TransformToNpc(Npc.Generate(new NpcGenerationConfiguration
                    { Branch = branch, PreferenceSettings = team.PreferenceSettings }));
                    npc.Id = npc.NpcProfile.Id;
                    npc.CreatedUtc = DateTime.UtcNow;
                    npc.Team = team.Name;
                    npc.Campaign = config.Campaign;
                    npc.Enclave = enclave.Name;
                    npc.ScenarioId = config.ScenarioId;

                    _context.Npcs.Add(npc);
                    createdNpcs.Add(npc);
                    _log.Trace($"{i} generated in {t.ElapsedMilliseconds - last} ms");
                }
            }
        }

        await _context.SaveChangesAsync(ct);

        t.Stop();
        _log.Trace($"{createdNpcs.Count} NPCs generated in {t.ElapsedMilliseconds} ms");

        return createdNpcs;
    }

    public async Task<IEnumerable<string>> GetKeys(string key)
    {
        if (key == null)
            return new List<string>();

        return key.ToLower() switch
        {
            "campaign" => await _context.Npcs.Where(x => x.Campaign != null).Select(x => x.Campaign).Distinct().ToListAsync(),
            "enclave" => await _context.Npcs.Where(x => x.Enclave != null).Select(x => x.Enclave).Distinct().ToListAsync(),
            "team" => await _context.Npcs.Where(x => x.Team != null).Select(x => x.Team).Distinct().ToListAsync(),
            _ => null
        };
    }

    public async Task DeleteById(Guid id)
    {
        var o = await _context.Npcs.FindAsync(id);
        if (o != null)
        {
            _context.Npcs.Remove(o);
            await _context.SaveChangesAsync();
        }
    }

    public async Task SyncWithMachineUsernames()
    {
        var machines = _context.Machines.ToList();
        var npcs = _context.Npcs.ToArray();

        foreach (var machine in machines)
        {
            if (npcs.Any(x => x.MachineId == machine.Id))
                continue;
            if (npcs.Any(x => string.Equals(x.NpcProfile.Name.ToString()?.Replace(" ", "."),
                    machine.CurrentUsername, StringComparison.InvariantCultureIgnoreCase)))
                continue;

            var npc = NpcRecord.TransformToNpc(Npc.Generate(MilitaryUnits.GetServiceBranch(), machine.CurrentUsername));

            //todo: need to be sure user is aligned with the machine currentusername

            npc.Id = npc.NpcProfile.Id;
            npc.CreatedUtc = DateTime.UtcNow;
            npc.MachineId = machine.Id;
            _context.Npcs.Add(npc);
            _log.Trace($"NPC created for {machine.CurrentUsername}...");
        }

        await _context.SaveChangesAsync();
        _log.Trace("NPCs created for each username in machines");
    }

    public async Task<IEnumerable<NpcRecord>> GetByScenarioId(int scenarioId)
    {
        return await _context.Npcs
            .Include(n => n.Connections)
            .Include(n => n.Knowledge)
            .Include(n => n.Beliefs)
            .Include(n => n.Preferences)
            .Where(x => x.ScenarioId == scenarioId)
            .ToListAsync();
    }
}
