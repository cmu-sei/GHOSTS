using System;
using System.IO;

namespace Ghosts.Api.Infrastructure;

public static class N8nConfig
{
    private static readonly string[] KeyFilePaths =
    [
        Path.Combine(AppContext.BaseDirectory, "n8n_data", ".api_key"),
        Path.Combine(Directory.GetCurrentDirectory(), "n8n_data", ".api_key"),
        "/home/node/.n8n/.api_key"
    ];

    public static string GetApiKey()
    {
        var key = Environment.GetEnvironmentVariable("N8N_API_KEY");
        if (!string.IsNullOrWhiteSpace(key))
            return key;

        var explicitFile = Environment.GetEnvironmentVariable("N8N_API_KEY_FILE");
        if (!string.IsNullOrWhiteSpace(explicitFile) && File.Exists(explicitFile))
        {
            key = File.ReadAllText(explicitFile).Trim();
            if (!string.IsNullOrWhiteSpace(key))
            {
                Environment.SetEnvironmentVariable("N8N_API_KEY", key);
                return key;
            }
        }

        foreach (var path in KeyFilePaths)
        {
            if (!File.Exists(path)) continue;
            key = File.ReadAllText(path).Trim();
            if (!string.IsNullOrWhiteSpace(key))
            {
                Environment.SetEnvironmentVariable("N8N_API_KEY", key);
                return key;
            }
        }

        return null;
    }

    public static string GetApiUrl()
    {
        return Environment.GetEnvironmentVariable("N8N_API_URL");
    }
}
