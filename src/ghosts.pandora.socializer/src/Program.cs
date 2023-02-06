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

        var app = builder.Build();

        //app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllerRoute(
                name: "images",
                pattern: "/images/{id?}",
                defaults: new { controller = "Files", action = "UploadFile" });
            
            endpoints.MapControllerRoute(
                name: "files",
                pattern: "/files/{id?}",
                defaults: new { controller = "Files", action = "UploadFile" });

            endpoints.MapControllerRoute(
                name: "other",
                pattern: "{*catchall}",
                defaults: new { controller = "Home", action = "Index" });
        });
        
        app.MapHub<PostsHub>("/hubs/posts");

        Configuration = ApplicationConfigurationLoader.Load();

        var logger = LoggerFactory.Create(config =>
        {
            config.AddConsole();
        }).CreateLogger("Program");

        logger.LogInformation(ApplicationDetails.Header);
        logger.LogInformation("GHOSTS SOCIALIZER {Version} ({VersionFile}) coming online...", ApplicationDetails.Version, ApplicationDetails.VersionFile);
        
        app.Run();
    }
}