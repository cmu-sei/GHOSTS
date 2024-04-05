using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ghosts.api.Areas.Animator.Infrastructure.Models;
using Ghosts.Api.Infrastructure;
using NLog;

namespace ghosts.api.Areas.Animator.Infrastructure.ContentServices.Shadows;

public class ShadowsFormatterService
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private readonly ApplicationSettings.AnimatorSettingsDetail.ContentEngineSettings _configuration;
    private ShadowsConnectorService _connectorService;

    public ShadowsFormatterService(ApplicationSettings.AnimatorSettingsDetail.ContentEngineSettings configuration)
    {
        _configuration = configuration;
        _configuration.Host = Environment.GetEnvironmentVariable("GHOSTS_SHADOWS_HOST") ??
                              configuration.Host;
        _configuration.Model = Environment.GetEnvironmentVariable("GHOSTS_SHADOWS_MODEL") ??
                               configuration.Model;

        _connectorService = new ShadowsConnectorService(_configuration);
    }

    public async Task<string> GenerateTweet(NpcRecord npc)
    {
        var flattenedAgent = GenericContentHelpers.GetFlattenedNpc(npc);

        var prompt = await File.ReadAllTextAsync("config/ContentServices/Shadows/GenerateTweet.txt");
        var messages = new StringBuilder();
        foreach (var p in prompt.Split(System.Environment.NewLine))
        {
            var s = p.Replace("[[flattenedAgent]]", flattenedAgent[..3050]);
            messages.Append(s);    
        }
        
        return await _connectorService.ExecuteQuery(messages.ToString());
    }

    public async Task<string> GenerateNextAction(NpcRecord npc, string history)
    {
        const string promptPath = "config/ContentServices/Shadows/GenerateNextAction.txt";
        var flattenedAgent = GenericContentHelpers.GetFlattenedNpc(npc);
        if (flattenedAgent.Length > 3050)
        {
            flattenedAgent = flattenedAgent[..3050];
        }

        _log.Trace($"{npc.NpcProfile.Name} with {history.Length} history records. Loading prompt from: {promptPath}");

        var prompt = await File.ReadAllTextAsync(promptPath);
        var messages = new StringBuilder();
        foreach (var p in prompt.Split(System.Environment.NewLine))
        {
            var s = p.Replace("[[flattenedAgent]]", flattenedAgent[..3050]);
            s = s.Replace("[[history]]", history);
            messages.Append(s).Append(' ');
        }

        // _log.Trace(messages.ToString());
        
        return await _connectorService.ExecuteQuery(messages.ToString());
    }
}