// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Client.Infrastructure;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using NetOffice.OfficeApi.Enums;
using NetOffice.PowerPointApi.Enums;
using NLog;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using PowerPoint = NetOffice.PowerPointApi;
using PpSlideLayout = NetOffice.PowerPointApi.Enums.PpSlideLayout;

namespace Ghosts.Client.Handlers
{
    public class PowerPointHandler : BaseHandler
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public PowerPointHandler(Timeline timeline, TimelineHandler handler)
        {
            _log.Trace("Launching PowerPoint handler");
            try
            {
                if (handler.Loop)
                {
                    _log.Trace("PowerPoint loop");
                    while (true)
                    {
                        if (timeline != null)
                        {
                            var processIds = ProcessManager.GetPids(ProcessManager.ProcessNames.PowerPoint).ToList();
                            if (processIds.Count > timeline.TimeLineHandlers.Count(o => o.HandlerType == HandlerType.PowerPoint))
                            {
                                continue;
                            }
                        }

                        ExecuteEvents(timeline, handler);
                    }
                }
                else
                {
                    _log.Trace("PowerPoint single run");
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
            ProcessManager.KillProcessAndChildrenByName(ProcessManager.ProcessNames.PowerPoint);
        }

        private void ExecuteEvents(Timeline timeline, TimelineHandler handler)
        {
            try
            {
                foreach (var timelineEvent in handler.TimeLineEvents)
                {
                    try
                    {
                        _log.Trace($"PowerPoint event - {timelineEvent}");
                        WorkingHours.Is(handler);

                        if (timelineEvent.DelayBefore > 0)
                        {
                            Thread.Sleep(timelineEvent.DelayBefore);
                        }

                        if (timeline != null)
                        {
                            var processIds = ProcessManager.GetPids(ProcessManager.ProcessNames.PowerPoint).ToList();
                            if (processIds.Count > timeline.TimeLineHandlers.Count(o => o.HandlerType == HandlerType.PowerPoint))
                            {
                                return;
                            }
                        }

                        var powerApplication = new PowerPoint.Application
                        {
                            DisplayAlerts = PpAlertLevel.ppAlertsNone,
                            Visible = MsoTriState.msoTrue
                        };

                        try
                        {
                            powerApplication.WindowState = PpWindowState.ppWindowMinimized;
                        }
                        catch (Exception e)
                        {
                            _log.Trace($"Could not minimize: {e}");
                        }

                        // add a new presentation with one new slide
                        var presentation = powerApplication.Presentations.Add(MsoTriState.msoTrue);
                        presentation.Slides.Add(1, PpSlideLayout.ppLayoutClipArtAndVerticalText);

                        var writeSleep = ProcessManager.Jitter(100);
                        Thread.Sleep(writeSleep);

                        // save the document 
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

                        var path = $"{dir}\\{rand}.pptx";

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

                        Thread.Sleep(5000);
                        presentation.SaveAs(path);

                        FileListing.Add(path);
                        Report(handler.HandlerType.ToString(), timelineEvent.Command, timelineEvent.CommandArgs[0].ToString());

                        if (timelineEvent.CommandArgs.Contains("pdf"))
                        {
                            // Save document into PDF Format
                            var outputFileName = timelineEvent.CommandArgs.Contains("pdf-vary-filenames") ? $"{RandomFilename.Generate()}.pdf" : presentation.FullName.Replace(".pptx", ".pdf");
                            object fileFormat = PpSaveAsFileType.ppSaveAsPDF;

                            presentation.SaveAs(outputFileName, fileFormat, MsoTriState.msoCTrue);
                            // end save as pdf
                            Report(handler.HandlerType.ToString(), timelineEvent.Command, "pdf");
                            FileListing.Add(outputFileName);
                        }

                        presentation.Close();

                        if (timelineEvent.DelayAfter > 0)
                        {
                            //sleep and leave the app open
                            _log.Trace($"Sleep after for {timelineEvent.DelayAfter}");
                            Thread.Sleep(timelineEvent.DelayAfter - writeSleep);
                        }
                        
                        // close power point and dispose reference
                        powerApplication.Quit();
                        powerApplication.Dispose();
                        powerApplication = null;
                        
                        try
                        {
                            Marshal.ReleaseComObject(powerApplication);
                        }
                        catch
                        {
                            // ignore
                        }

                        try
                        {
                            Marshal.FinalReleaseComObject(powerApplication);
                        }
                        catch
                        {
                            // ignore
                        }

                        GC.Collect();
                    }
                    catch (Exception e)
                    {
                        _log.Debug(e);
                    }
                    finally
                    {
                        Thread.Sleep(5000);
                    }
                }
            }
            catch (Exception e)
            {
                _log.Debug(e);
            }
            finally
            {
                KillApp();
                _log.Trace("PowerPoint closing...");
            }
        }
    }
}


