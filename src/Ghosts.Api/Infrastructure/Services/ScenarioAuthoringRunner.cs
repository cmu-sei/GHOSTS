// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using NLog;

namespace Ghosts.Api.Infrastructure.Services
{
    /// <summary>
    /// Runs authoring turns in the background, not inside the browser's request (section 10): a drafting
    /// turn takes minutes, and its own limit, not the request, ends it (G2, G3). One turn per session at a
    /// time in this process; the service's own lock also keeps an import out while a turn runs.
    /// </summary>
    public class ScenarioAuthoringRunner(IServiceScopeFactory scopes, IAuthoringProgress progress)
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private static readonly ConcurrentDictionary<Guid, DateTime> Running = new();

        public bool IsRunning(Guid sessionId) => Running.ContainsKey(sessionId);

        /// <summary>Starts a turn and returns at once; false when the session already has one running.</summary>
        public bool TryStart(Guid sessionId, int? scenarioId, string message)
        {
            if (!Running.TryAdd(sessionId, DateTime.UtcNow)) return false;

            _ = Task.Run(async () =>
            {
                AuthoringTurnResult result = null;
                string error = null;
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    var authoring = scope.ServiceProvider.GetRequiredService<IScenarioAuthoringService>();
                    result = await authoring.RunTurnAsync(sessionId, message, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _log.Error(ex, $"Authoring turn failed outside the turn, session {sessionId}");
                    error = ex is AuthoringSessionBusyException ? ex.Message : $"{ex.GetType().Name}: {ex.Message}";
                }
                finally
                {
                    Running.TryRemove(sessionId, out _);
                }

                // Sent after the session is free, so the page can send its next message when it hears it.
                await progress.SendAsync(scenarioId, new { sessionId, turn = result?.Turn, kind = "turn-ended", detail = new { error }, at = DateTime.UtcNow });
            });
            return true;
        }
    }

    /// <summary>A turn's progress, to every page open on the session's scenario, through the Scenario Builder's hub.</summary>
    public class HubAuthoringProgress(IHubContext<ScenarioBuilderHub> hub) : IAuthoringProgress
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public async Task SendAsync(int? scenarioId, object progress)
        {
            if (scenarioId == null) return;
            try
            {
                foreach (var connectionId in ScenarioBuilderHub.GetConnections().GetConnections(scenarioId.ToString()).ToList())
                    await hub.Clients.Client(connectionId).SendAsync("authoringProgress", progress);
            }
            catch (Exception ex)
            {
                _log.Warn(ex, "Failed to send authoring progress");
            }
        }
    }
}
