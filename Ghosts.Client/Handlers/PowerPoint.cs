// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.IO;
using System.Threading;
using Ghosts.Client.Code;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using NetOffice.OfficeApi.Enums;
using NetOffice.PowerPointApi.Enums;
using NLog;
using Exception = System.Exception;
using PowerPoint = NetOffice.PowerPointApi;
using PpSlideLayout = NetOffice.PowerPointApi.Enums.PpSlideLayout;

namespace Ghosts.Client.Handlers
{
    public class PowerPointHandler : BaseHandler
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public PowerPointHandler(TimelineHandler handler)
        {
            _log.Trace("Launching PowerPoint handler");
            try
            {
                if (handler.Loop)
                {
                    _log.Trace("PowerPoint loop");
                    while (true)
                    {
                        KillApp();
                        ExecuteEvents(handler);
                        KillApp();
                    }
                }
                else
                {
                    _log.Trace("PowerPoint single run");
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
            ProcessManager.KillProcessAndChildrenByName(ProcessManager.ProcessNames.PowerPoint);
        }

        private void ExecuteEvents(TimelineHandler handler)
        {
            try
            {
                foreach (var timelineEvent in handler.TimeLineEvents)
                {
                    _log.Trace($"PowerPoint event - {timelineEvent}");
                    WorkingHours.Is(handler);

                    if (timelineEvent.DelayBefore > 0)
                        Thread.Sleep(timelineEvent.DelayBefore);

                    var powerApplication = new PowerPoint.Application();
                    powerApplication.DisplayAlerts = PpAlertLevel.ppAlertsNone;
                    powerApplication.Visible = MsoTriState.msoTrue;

                    // add a new presentation with one new slide
                    var presentation = powerApplication.Presentations.Add(MsoTriState.msoTrue);
                    presentation.Slides.Add(1, PpSlideLayout.ppLayoutClipArtAndVerticalText);

                    Thread.Sleep(180000); //wait 3 minutes

                    // save the document 
                    var rand = RandomFilename.Generate();

                    var dir = timelineEvent.CommandArgs[0];
                    if (dir.Contains("%"))
                        dir = Environment.ExpandEnvironmentVariables(dir);
                    if (Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    var path = $"{dir}\\{rand}.pptx";

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

                    presentation.SaveAs(path);
                    FileListing.Add(path);
                    this.Report(handler.HandlerType.ToString(), timelineEvent.Command, timelineEvent.CommandArgs[0]);

                    // close power point and dispose reference
                    try
                    {
                        powerApplication.Quit();
                    }
                    catch (Exception e)
                    {
                    }

                    try
                    {
                        powerApplication.Dispose();
                    }
                    catch (Exception e)
                    {
                    }

                    try
                    {
                        if (powerApplication != null)
                            System.Runtime.InteropServices.Marshal.ReleaseComObject(powerApplication);
                    }
                    catch
                    {
                    }
                    powerApplication = null;
                    presentation = null;
                    GC.Collect();

                    if (timelineEvent.DelayAfter > 0)
                        Thread.Sleep(timelineEvent.DelayAfter);
                }
            }
            catch (Exception e)
            {
                _log.Debug(e);
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


