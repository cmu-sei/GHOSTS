using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ghosts.api.Areas.Animator.Infrastructure.Models;
using Ghosts.Api.Infrastructure;
using NLog;

namespace ghosts.api.Areas.Animator.Infrastructure.ContentServices.Ollama;

public class OllamaFormatterService
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private readonly ApplicationSettings.AnimatorSettingsDetail.ContentEngineSettings _configuration;
    private OllamaConnectorService _ollamaConnectorService;

    public OllamaFormatterService(ApplicationSettings.AnimatorSettingsDetail.ContentEngineSettings configuration)
    {
        _configuration = configuration;
        _configuration.Host = Environment.GetEnvironmentVariable("OLLAMA_HOST") ??
                              configuration.Host;
        _configuration.Model = Environment.GetEnvironmentVariable("OLLAMA_MODEL") ??
                               configuration.Model;

        _ollamaConnectorService = new OllamaConnectorService(_configuration);
    }

    public async Task<string> GenerateTweet(NpcRecord npc)
    {
        var flattenedAgent = GenericContentHelpers.GetFlattenedNpc(npc);

        var prompt = await File.ReadAllTextAsync("config/ContentServices/Ollama/GenerateTweet.txt");
        var messages = new StringBuilder();
        foreach (var p in prompt.Split(System.Environment.NewLine))
        {
            var s = p.Replace("[[flattenedAgent]]", flattenedAgent[..3050]);
            messages.Append(s);    
        }
        
        return await _ollamaConnectorService.ExecuteQuery(messages.ToString());
    }

    public async Task<string> GenerateNextAction(NpcRecord npc, string history)
    {
        const string promptPath = "config/ContentServices/Ollama/GenerateNextAction.txt";
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
        
        return await _ollamaConnectorService.ExecuteQuery(messages.ToString());
    }
}