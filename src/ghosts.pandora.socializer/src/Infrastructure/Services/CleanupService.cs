namespace Socializer.Infrastructure.Services;

using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

public class CleanupService(ILogger logger, IServiceProvider serviceProvider) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        Run();
        return Task.CompletedTask;
    }
    
    private async void Run()
    {
        while (true)
        {
            try
            {
                await Sync();
            }
            catch (Exception ex)
            {
                logger.LogError($"A sync error occured in the cleanup service {ex.Message}");
            }

            await Task.Delay(
                new TimeSpan(
                    Program.Configuration.CleanupJob.Hours, 
                    Program.Configuration.CleanupJob.Minutes, 
                    Program.Configuration.CleanupJob.Seconds));
        }
    }

    private async Task Sync()
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataContext>();
        var threshold = DateTime.UtcNow
                                    .AddDays(-Program.Configuration.CleanupAge.Days)
                                    .AddHours(-Program.Configuration.CleanupAge.Hours)
                                    .AddMinutes(-Program.Configuration.CleanupAge.Minutes);

        var oldPosts = dbContext.Posts.Where(p => p.CreatedUtc < threshold);
        if(oldPosts.Any())
        {
            dbContext.Posts.RemoveRange(oldPosts);
            await dbContext.SaveChangesAsync();
        }
        
        //now delete images
        var savePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images");
        if (!Directory.Exists(savePath)) return;

        var fileCount = 0; 
        foreach (var file in Directory.GetFiles(savePath))
        {
            if(File.GetCreationTimeUtc(file) > threshold) continue;
            try
            {
                File.Delete(file);
                fileCount++;
            }
            catch (Exception ex)
            {
                logger.LogError($"Could not delete {file}: {ex.Message}");
            }
        }
        logger.LogInformation($"Cleanup service removed {oldPosts.Count()} posts and {fileCount} files");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
