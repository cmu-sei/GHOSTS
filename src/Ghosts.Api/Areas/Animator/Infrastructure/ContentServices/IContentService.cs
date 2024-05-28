using System.Threading.Tasks;

namespace ghosts.api.Areas.Animator.Infrastructure.ContentServices;

public interface IContentService
{
    Task<string> ExecuteQuery(string prompt);
}