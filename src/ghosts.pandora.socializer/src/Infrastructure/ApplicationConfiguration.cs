namespace Socializer.Infrastructure;

public class ApplicationConfiguration
{
    public int DefaultDisplay { get; set; }
    public int MinutesToCheckForDuplicatePost { get; set; }

    public CleanupJobConfig CleanupJob { get; set; }
    public CleanupAgeConfig CleanupAge { get; set; }

    public int CleanupDiskUtilThreshold { get; set; }

    public class CleanupJobConfig
    {
        public int Hours { get; set; }
        public int Minutes { get; set; }
        public int Seconds { get; set; }
    }

    public class CleanupAgeConfig
    {
        public int Days { get; set; }
        public int Hours { get; set; }
        public int Minutes { get; set; }
    }
}

public static class ApplicationConfigurationLoader
{
    public static ApplicationConfiguration Load()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json");

        var config = builder.Build();
        var appConfig = new ApplicationConfiguration();
        config.GetSection("ApplicationConfiguration").Bind(appConfig);

        return appConfig;
    }
}
