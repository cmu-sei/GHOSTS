using System.Threading.Tasks;
using ghosts.api.Areas.Animator.Infrastructure.Models;

namespace ghosts.api.Areas.Animator.Infrastructure.ContentServices;

public interface IFormatterService
{
    Task<string> GenerateNextAction(NpcRecord npc, string history);
    Task<string> GenerateTweet(NpcRecord npc);
    
    Task<string> ExecuteQuery(string prompt);
}