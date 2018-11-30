// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Client.Code;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using NetOffice.WordApi.Enums;
using NLog;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Word = NetOffice.WordApi;

namespace Ghosts.Client.Handlers
{
    public class WordHandler : BaseHandler
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public WordHandler(Timeline timeline, TimelineHandler handler)
        {
            _log.Trace("Launching Word handler");
            try
            {
                if (handler.Loop)
                {
                    _log.Trace("Word loop");
                    while (true)
                    {
                        if (timeline != null)
                        {
                            var pids = ProcessManager.GetPids(ProcessManager.ProcessNames.Word).ToList();
                            if (pids.Count > timeline.TimeLineHandlers.Count(o => o.HandlerType == HandlerType.Word))
                            {
                                continue;
                            }
                        }

                        ExecuteEvents(timeline, handler);
                        Thread.Sleep(300000);
                    }
                }
                else
                {
                    _log.Trace("Word single run");
                    KillApp();
                    ExecuteEvents(timeline, handler);
                    KillApp();
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
                KillApp();
            }
        }

        private static void KillApp()
        {
            ProcessManager.KillProcessAndChildrenByName(ProcessManager.ProcessNames.Word);
        }

        private void ExecuteEvents(Timeline timeline, TimelineHandler handler)
        {
            try
            {
                foreach (TimelineEvent timelineEvent in handler.TimeLineEvents)
                {
                    try
                    {
                        WorkingHours.Is(handler);

                        if (timelineEvent.DelayBefore > 0)
                        {
                            Thread.Sleep(timelineEvent.DelayBefore);
                        }

                        if (timeline != null)
                        {
                            var pids = ProcessManager.GetPids(ProcessManager.ProcessNames.Word).ToList();
                            if (pids.Count > timeline.TimeLineHandlers.Count(o => o.HandlerType == HandlerType.Word))
                            {
                                return;
                            }
                        }

                        // start word and turn off msg boxes
                        Word.Application wordApplication = new Word.Application
                        {
                            DisplayAlerts = WdAlertLevel.wdAlertsNone,
                            Visible = true
                        };

                        // add a new document
                        Word.Document newDocument = wordApplication.Documents.Add();

                        try
                        {
                            wordApplication.WindowState = WdWindowState.wdWindowStateMinimize;
                            foreach (Word.Document item in wordApplication.Documents)
                            {
                                item.Windows[1].WindowState = WdWindowState.wdWindowStateMinimize;
                            }
                        }
                        catch (Exception e)
                        {
                            _log.Trace($"Could not minimize: {e}");
                        }

                        // insert some text
                        System.Collections.Generic.List<string> list = RandomText.GetDictionary.GetDictionaryList();
                        RandomText rt = new RandomText(list.ToArray());
                        rt.AddContentParagraphs(1, 1, 1, 10, 50);
                        wordApplication.Selection.TypeText(rt.Content);

                        Thread.Sleep(180000); //wait 3 minutes

                        wordApplication.Selection.HomeKey(WdUnits.wdLine, WdMovementType.wdExtend);
                        wordApplication.Selection.Font.Color = WdColor.wdColorSeaGreen;
                        wordApplication.Selection.Font.Bold = 1;
                        wordApplication.Selection.Font.Size = 18;

                        string rand = RandomFilename.Generate();

                        string dir = timelineEvent.CommandArgs[0].ToString();
                        if (dir.Contains("%"))
                        {
                            dir = Environment.ExpandEnvironmentVariables(dir);
                        }

                        if (Directory.Exists(dir))
                        {
                            Directory.CreateDirectory(dir);
                        }

                        string path = $"{dir}\\{rand}.docx";

                        //if directory does not exist, create!
                        _log.Trace($"Checking directory at {path}");
                        DirectoryInfo f = new FileInfo(path).Directory;
                        if (f == null)
                        {
                            _log.Trace($"Directory does not exist, creating directory at {f.FullName}");
                            Directory.CreateDirectory(f.FullName);
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

                        newDocument.SaveAs(path);
                        Report(handler.HandlerType.ToString(), timelineEvent.Command, timelineEvent.CommandArgs[0].ToString());

                        FileListing.Add(path);

                        Thread.Sleep(20000);

                        wordApplication.Quit();
                        wordApplication.Dispose();

                        if (wordApplication != null)
                        {
                            try
                            {
                                Marshal.ReleaseComObject(wordApplication);
                            }
                            catch
                            {
                            }

                            try
                            {
                                Marshal.FinalReleaseComObject(wordApplication);
                            }
                            catch
                            {
                            }
                        }

                        wordApplication = null;
                        GC.Collect();
                    }
                    catch (Exception e)
                    {
                        _log.Debug(e);
                    }
                    finally
                    {
                        if (timelineEvent.DelayAfter > 0)
                        {
                            Thread.Sleep(timelineEvent.DelayAfter);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
            }
            finally
            {
                KillApp();
                FileListing.FlushList();
            }
        }
    }
}
