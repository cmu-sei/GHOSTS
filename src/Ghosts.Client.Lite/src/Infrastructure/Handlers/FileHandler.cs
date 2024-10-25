// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text;
using Ghosts.Client.Lite.Infrastructure.Services;
using Ghosts.Domain;
using Ghosts.Domain.Code.Helpers;
using Newtonsoft.Json;
using NLog;
using Quartz;

namespace Ghosts.Client.Lite.Infrastructure.Handlers;

public class FileCreatorJob : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var raw = context.MergedJobDataMap["handler"].ToString();
        if (string.IsNullOrEmpty(raw))
            return;
        var handler = JsonConvert.DeserializeObject<TimelineHandler>(raw);
        if (handler == null)
            return;

        await FileHandler.Run(handler);
    }
}

public class FileHandler
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();

    public static async Task Run(TimelineHandler handler)
    {
        foreach (var timelineEvent in handler.TimeLineEvents)
        {
            await Run(handler.HandlerType, timelineEvent);
        }
    }

    public static async Task Run(HandlerType handler, TimelineEvent t)
    {
        var sizeMap = new Dictionary<string, int>
        {
            { "Word", 1000001 },
            { "Excel", 100001 },
            { "PowerPoint", 500001 }
        };

        var rand = RandomFilename.Generate();

        var defaultSaveDirectory = t.CommandArgs[0].ToString();
        if (defaultSaveDirectory!.Contains('%'))
        {
            defaultSaveDirectory = Environment.ExpandEnvironmentVariables(defaultSaveDirectory);
        }

        try
        {
            foreach (var key in t.CommandArgs)
            {
                if (key.ToString()!.StartsWith("save-array:"))
                {
                    var savePathString = key.ToString()!.Replace("save-array:", "").Replace("'", "\"");
                    savePathString = savePathString.Replace("\\", "/"); // Can't deserialize Windows path
                    var savePaths = JsonConvert.DeserializeObject<string[]>(savePathString);
                    defaultSaveDirectory = savePaths.PickRandom().Replace("/", "\\"); // Revert to Windows path
                    if (defaultSaveDirectory.Contains('%'))
                    {
                        defaultSaveDirectory = Environment.ExpandEnvironmentVariables(defaultSaveDirectory);
                    }

                    break;
                }
            }
        }
        catch (Exception e)
        {
            _log.Trace($"save-array exception: {e}");
        }

        defaultSaveDirectory = ApplicationDetails.GetPath(defaultSaveDirectory);

        if (!Directory.Exists(defaultSaveDirectory))
        {
            Directory.CreateDirectory(defaultSaveDirectory);
        }

        var ext = handler switch
        {
            HandlerType.Excel => "xlsx",
            HandlerType.PowerPoint => "pptx",
            _ => "docx"
        };

        var path = $"{defaultSaveDirectory}\\{rand}.{ext}";

        try
        {
            await using (var fs = File.Create(path))
            {
                _log.Trace(File.Exists(path));
                var bitLength = new Random().Next(1000, sizeMap[handler.ToString()]);
                var info = new UTF8Encoding(true).GetBytes(GenerateBits(bitLength));
                await fs.WriteAsync(info);
            }

            // Report on file creation success
            LogWriter.Timeline(new TimeLineRecord
            {
                Command = t.Command,
                CommandArg = path,
                Handler = handler.ToString()
            });
        }
        catch (Exception ex)
        {
            _log.Error(ex);
        }
    }

    private static string GenerateBits(int length)
    {
        var rand = new Random();
        var sb = new StringBuilder(length);
        for (var i = 0; i < length; i++)
        {
            sb.Append(rand.Next(2));
        }

        return sb.ToString();
    }
}
