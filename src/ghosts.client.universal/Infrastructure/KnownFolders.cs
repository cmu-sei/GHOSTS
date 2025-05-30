using System;

namespace ghosts.client.universal.Infrastructure;

public static class KnownFolders
{
    public static string GetHomePath()
    {
        return Environment.ExpandEnvironmentVariables("%HOME%");
    }

    public static string GetDownloadFolderPath()
    {
        return GetHomePath() + "/Downloads";
    }
}
