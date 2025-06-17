// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Client.Infrastructure;
using Ghosts.Domain;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Client.Universal.Infrastructure;
using Ghosts.Domain.Code;
using NLog;

namespace Ghosts.Client.Universal.Handlers;

public class LightHandlers
{
    internal static readonly Logger _log = LogManager.GetCurrentClassLogger();

    private static string GetSavePath(Type cls, TimelineHandler handler, TimelineEvent timelineEvent,
        string fileExtension)
    {
        _log.Trace($"{cls} event - {timelineEvent}");
        WorkingHours.Is(handler);

        if (timelineEvent.DelayBeforeActual > 0)
        {
            Thread.Sleep(timelineEvent.DelayBeforeActual);
        }

        Thread.Sleep(3000);

        var rand = RandomFilename.Generate();

        var dir = timelineEvent.CommandArgs[0].ToString();
        if (dir.Contains("%"))
        {
            dir = Environment.ExpandEnvironmentVariables(dir);
        }

        if (Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var path = $"{dir}\\{rand}.{fileExtension}";

        //if directory does not exist, create!
        _log.Trace($"Checking directory at {path}");
        var f = new FileInfo(path).Directory;
        if (f == null)
        {
            _log.Trace($"Directory does not exist, creating directory at {path}");
            Directory.CreateDirectory(path);
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception e)
        {
            _log.Debug(e);
        }

        _log.Trace($"{cls} saving to path - {path}");
        return path;
    }

    public class LightWordHandler(
        Timeline entireTimeline,
        TimelineHandler timelineHandler,
        CancellationToken cancellationToken)
        : BaseHandler(entireTimeline, timelineHandler, cancellationToken)
    {
        protected override Task RunOnce()
        {
            try
            {
                foreach (var timelineEvent in this.Handler.TimeLineEvents)
                {
                    var path = GetSavePath(typeof(LightExcelHandler), this.Handler, timelineEvent, "docx");

                    var list = RandomText.GetDictionary.GetDictionaryList();
                    using (var rt = new RandomText(list))
                    {
                        rt.AddSentence(5);

                        var title = rt.Content;
                        rt.AddContentParagraphs(2, 3, 5, 7, 22);
                        var paragraph = rt.Content;
                        Domain.Code.Office.Word.Write(path, title, paragraph);
                    }

                    FileListing.Add(path, this.Handler.HandlerType);
                    Report(new ReportItem
                    {
                        Handler = this.Handler.HandlerType.ToString(),
                        Command = timelineEvent.Command,
                        Arg = timelineEvent.CommandArgs[0].ToString(),
                        Trackable = timelineEvent.TrackableId
                    });
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
            }

            return Task.CompletedTask;
        }
    }

    public class LightPowerPointHandler(
        Timeline entireTimeline,
        TimelineHandler timelineHandler,
        CancellationToken cancellationToken)
        : BaseHandler(entireTimeline, timelineHandler, cancellationToken)
    {
        protected override Task RunOnce()
        {
            throw new NotImplementedException();
        }
    }

    public class LightExcelHandler(
        Timeline entireTimeline,
        TimelineHandler timelineHandler,
        CancellationToken cancellationToken)
        : BaseHandler(entireTimeline, timelineHandler, cancellationToken)
    {
        protected override Task RunOnce()
        {
            try
            {
                foreach (var timelineEvent in this.Handler.TimeLineEvents)
                {
                    var path = GetSavePath(typeof(LightExcelHandler), this.Handler, timelineEvent, "xlsx");

                    var list = RandomText.GetDictionary.GetDictionaryList();
                    using (var rt = new RandomText(list))
                    {
                        rt.AddSentence(5);
                        Domain.Code.Office.Excel.Write(path, rt.Content);
                    }

                    FileListing.Add(path, this.Handler.HandlerType);
                    Report(new ReportItem
                    {
                        Handler = this.Handler.HandlerType.ToString(),
                        Command = timelineEvent.Command,
                        Arg = timelineEvent.CommandArgs[0].ToString(),
                        Trackable = timelineEvent.TrackableId
                    });
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
            }

            return Task.CompletedTask;
        }
    }
}
