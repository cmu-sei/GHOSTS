using Ghosts.Socializer.Hubs;
using Ghosts.Socializer.Infrastructure;
using Ghosts.Socializer.Infrastructure.Middleware;
using Ghosts.Socializer.Infrastructure.Services;
using Ghosts.Socializer.Infrastructure.Startup.Extensions;

var builder = WebApplication.CreateBuilder(args);

var configuration = ApplicationConfigurationLoader.Load();

// Override configuration from environment variables
var modeTypeEnv = Environment.GetEnvironmentVariable("MODE_TYPE");
if (!string.IsNullOrEmpty(modeTypeEnv))
{
    configuration.Mode.Type = modeTypeEnv;
}

var defaultThemeEnv = Environment.GetEnvironmentVariable("DEFAULT_THEME");
if (!string.IsNullOrEmpty(defaultThemeEnv))
{
    configuration.Mode.DefaultTheme = defaultThemeEnv;
}

var siteTypeEnv = Environment.GetEnvironmentVariable("SITE_TYPE");
if (!string.IsNullOrEmpty(siteTypeEnv))
{
    configuration.Mode.SiteType = siteTypeEnv;
}

var siteNameEnv = Environment.GetEnvironmentVariable("SITE_NAME");
if (!string.IsNullOrEmpty(siteNameEnv))
{
    configuration.Mode.SiteName = siteNameEnv;
}

var articleCountEnv = Environment.GetEnvironmentVariable("ARTICLE_COUNT");
if (!string.IsNullOrEmpty(articleCountEnv) && int.TryParse(articleCountEnv, out var articleCount))
{
    configuration.Mode.ArticleCount = articleCount;
}

builder.Services.AddSingleton(configuration);

builder.Services.AddDbContext<DataContext>();

builder.Services
    .AddControllersWithViews()
    .AddRazorRuntimeCompilation()
    .AddRazorOptions(opts =>
    {
        opts.ViewLocationExpanders.Add(new ThemeViewLocationExpander());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwagger();

builder.Services.AddSignalR();
builder.Services.AddHostedService<CleanupService>();

builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IThemeService, ThemeService>();
builder.Services.AddScoped<IPostService, PostService>();
builder.Services.AddScoped<IDirectMessageService, DirectMessageService>();
builder.Services.AddScoped<IFollowService, FollowService>();

// Pandora content generation services
builder.Services.AddScoped<IPdfGenerationService, PdfGenerationService>();
builder.Services.AddScoped<IOfficeDocumentGenerationService, OfficeDocumentGenerationService>();
builder.Services.AddScoped<IImageGenerationService, ImageGenerationService>();
builder.Services.AddScoped<IDataFormatGenerationService, DataFormatGenerationService>();
builder.Services.AddScoped<IArchiveGenerationService, ArchiveGenerationService>();
builder.Services.AddScoped<IVideoGenerationService, VideoGenerationService>();
builder.Services.AddScoped<IAudioGenerationService, AudioGenerationService>();
builder.Services.AddScoped<IBinaryGenerationService, BinaryGenerationService>();
builder.Services.AddScoped<IWebsiteGenerationService, WebsiteGenerationService>();
builder.Services.AddScoped<ContentManager>();

builder.Services.AddSingleton(_ =>
    LoggerFactory
        .Create(config => config.AddConsole())
        .CreateLogger("Program"));

var app = builder.Build();

app.UseStaticFiles();

app.UseSwagger();
app.UseSwaggerUI();

// Pandora file extension middleware - must be before routing
app.UsePandoraFileExtensionMiddleware();

app.UseRouting();
app.UseAuthorization();

// Map Pandora routes with higher priority (order: 0)
app.MapControllers();

// Map default route with lower priority (order: 1)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<PostsHub>("/hubs/posts");

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation(ApplicationDetails.Header);
logger.LogInformation(
    "GHOSTS PANDORA {Version} ({VersionFile}) coming online...",
    ApplicationDetails.Version, ApplicationDetails.VersionFile
);
logger.LogInformation("This server is configured for '{Mode}' and to use the '{Theme}' theme", configuration.Mode.Type, configuration.Mode.DefaultTheme);

app.Run();
