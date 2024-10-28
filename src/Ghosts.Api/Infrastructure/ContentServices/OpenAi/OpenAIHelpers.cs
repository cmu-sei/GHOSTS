// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;

namespace ghosts.api.Infrastructure.ContentServices.OpenAi;

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
