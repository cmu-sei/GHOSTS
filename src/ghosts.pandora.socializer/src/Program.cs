using Socializer.Hubs;
using Socializer.Infrastructure;
using Socializer.Infrastructure.Services;

namespace Socializer;

class Program
{
    public static ApplicationConfiguration Configuration { get; private set; }

    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        ConfigureServices(builder.Services, builder.Configuration);

        var app = builder.Build();
        ConfigureMiddleware(app);

        Configuration = ApplicationConfigurationLoader.Load();
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation(ApplicationDetails.Header);
        logger.LogInformation("GHOSTS SOCIALIZER {Version} ({VersionFile}) coming online...",
            ApplicationDetails.Version, ApplicationDetails.VersionFile);
        app.Run();
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<DataContext>();
        services.AddRazorPages().AddRazorRuntimeCompilation();
        services.AddControllersWithViews();
        services.AddSignalR();

        services.AddSingleton<ILogger>(provider =>
            LoggerFactory.Create(config => config.AddConsole()).CreateLogger("Program"));

        services.AddHostedService<CleanupService>();

        // Optionally, add more service configurations here
    }

    private static void ConfigureMiddleware(WebApplication app)
    {
        //app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapHub<PostsHub>("/hubs/posts");
        });

        // Optionally, add more middleware configurations here
    }
}
