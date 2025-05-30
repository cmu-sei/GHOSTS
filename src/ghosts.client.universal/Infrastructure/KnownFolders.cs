using System;

namespace Ghosts.Client.Universal.Infrastructure;

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
