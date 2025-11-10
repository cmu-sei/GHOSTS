// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ghosts.Api.Hubs;
using Ghosts.Api.Infrastructure;
using Ghosts.Api.Infrastructure.Animations;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Api.Infrastructure.Extensions;
using Ghosts.Api.Infrastructure.Filters;
using Ghosts.Api.Infrastructure.Formatters;
using Ghosts.Api.Infrastructure.Services;
using Ghosts.Api.Infrastructure.Services.ClientServices;
using Ghosts.Domain.Code;
using Ghosts.Domain.Code.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json.Converters;
using NLog;
using Npgsql;
using Swashbuckle.AspNetCore.Filters;

namespace Ghosts.Api;

public class Program
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    public const int ApiVersion = 10;

    public static ApplicationSettings ApplicationSettings { get; set; }

    public static void Main(string[] args)
    {
        Console.WriteLine(ApplicationDetails.Header);

        var msg = $"GHOSTS API {ApplicationDetails.Version} ({ApplicationDetails.VersionFile}) coming online...";
        Console.WriteLine(msg);
        _log.Info(msg);

        ApiDetails.LoadConfiguration();

        var builder = WebApplication.CreateBuilder(args);

        // Set Npgsql legacy timestamp behavior
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        // Configure DbContext
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(
                builder.Configuration.GetConnectionString("DefaultConnection"),
                npgsqlOptions =>
                {
                    npgsqlOptions.ConfigureDataSource(dataSourceBuilder =>
                    {
                        var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
                        {
                            NumberHandling = JsonNumberHandling.AllowReadingFromString
                        };
                        serializerOptions.Converters.Add(new JsonStringEnumConverter());
                        dataSourceBuilder.ConfigureJsonOptions(serializerOptions);
                    });
                    npgsqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                }
            );
        });
        NpgsqlConnection.GlobalTypeMapper.EnableDynamicJson();

        // Configure services
        builder.Services.TryAddTransient<IHttpContextAccessor, HttpContextAccessor>();
        builder.Services.AddCors(options => options.UseConfiguredCors(builder.Configuration.GetSection("CorsPolicy")));

        // Configure Swagger
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc($"v{ApiVersion}", new OpenApiInfo
            {
                Version = $"v{ApiVersion}",
                Title = "GHOSTS API",
                Description = $"GHOSTS API v{ApiVersion} - Assembly: {ApplicationDetails.Version}",
                Contact = new OpenApiContact
                {
                    Name = "Dustin Updyke",
                    Email = "ddupdyke [*at*] sei.cmu.edu",
                    Url = new Uri("https://sei.cmu.edu")
                },
                License = new OpenApiLicense
                {
                    Name =
                        "Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms"
                }
            });

            // Set the comments path for the Swagger JSON and UI.
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            c.DocumentFilter<CustomDocumentFilter>();
            c.IncludeXmlComments(xmlPath);
            c.ExampleFilters();
        });
        builder.Services.AddSwaggerGenNewtonsoftSupport();
        builder.Services.AddSwaggerExamplesFromAssemblies(Assembly.GetEntryAssembly());

        // Add application services
        builder.Services.AddScoped<IMachineService, MachineService>();
        builder.Services.AddScoped<IMachineGroupService, MachineGroupService>();
        builder.Services.AddScoped<IMachineUpdateService, MachineUpdateService>();
        builder.Services.AddScoped<ITimelineService, TimelineService>();
        builder.Services.AddScoped<IMachineTimelinesService, MachineTimelinesService>();
        builder.Services.AddScoped<ITrackableService, TrackableService>();
        builder.Services.AddScoped<ISurveyService, SurveyService>();
        builder.Services.AddScoped<INpcService, NpcService>();

        builder.Services.AddScoped<IClientResultsService, ClientResultsService>();
        builder.Services.AddScoped<IClientIdService, ClientIdService>();
        builder.Services.AddScoped<IClientSurveyService, ClientSurveyService>();
        builder.Services.AddScoped<IClientTimelineService, ClientTimelineService>();
        builder.Services.AddScoped<IClientUpdateService, ClientUpdateService>();
        builder.Services.AddScoped<IClientHubService, ClientHubService>();

        builder.Services.AddScoped<MachineUpdateExample>();
        builder.Services.AddSwaggerExamplesFromAssemblyOf<MachineUpdateExample>();

        // Background services
        builder.Services.AddSingleton<IBackgroundQueue, BackgroundQueue>();
        builder.Services.AddSingleton<IHostedService, QueueSyncService>();
        builder.Services.AddSingleton<IManageableHostedService, AnimationsManager>();

        // Configure controllers with JSON serialization and custom formatters
        builder.Services.AddControllers(options => { options.OutputFormatters.Add(new MarkdownOutputFormatter()); })
            .AddJsonOptions(options =>
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        builder.Services.AddMvc().AddNewtonsoftJson(options =>
        {
            options.SerializerSettings.Converters.Add(new TimeSpanConverter());
            options.SerializerSettings.Converters.Add(new StringEnumConverter());
            options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
        });

        // Add SignalR
        builder.Services.AddSignalR();

        // Add MVC with Razor runtime compilation
        builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();

        // Configure routing
        builder.Services.AddRouting(options => options.LowercaseUrls = true);

        var app = builder.Build();

        // Initialize database
        using (var scope = app.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            try
            {
                var context = services.GetRequiredService<ApplicationDbContext>();
                var dbInitializerLogger = services.GetRequiredService<ILogger<DbInitializer>>();

                DbInitializer.Initialize(context, dbInitializerLogger).Wait();
            }
            catch (Exception ex)
            {
                _log.Fatal(ex, "An error occurred while seeding the GHOSTS database");
            }
        }

        // Configure middleware pipeline
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseStaticFiles();
        app.UseRouting();
        app.UseCors("default");

        // Configure endpoints
        app.MapControllerRoute(
            name: "areas",
            pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}"
        );

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");

        app.MapHub<ClientHub>("/clientHub");
        app.MapHub<ClientHub>("/api/clientHub");
        app.MapHub<ActivityHub>("/hubs/activities");
        app.MapHub<ActivityHub>("/api/hubs/activities");

        // Configure Swagger
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint($"/swagger/v{ApiVersion}/swagger.json", $"GHOSTS API v{ApiVersion}");
        });

        app.Run();
    }
}
