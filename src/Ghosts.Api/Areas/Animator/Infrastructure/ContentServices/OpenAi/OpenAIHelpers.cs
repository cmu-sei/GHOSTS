using System;

namespace ghosts.api.Areas.Animator.Infrastructure.ContentServices.OpenAi;

public static class OpenAiHelpers
{
    /// <summary>
    /// You'll need to supply your openAi api key via an environment variable
    /// </summary>
    public static string GetApiKey()
    {
        return Environment.GetEnvironmentVariable("OPEN_AI_API_KEY");
    }
}