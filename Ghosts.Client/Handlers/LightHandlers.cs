// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.IO;
using System.Threading;
using Ghosts.Client.Code;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using NLog;

namespace Ghosts.Client.Handlers
{
    public class LightHandlers
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        private static string GetSavePath(Type cls, TimelineHandler handler, TimelineEvent timelineEvent, string fileExtension)
        {
            _log.Trace($"{cls} event - {timelineEvent}");
            WorkingHours.Is(handler);

            if (timelineEvent.DelayBefore > 0)
                Thread.Sleep(timelineEvent.DelayBefore);

            Thread.Sleep(3000);

            var rand = RandomFilename.Generate();

            var dir = timelineEvent.CommandArgs[0].ToString();
            if (dir.Contains("%"))
                dir = Environment.ExpandEnvironmentVariables(dir);
            if (Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var path = $"{dir}\\{rand}.{fileExtension}";

            //if directory does not exist, create!
            _log.Trace($"Checking directory at {path}");
            var f = new FileInfo(path).Directory;
            if (f == null)
            {
                _log.Trace($"Directory does not exist, creating directory at {f.FullName}");
                Directory.CreateDirectory(f.FullName);
            }

            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception e)
            {
                _log.Debug(e);
            }

            _log.Trace($"{cls} saving to path - {path}");
            return path;
        }

        public class LightWordHandler : BaseHandler
        {
          private static readonly Logger _log = LogManager.GetCurrentClassLogger();

            public LightWordHandler(TimelineHandler handler)
            {
                _log.Trace("Launching Light Word handler");
                try
                {
                    if (handler.Loop)
                    {
                        _log.Trace("Light Word loop");
                        while (true)
                        {
                            ExecuteEvents(handler);
                        }
                    }
                    else
                    {
                        _log.Trace("Light Word single run");
                        ExecuteEvents(handler);
                    }
                }
                catch (Exception e)
                {
                    _log.Error(e);
                }
            }

            private void ExecuteEvents(TimelineHandler handler)
            {
                try
                {
                    foreach (var timelineEvent in handler.TimeLineEvents)
                    {
                        var path = GetSavePath(typeof(LightExcelHandler), handler, timelineEvent, "docx");

                        var list = RandomText.GetDictionary.GetDictionaryList();
                        var rt = new RandomText(list.ToArray());
                        rt.AddSentence(5);

                        var title = rt.Content;
                        rt.AddContentParagraphs(2, 3, 5, 7, 22);
                        var paragraph = rt.Content;

                        Domain.Code.Office.Word.Write(path, title, paragraph);

                        FileListing.Add(path);
                        this.Report(handler.HandlerType.ToString(), timelineEvent.Command,
                            timelineEvent.CommandArgs[0].ToString());
                    }
                }
                catch (Exception e)
                {
                    _log.Error(e);
                }
            }
        }

        public class LightPowerPointHandler : BaseHandler
        {
            // TODO
        }

        public class LightExcelHandler : BaseHandler
        {
            private static readonly Logger _log = LogManager.GetCurrentClassLogger();

            public LightExcelHandler(TimelineHandler handler)
            {
                _log.Trace("Launching Light Excel handler");
                try
                {
                    if (handler.Loop)
                    {
                        _log.Trace("Light Excel loop");
                        while (true)
                        {
                            ExecuteEvents(handler);
                        }
                    }
                    else
                    {
                        _log.Trace("Light Excel single run");
                        ExecuteEvents(handler);
                    }
                }
                catch (Exception e)
                {
                    _log.Error(e);
                }
            }

            private void ExecuteEvents(TimelineHandler handler)
            {
                try
                {
                    foreach (var timelineEvent in handler.TimeLineEvents)
                    {
                        var path = GetSavePath(typeof(LightExcelHandler), handler, timelineEvent, "xlsx");

                        var list = RandomText.GetDictionary.GetDictionaryList();
                        var rt = new RandomText(list.ToArray());
                        rt.AddSentence(5);
                        
                        Domain.Code.Office.Excel.Write(path, rt.Content);

                        FileListing.Add(path);
                        this.Report(handler.HandlerType.ToString(), timelineEvent.Command,
                            timelineEvent.CommandArgs[0].ToString());
                    }
                }
                catch (Exception e)
                {
                    _log.Error(e);
                }
            }
        }
    }
}