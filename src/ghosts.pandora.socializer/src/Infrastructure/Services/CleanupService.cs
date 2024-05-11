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

    // This startDir points to wwwroot/images, so directories will either be post directories or 'u', skip 'u'
    static int deleteFiles(System.DateTime threshold, string startDir)
    {
        int count = 0;
        foreach (var dirName in Directory.GetDirectories(startDir))
        {
            var fullPath = Path.GetFullPath(dirName).TrimEnd(Path.DirectorySeparatorChar);
            var lastDirname = fullPath.Split(Path.DirectorySeparatorChar).Last();
            if (lastDirname == "u") continue;  // skip 'u' directory as that contains avatar images, never delete these
            if (File.GetCreationTimeUtc(dirName) > threshold) continue;
            // found a post directory that meets the criteria. First delete all files in the directory, then the directory
            // we know that there are only files in this directory, it does not contain subdirectories
            foreach (var file in Directory.GetFiles(fullPath))
            {
                try
                {
                    File.Delete(file);  // Delete file in post directory
                    count++;
                }
                catch (Exception ex)
                {
                    logger.LogError($"Could not delete file {file}: {ex.Message}");
                }
            }
            // now delete the post directory, will be empty
            // we could just delete the entire directory+files in one call but there have been bug reports about this 
            // not working consistently across all platforms, safer to delete the files first to get an empty directory
            try
            {
                Directory.Delete(fullPath);
            }
            catch (Exception ex)
            {
                logger.LogError($"Could not delete directory {fullPath}: {ex.Message}");
            }

            count++;
        }
        return count;
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
        var oldPostCount = oldPosts.Count();   //get the count from the query before posts are deleted
        if(oldPosts.Any())
        {
            dbContext.Posts.RemoveRange(oldPosts);
            await dbContext.SaveChangesAsync();
        }
        
        //now delete images
        var savePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images");
        if (!Directory.Exists(savePath)) return;

        var fileCount = deleteFiles(threshold, savePath);
        logger.LogInformation($"Cleanup service removed {oldPostCount} posts and {fileCount} files");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
