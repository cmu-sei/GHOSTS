using Quartz;

namespace Ghosts.Client.Lite.Infrastructure.Handlers;

public class MemoryCleanupJob : IJob
{
    public Task Execute(IJobExecutionContext context)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        return Task.CompletedTask;
    }
}
