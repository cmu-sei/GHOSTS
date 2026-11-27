using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Services;
using Ghosts.Domain;
using Ghosts.Domain.Messages.MesssagesForServer;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace Ghosts.Api.Tests;

public class QueueSyncTimingTests
{
    // A client's TIMELINE line carries when the action started and finished and the process that
    // ran it; the server stores them on the timeline row and on the trackable row.
    [Fact]
    public async Task Sync_StoresStartFinishAndProcessId_OnTimelineAndTrackable()
    {
        var started = new DateTime(2026, 5, 13, 12, 0, 0, 250, DateTimeKind.Utc);
        var finished = started.AddSeconds(3);
        var trackableId = Guid.NewGuid();
        var record = new TimeLineRecord
        {
            Handler = "BrowserChrome", Command = "click.by.id", CommandArg = "login", TrackableId = trackableId.ToString(),
            StartedUtc = started, FinishedUtc = finished, ProcessId = 4242
        };

        var (timeline, trackable) = await Sync(record);

        Assert.Equal(started, timeline.StartedUtc);
        Assert.Equal(finished, timeline.FinishedUtc);
        Assert.Equal(4242, timeline.ProcessId);
        Assert.Equal(trackableId, trackable!.TrackableId);
        Assert.Equal(started, trackable.StartedUtc);
        Assert.Equal(finished, trackable.FinishedUtc);
        Assert.Equal(4242, trackable.ProcessId);
    }

    // Clients that predate these fields leave them out of the line; the row is stored with them empty.
    [Fact]
    public async Task Sync_LeavesTimingEmpty_WhenTheClientDoesNotSendIt()
    {
        var (timeline, trackable) = await Sync(new TimeLineRecord { Handler = "Command", Command = "dir" });

        Assert.Null(timeline.StartedUtc);
        Assert.Null(timeline.FinishedUtc);
        Assert.Null(timeline.ProcessId);
        Assert.Null(trackable);
    }

    private static async Task<(HistoryTimeline, HistoryTrackable?)> Sync(TimeLineRecord record)
    {
        var databaseName = $"queue-sync-{Guid.NewGuid()}";
        var machine = new Machine { Id = Guid.NewGuid(), Name = "client" };
        await using (var seed = TestDb.Named(databaseName))
        {
            seed.Machines.Add(machine);
            await seed.SaveChangesAsync();
        }

        // the line as a client's BaseHandler.Report writes it
        var json = JsonConvert.SerializeObject(record, Formatting.None, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        var line = $"TIMELINE|05/13/2026 12:00:03.250Z|{json}";

        var queue = new BackgroundQueue();
        queue.Enqueue(new QueueEntry
        {
            Type = QueueEntry.Types.Machine,
            Payload = new MachineQueueEntry
            {
                Machine = machine,
                LogDump = new TransferLogDump { Log = line },
                HistoryType = Machine.MachineHistoryItem.HistoryType.PostedResults
            }
        });

        var services = new ServiceCollection();
        services.AddScoped<ApplicationDbContext>(_ => TestDb.Named(databaseName));
        services.AddSingleton(new Mock<IMachineService>().Object);
        var sync = new QueueSyncService(services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), queue);
        await (Task)typeof(QueueSyncService).GetMethod("Sync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(sync, null)!;

        await using var db = TestDb.Named(databaseName);
        return (db.HistoryTimeline.Single(), db.HistoryTrackables.SingleOrDefault());
    }
}
