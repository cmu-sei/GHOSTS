using Ghosts.Socializer.Hubs;
using Ghosts.Socializer.Infrastructure;
using Ghosts.Socializer.Infrastructure.Services;
using Ghosts.Socializer.Services;

var builder = WebApplication.CreateBuilder(args);

var configuration = ApplicationConfigurationLoader.Load();
builder.Services.AddSingleton(configuration);

builder.Services.AddDbContext<DataContext>();

builder.Services
    .AddControllersWithViews()
    .AddRazorRuntimeCompilation()
    .AddRazorOptions(opts =>
    {
        opts.ViewLocationExpanders.Add(new ThemeViewLocationExpander());
    });

builder.Services.AddSignalR();
builder.Services.AddHostedService<CleanupService>();

builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IThemeService, ThemeService>();
builder.Services.AddScoped<IPostService, PostService>();
builder.Services.AddScoped<IDirectMessageService, DirectMessageService>();

builder.Services.AddSingleton(_ =>
    LoggerFactory
        .Create(config => config.AddConsole())
        .CreateLogger("Program"));

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<PostsHub>("/hubs/posts");

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation(ApplicationDetails.Header);
logger.LogInformation(
    "GHOSTS SOCIALIZER {Version} ({VersionFile}) coming online...",
    ApplicationDetails.Version, ApplicationDetails.VersionFile
);

app.Run();
