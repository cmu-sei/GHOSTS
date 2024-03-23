// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.IO;
using System.Reflection;
using System.Text.Json.Serialization;
using ghosts.api.Areas.Animator.Hubs;
using ghosts.api.Areas.Animator.Infrastructure.Animations;
using Ghosts.Api.Hubs;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Api.Infrastructure.Extensions;
using ghosts.api.Infrastructure.Services;
using Ghosts.Api.ViewModels;
using Ghosts.Domain.Code;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Filters;

namespace Ghosts.Api
{
    public class Startup
    {
        public const int ApiVersion = 8;
        
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            //services.AddDbContext<ApplicationDbContext>(opt => opt.UseInMemoryDatabase("ghosts"));
            services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(Configuration.GetConnectionString("DefaultConnection")));
            
            services.TryAddTransient<IHttpContextAccessor, HttpContextAccessor>();
            services.AddCors(options => options.UseConfiguredCors(Configuration.GetSection("CorsPolicy")));

            services.AddSwaggerGen(c =>
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
                        Name = "Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms"
                    }
                });
                
                // Set the comments path for the Swagger JSON and UI.
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                c.IncludeXmlComments(xmlPath);
                c.ExampleFilters();
            });
            services.AddSwaggerExamplesFromAssemblies(Assembly.GetEntryAssembly());
            services.AddSwaggerExamplesFromAssemblyOf<MachineUpdateViewModelExample>();


            // Add application services.
            services.AddScoped<IMachineService, MachineService>();
            services.AddScoped<IMachineGroupService, MachineGroupService>();
            services.AddScoped<IMachineUpdateService, MachineUpdateService>();
            services.AddScoped<ITimelineService, TimelineService>();
            services.AddScoped<IMachineTimelinesService, MachineTimelinesService>();
            services.AddScoped<ITrackableService, TrackableService>();
            services.AddScoped<ISurveyService, SurveyService>();
            
            services.AddSingleton<IBackgroundQueue, BackgroundQueue>();
            services.AddSingleton<IHostedService, QueueSyncService>();

            services.AddCors(options => options.UseConfiguredCors(Configuration.GetSection("CorsPolicy")));
            
            services.AddControllers().AddJsonOptions(options => 
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
            services.AddMvc().AddNewtonsoftJson();
            
            services.AddSignalR();
            services.AddControllersWithViews().AddRazorRuntimeCompilation();
            services.AddRouting(options => options.LowercaseUrls = true);
            
            // start any configured animation jobs
            services.AddSingleton<IManageableHostedService, AnimationsManager>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment()) app.UseDeveloperExceptionPage();

            app.UseStaticFiles();
            app.UseRouting();
            app.UseCors("default");
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    "areas",
                    "{area:exists}/{controller=Home}/{action=Index}/{id?}"
                );
                
                endpoints.MapControllerRoute(
                    "default",
                    "{controller=Home}/{action=Index}/{id?}");
                
                endpoints.MapHub<ClientHub>("/clientHub");
                endpoints.MapHub<ClientHub>("/api/clientHub");
                endpoints.MapHub<ActivityHub>("/hubs/activities");
            });
            
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint($"/swagger/v{ApiVersion}/swagger.json", $"GHOSTS API v{ApiVersion}");
                // c.RoutePrefix = string.Empty; // this places the swagger file at the site root
            });
        }
    }
}