
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Socializer.Infrastructure.Services;
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

    // only works in Docker container as looking for overlay format
    static int GetDiskUtil()
    {

        var allDrives = DriveInfo.GetDrives();
        foreach (var d in allDrives)
        {
            if (d.Name == "/" && d.DriveFormat == "overlay")
            {
                var utilFloat = 100.0 - ((double)(d.AvailableFreeSpace) * 100.0) / (double)d.TotalSize;
                return (int)utilFloat;
            }
        }
        return -1;  // unable to get disk util
    }

    // This task does post cleanup
    // if CleanupDiskUtilThreshold < 0, then disk utilization is not checked and posts are deleteed
    // according to the CleanUpAge
    // if CleanupDiskUtilThreshold > 0, then disk cleanup triggered only when the disk utilization exceeds
    // this value. The deletion loop halves the post threshold time each time through the loop if the
    // disk utilization threadhold is not met by deleting posts.
    // If the threshold falls below 2 hours or 10 loop iterations is reached (good for 100 day old posts)
    // then the loop is exited with a message indicating that the target disk utilization could not be reached.
    // So, when using disk utilization, can specify large CleanUpAge on posts and not worry about filling up
    // disk.

    private async Task Sync()
    {
        var diskUtil = GetDiskUtil();
        var targetUtilization = Program.Configuration.CleanupDiskUtilThreshold;
        logger.LogInformation($"Cleanup Service: Disk Utilization {diskUtil}, CleanupDiskUtilThreshold {targetUtilization}");

        // set actual targetUtilization 10% lower of specified value so that we are not constantly tripping the cleanup
        if (targetUtilization > 0)
        {
            targetUtilization -= (int)(targetUtilization * 0.1);
        }

        if (targetUtilization > 0 && diskUtil < 0)
        {
            logger.LogInformation($"Cleanup will not check disk utilization as the disk utilization info cannot be read.");
            targetUtilization = -1; //skip this as we cannot check disktuil
        }
        if (targetUtilization > 0 && diskUtil < targetUtilization)
        {
            logger.LogInformation($"Cleanup skipped as disk threshold not reached.");
            return;
        }


        var totalPostCount = 0;
        var totalFileCount = 0;
        var threshold = DateTime.UtcNow
                                        .AddDays(-Program.Configuration.CleanupAge.Days)
                                        .AddHours(-Program.Configuration.CleanupAge.Hours)
                                        .AddMinutes(-Program.Configuration.CleanupAge.Minutes);

        var startTime = DateTime.UtcNow;

        var minThreshold = DateTime.UtcNow.AddHours(-2);  // this should be low enough for a minimum threshold
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataContext>();
        var loopExit = false;
        var loopCount = 0;

        // if CleanupDiskUtilThreshold < 0 then only one loop iteration is done as we are not checking disk utilization
        while (!loopExit)
        {
            if (loopCount == 0)
            {
                logger.LogInformation($"Cleanup service beginning post deletion with threshold: {threshold}");
            }
            else
            {
                logger.LogInformation($"Cleanup service beginning post deletion with new threshold: {threshold}");
            }
            var oldPosts = dbContext.Posts.Where(p => p.CreatedUtc < threshold);
            totalPostCount += oldPosts.Count();   //get the count from the query before posts are deleted
            if (oldPosts.Any())
            {
                dbContext.Posts.RemoveRange(oldPosts);
                await dbContext.SaveChangesAsync();
            }

            //now delete images
            var savePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images");
            if (Directory.Exists(savePath))
            {
                var fileCount = 0;
                foreach (var dirName in Directory.GetDirectories(savePath))
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
                            fileCount++;
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

                }
                totalFileCount += fileCount;
            }

            // update disk utilization
            diskUtil = GetDiskUtil();
            // check for loop exit
            if (targetUtilization < 0) loopExit = true;  // exit if not checking disk util
            else if (diskUtil < targetUtilization) loopExit = true; // met target disk util
            else
            {
                // have not met disk utilization, try to lower threshold
                // compute a new threshold that is half of old threshold
                threshold = startTime - (startTime - threshold) / 2;
                // at this point, above disk utilization. Check if we have reached mininium threshold of 2 hours
                if (threshold > minThreshold)
                {
                    logger.LogInformation($"Cleanup service is unable to free disk space to reach target utilization of {targetUtilization}");
                    loopExit = true;
                }
            }
            loopCount++;
            if (loopCount > 10)
            {
                // 10 iterations is good enough for 100 days. Something is wrong.
                logger.LogInformation($"Cleanup service is unable to free disk space to reach target utilization of {targetUtilization}, max iterations reached.");
                loopExit = true;
            }
        }

        logger.LogInformation($"Cleanup service removed {totalPostCount} posts and {totalFileCount} files");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
