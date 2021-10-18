// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.IO;
using System.Reflection;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Api.Infrastructure.Extensions;
using Ghosts.Api.Services;
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

namespace Ghosts.Api
{
    public class Startup
    {
        public const int apiVersion = 5; 
        
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

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc($"v{apiVersion}", new OpenApiInfo
                {
                    Version = $"v{apiVersion}",
                    Title = "GHOSTS API",
                    Description = $"GHOSTS API v{apiVersion} - Assembly: {ApplicationDetails.Version}",
                    Contact = new OpenApiContact
                    {
                        Name = "Dustin Updyke",
                        Email = "ddupdyke@sei.cmu.edu",
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
            });

            // Add application services.
            services.AddScoped<IMachineService, MachineService>();
            services.AddScoped<IMachineGroupService, MachineGroupService>();
            services.AddScoped<IMachineUpdateService, MachineUpdateService>();
            services.AddScoped<ITimelineService, TimelineService>();
            services.AddScoped<IMachineTimelineService, MachineTimelineService>();
            services.AddScoped<ITrackableService, TrackableService>();

            services.AddSingleton<IBackgroundQueue, BackgroundQueue>();
            services.AddSingleton<IHostedService, QueueSyncService>();

            services.AddCors(options => options.UseConfiguredCors(Configuration.GetSection("CorsPolicy")));
            services.AddRouting(options => options.LowercaseUrls = true);
            services.AddMvc().AddNewtonsoftJson();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment()) app.UseDeveloperExceptionPage();

            app.UseRouting();
            app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
            app.UseCors("default");

            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint($"/swagger/v{apiVersion}/swagger.json", $"GHOSTS API v{apiVersion}");
                c.RoutePrefix = string.Empty;
            });
        }
    }
}