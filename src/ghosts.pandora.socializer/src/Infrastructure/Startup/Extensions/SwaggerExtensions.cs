using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;

namespace Ghosts.Socializer.Infrastructure.Startup.Extensions;

public static class SwaggerExtensions
{
    public static void AddSwagger(this IServiceCollection services, AuthorizationOptions authOptions = null)
    {
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "GHOSTS SOCIALIZER API",
                Version = "v1",
                Description = "GHOSTS Social Content Services API v1",
                Contact = new OpenApiContact
                {
                    Name = "SEI CERT GHOSTS Team",
                    Email = "info@sei.cmu.edu"
                },
                License = new OpenApiLicense
                {
                    Name =
                        $"Copyright 2025 Carnegie Mellon University. All Rights Reserved. See license.md file for terms"
                }
            });

            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                c.IncludeXmlComments(xmlPath);
            }
            c.EnableAnnotations();
        });
    }
}
