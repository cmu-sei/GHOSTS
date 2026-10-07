// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Ghosts.Api.Tests;

/// <summary>
/// Extraction runs in the background when a source is added (J9): one run per scenario at a time, and a source
/// added during a run gets one more run after it, since a run reads only the chunks pending when it began.
/// </summary>
public class ScenarioExtractionRunnerTests
{
    [Fact]
    public async Task A_source_added_during_a_run_queues_one_more_run_and_no_run_overlaps()
    {
        var extraction = new ScriptedExtraction();
        var services = new ServiceCollection().AddScoped<IScenarioExtractionService>(_ => extraction).BuildServiceProvider();
        var runner = new ScenarioExtractionRunner(services.GetRequiredService<IServiceScopeFactory>());

        runner.Start(7);
        await extraction.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(runner.IsRunning(7));

        // Two more sources arrive while the first run is going: one further run covers them both.
        runner.Start(7);
        runner.Start(7);
        Assert.Equal(1, extraction.Calls);
        extraction.Release.SetResult();

        await WaitUntil(() => !runner.IsRunning(7));
        Assert.Equal(2, extraction.Calls);
        Assert.Equal([7, 7], extraction.Scenarios);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline) await Task.Delay(20);
        Assert.True(condition());
    }

    /// <summary>Blocks its first call until released; later calls return at once. Counts every call.</summary>
    private sealed class ScriptedExtraction : IScenarioExtractionService
    {
        public readonly TaskCompletionSource Started = new();
        public readonly TaskCompletionSource Release = new();
        public readonly List<int> Scenarios = [];
        public int Calls => Scenarios.Count;

        public async Task<ExtractionResultDto> ExtractAllAsync(int scenarioId, CancellationToken ct)
        {
            lock (Scenarios) Scenarios.Add(scenarioId);
            if (Calls == 1)
            {
                Started.SetResult();
                await Release.Task;
            }
            return new ExtractionResultDto(0, 0, 0, []);
        }

        public Task<ExtractionResultDto> ExtractChunkAsync(int chunkId, CancellationToken ct) => throw new NotSupportedException();
    }
}
