using System;

namespace Ghosts.Client.Infrastructure;

public static class KnownFolders
{
    public static string GetHomePath()
    {
        return Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%");
    }

    public static string GetDownloadFolderPath()
    {
        return GetHomePath() + "\\Downloads";
    }
}