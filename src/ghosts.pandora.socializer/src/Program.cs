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
        logger.LogInformation(
            "GHOSTS SOCIALIZER {Version} ({VersionFile}) coming online...",
            ApplicationDetails.Version, ApplicationDetails.VersionFile
        );

        app.Run();
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<DataContext>();

        services
            .AddControllersWithViews()
            .AddRazorRuntimeCompilation()
            .AddRazorOptions(opts =>
            {
                opts.ViewLocationExpanders.Add(new ThemeViewLocationExpander());
            });

        services.AddSignalR();
        services.AddHostedService<CleanupService>();

        services.AddSingleton<ILogger>(provider =>
            LoggerFactory
                .Create(config => config.AddConsole())
                .CreateLogger("Program"));
    }

    private static void ConfigureMiddleware(WebApplication app)
    {
        //app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            // MVC route
            endpoints.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            // SignalR
            endpoints.MapHub<PostsHub>("/hubs/posts");
        });
    }
}
