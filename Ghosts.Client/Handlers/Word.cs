// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.IO;
using System.Threading;
using Ghosts.Client.Code;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Word = NetOffice.WordApi;
using NetOffice.WordApi.Enums;
using NLog;

namespace Ghosts.Client.Handlers
{
    public class WordHandler : BaseHandler
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public WordHandler(TimelineHandler handler)
        {
            _log.Trace("Launching Word handler");
            try
            {
                if (handler.Loop)
                {
                    _log.Trace("Word loop");
                    while (true)
                    {
                        KillApp();
                        ExecuteEvents(handler);
                        KillApp();
                    }
                }
                else
                {
                    _log.Trace("Word single run");
                    KillApp();
                    ExecuteEvents(handler);
                    KillApp();
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
            }
            finally
            {
                KillApp();
            }
        }

        private static void KillApp()
        {
            ProcessManager.KillProcessAndChildrenByName(ProcessManager.ProcessNames.Word);
        }

        private void ExecuteEvents(TimelineHandler handler)
        {
            try
            {
                foreach (var timelineEvent in handler.TimeLineEvents)
                {
                    WorkingHours.Is(handler);

                    if (timelineEvent.DelayBefore > 0)
                        Thread.Sleep(timelineEvent.DelayBefore);

                    // start word and turn off msg boxes
                    var wordApplication = new Word.Application();
                    wordApplication.DisplayAlerts = WdAlertLevel.wdAlertsNone;
                    wordApplication.Visible = true;

                    // add a new document
                    var newDocument = wordApplication.Documents.Add();

                    // insert some text
                    var list = RandomText.GetDictionary.GetDictionaryList();
                    var rt = new RandomText(list.ToArray());
                    rt.AddContentParagraphs(1, 1, 1, 10, 50);
                    wordApplication.Selection.TypeText(rt.Content);

                    Thread.Sleep(180000); //wait 3 minutes

                    wordApplication.Selection.HomeKey(WdUnits.wdLine, WdMovementType.wdExtend);
                    wordApplication.Selection.Font.Color = WdColor.wdColorSeaGreen;
                    wordApplication.Selection.Font.Bold = 1;
                    wordApplication.Selection.Font.Size = 18;

                    var rand = RandomFilename.Generate();

                    var dir = timelineEvent.CommandArgs[0];
                    if (dir.Contains("%"))
                        dir = Environment.ExpandEnvironmentVariables(dir);
                    if (Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    var path = $"{dir}\\{rand}.docx";

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

                    newDocument.SaveAs(path);
                    this.Report(handler.HandlerType.ToString(), timelineEvent.Command, timelineEvent.CommandArgs[0]);

                    FileListing.Add(path);

                    try
                    {
                        wordApplication.Quit();
                    }
                    catch (Exception e)
                    {
                    }

                    try
                    {
                        wordApplication.Dispose();
                    }
                    catch (Exception e)
                    {
                    }

                    if (wordApplication != null)
                    {
                        try
                        {
                            System.Runtime.InteropServices.Marshal.ReleaseComObject(wordApplication);
                        }
                        catch
                        {   
                        }

                        try
                        {
                            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(wordApplication);
                        }
                        catch
                        {
                        }
                    }

                    wordApplication = null;
                    GC.Collect();

                    if (timelineEvent.DelayAfter > 0)
                        Thread.Sleep(timelineEvent.DelayAfter);
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
            }

            try
            {
                KillApp();
            }
            catch (Exception e)
            {
                _log.Error(e);
            }

            try
            {
                FileListing.FlushList();
            }
            catch (Exception e)
            {
                _log.Error(e);
            }
        }
    }
}
