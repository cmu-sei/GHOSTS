// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NLog;

namespace Ghosts.Api.Infrastructure.Terrain;

/// <summary>Every five seconds, refreshes the status and manifest of each execution whose terrain is still being built or torn down.</summary>
public class TerrainStatusPoller(IServiceScopeFactory scopes) : BackgroundService
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<ITerrainService>().PollAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.Warn(ex, "Terrain poll cycle failed");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
