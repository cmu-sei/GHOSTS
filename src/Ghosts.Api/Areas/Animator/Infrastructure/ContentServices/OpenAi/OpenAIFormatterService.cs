using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ghosts.api.Areas.Animator.Infrastructure.Models;
using NLog;
using OpenAI.ObjectModels.RequestModels;

namespace ghosts.api.Areas.Animator.Infrastructure.ContentServices.OpenAi;

public class OpenAiFormatterService
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private readonly OpenAiConnectorService _openAiConnectorService;
    
    public bool IsReady { get; set; }
    
    public OpenAiFormatterService()
    {
        _openAiConnectorService = new OpenAiConnectorService();
        this.IsReady = _openAiConnectorService.IsReady;
    }

    public async Task<string> GenerateTweet(NpcRecord npc)
    {
        var flattenedAgent = GenericContentHelpers.GetFlattenedNpc(npc);

        var messages = new List<ChatMessage>();

        var prompt = await File.ReadAllTextAsync("config/ContentServices/OpenAi/GenerateTweet.txt");

        foreach (var p in prompt.Split(System.Environment.NewLine))
        {
            var s = p.Replace("[[flattenedAgent]]", flattenedAgent[..3050]);
            messages.Add(ChatMessage.FromSystem(s));
        }
        
        return await _openAiConnectorService.ExecuteQuery(messages);
    }

    public async Task<string> GenerateNextAction(NpcRecord npc, string history)
    {
        var flattenedAgent = GenericContentHelpers.GetFlattenedNpc(npc);

        _log.Trace($"{npc.NpcProfile.Name} with {history.Length} history records");

        var messages = new List<ChatMessage>();
        
        var prompt = await File.ReadAllTextAsync("config/ContentServices/OpenAi/GenerateNextAction.txt");
        foreach (var p in prompt.Split(System.Environment.NewLine))
        {
            var s = p.Replace("[[flattenedAgent]]", flattenedAgent[..3050]);
            s = s.Replace("[[history]]", history);
            messages.Add(ChatMessage.FromSystem(s));    
        }

        return await _openAiConnectorService.ExecuteQuery(messages);
    }
    
    public async Task<string> GeneratePowershellScript(NpcRecord npc)
    {
        var flattenedAgent = GenericContentHelpers.GetFlattenedNpc(npc);

        var messages = new List<ChatMessage>
        {
            ChatMessage.FromSystem($"Given this json information about a person: ```{flattenedAgent[..3050]}```"),
            ChatMessage.FromSystem("Generate a relevant powershell script for this person to execute on their windows computer")
        };

        return await _openAiConnectorService.ExecuteQuery(messages);
    }

    public async Task<string> GenerateCommand(NpcRecord npc)
    {
        var flattenedAgent = GenericContentHelpers.GetFlattenedNpc(npc);

        var messages = new List<ChatMessage>
        {
            ChatMessage.FromSystem($"Given this json information about a person: ```{flattenedAgent[..3050]}```"),
            ChatMessage.FromSystem("Generate a relevant command-line command for this person to execute on their windows computer")
        };

        return await _openAiConnectorService.ExecuteQuery(messages);
    }
    
    //public async Task<string> GenerateDocumentContent(NPC npc)
    //public async Task<string> GenerateExcelContent(NPC npc)
    //public async Task<string> GeneratePowerPointContent(NPC npc)
    //public async Task<string> GenerateEmail(NPC npc)
}