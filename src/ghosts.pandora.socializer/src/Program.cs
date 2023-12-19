using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Socializer.Hubs;
using Socializer.Infrastructure;

namespace Socializer;

class Program
{
    public static ApplicationConfiguration Configuration { get; private set; }
    
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddDbContext<DataContext>();

        builder.Services.AddRazorPages().AddRazorRuntimeCompilation();
        builder.Services.AddControllersWithViews();
        builder.Services.AddSignalR();
        
        var logger = LoggerFactory.Create(config =>
        {
            config.AddConsole();
        }).CreateLogger("Program");
        builder.Services.AddSingleton(typeof(ILogger), logger);

        var app = builder.Build();

        //app.UseHttpsRedirection();
        app.UseAuthorization();
        app.UseStaticFiles();
        
        app.UseRouting();
        app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
        
        app.MapHub<PostsHub>("/hubs/posts");

        Configuration = ApplicationConfigurationLoader.Load();
        
        logger.LogInformation(ApplicationDetails.Header);
        logger.LogInformation("GHOSTS SOCIALIZER {Version} ({VersionFile}) coming online...", ApplicationDetails.Version, ApplicationDetails.VersionFile);
        
        app.Run();
    }
}