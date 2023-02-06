namespace Socializer.Infrastructure;

public class ApplicationConfiguration
{
    public int DefaultDisplay { get; set; }
    public int MinutesToCheckForDuplicatePost { get; set; }
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