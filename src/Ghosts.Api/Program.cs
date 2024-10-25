// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using Ghosts.Api.Infrastructure;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Domain.Code;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog;
using Npgsql;

namespace Ghosts.Api
{
    public class Program
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public static ApplicationSettings ApplicationSettings { get; set; }

        public static void Main(string[] args)
        {
            Console.WriteLine(ApplicationDetails.Header);

            var msg = $"GHOSTS API {ApplicationDetails.Version} ({ApplicationDetails.VersionFile}) coming online...";
            Console.WriteLine(msg);
            _log.Info(msg);

            ApiDetails.LoadConfiguration();

            var host = WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>()
                .Build();

            NpgsqlConnection.GlobalTypeMapper.EnableDynamicJson();

            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

            using (var scope = host.Services.CreateScope())
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

            host.Run();
        }
    }
}
