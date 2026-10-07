// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NLog;

namespace Ghosts.Api.Infrastructure.Services
{
    /// <summary>
    /// Runs extraction in the background when a source is added (J9): the request returns at once, and the
    /// Sources step follows the run over the hub's extractionProgress events. One run per scenario at a time;
    /// a source added during a run queues one more, since the run reads only the chunks pending when it began.
    /// </summary>
    public class ScenarioExtractionRunner(IServiceScopeFactory scopes)
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private static readonly ConcurrentDictionary<int, bool> Running = new();
        private static readonly ConcurrentDictionary<int, bool> Again = new();

        public bool IsRunning(int scenarioId) => Running.ContainsKey(scenarioId);

        /// <summary>Starts a run for the scenario's pending chunks, or queues one if a run is already going.</summary>
        public void Start(int scenarioId)
        {
            if (!Running.TryAdd(scenarioId, true))
            {
                Again[scenarioId] = true;
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    do
                    {
                        Again.TryRemove(scenarioId, out _);
                        await using var scope = scopes.CreateAsyncScope();
                        var extraction = scope.ServiceProvider.GetRequiredService<IScenarioExtractionService>();
                        await extraction.ExtractAllAsync(scenarioId, CancellationToken.None);
                    } while (Again.ContainsKey(scenarioId));
                }
                catch (Exception ex)
                {
                    _log.Error(ex, $"Extraction after a source was added failed, scenario {scenarioId}");
                }
                finally
                {
                    Running.TryRemove(scenarioId, out _);
                }
            });
        }
    }
}
