// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Client.Infrastructure;
using Ghosts.Domain;
using NetOffice.OfficeApi.Enums;
using NetOffice.PowerPointApi.Enums;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Ghosts.Domain.Code;
using Ghosts.Domain.Code.Helpers;
using Newtonsoft.Json;
using PowerPoint = NetOffice.PowerPointApi;

namespace Ghosts.Client.Handlers;

public class PowerPointHandler : BaseHandler
{
    public PowerPointHandler(Timeline timeline, TimelineHandler handler)
    {
        base.Init(handler);
        Log.Trace("Launching PowerPoint handler");
        try
        {
            if (handler.Loop)
            {
                Log.Trace("PowerPoint loop");
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
                Log.Trace("PowerPoint single run");
                KillApp();
                ExecuteEvents(timeline, handler);
                KillApp();
            }
        }
        catch (ThreadAbortException)
        {
            KillApp();
            Log.Trace("Thread aborted, PowerPoint closing...");
        }
        catch (Exception e)
        {
            Log.Error(e);
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
            var jitterFactor = 0;
            var stayOpen = 0;
            if (handler.HandlerArgs.ContainsKey("stay-open"))
            {
                int.TryParse(handler.HandlerArgs["stay-open"].ToString(), out stayOpen);
            }
            if (handler.HandlerArgs.ContainsKey("delay-jitter"))
            {
                jitterFactor = Jitter.JitterFactorParse(handler.HandlerArgs["delay-jitter"].ToString());
            }

            foreach (var timelineEvent in handler.TimeLineEvents)
            {
                try
                {
                    Log.Trace($"PowerPoint event - {timelineEvent}");
                    WorkingHours.Is(handler);

                    if (timelineEvent.DelayBeforeActual > 0)
                    {
                        if (jitterFactor > 0)
                        {
                            Log.Trace($"DelayBefore, Sleeping with jitterfactor of {jitterFactor}% {timelineEvent.DelayBeforeActual}");
                            Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayBeforeActual, jitterFactor));
                        }
                        else
                        {
                            Log.Trace($"DelayBefore, Sleeping {timelineEvent.DelayBeforeActual}");
                            Thread.Sleep(timelineEvent.DelayBeforeActual);
                        }
                    }

                    if (timeline != null)
                    {
                        var processIds = ProcessManager.GetPids(ProcessManager.ProcessNames.PowerPoint).Count();
                        if (processIds > 2 && processIds > timeline.TimeLineHandlers.Count(o => o.HandlerType == HandlerType.PowerPoint))
                        {
                            return;
                        }
                    }

                    using (var powerApplication = new PowerPoint.Application
                           {
                               DisplayAlerts = PpAlertLevel.ppAlertsNone,
                               Visible = MsoTriState.msoTrue
                           })
                    {

                        try
                        {
                            powerApplication.WindowState = PpWindowState.ppWindowMinimized;
                            foreach (PowerPoint.Presentation item in powerApplication.Presentations)
                            {
                                item.Windows[1].WindowState = PpWindowState.ppWindowMinimized;
                            }
                        }
                        catch (Exception e)
                        {
                            Log.Trace($"Could not minimize: {e}");
                        }

                        var writeSleep = Jitter.Basic(100);
                        if (jitterFactor > 0)
                        {
                            writeSleep = Jitter.JitterFactorDelay((stayOpen / 4), jitterFactor);
                        }
                        Log.Trace($"Write sleep, Sleeping {writeSleep}");
                        Thread.Sleep(writeSleep);

                        PowerPoint.Presentation document = null;
                        if (OfficeHelpers.ShouldOpenExisting(handler))
                        {
                            document = powerApplication.Presentations.Open(FileListing.GetRandomFile(handler.HandlerType));
                            Log.Trace($"{handler.HandlerType} opening existing file: {document.FullName}");
                        }
                        if (document == null)
                        {
                            document = powerApplication.Presentations.Add(MsoTriState.msoTrue);
                            Log.Trace($"{handler.HandlerType} adding new...");
                        }

                        // add new slide
                        document.Slides.Add(1, PpSlideLayout.ppLayoutClipArtAndVerticalText);

                        // save the document 
                        var rand = RandomFilename.Generate();

                        var defaultSaveDirectory = timelineEvent.CommandArgs[0].ToString();
                        if (defaultSaveDirectory.Contains("%"))
                        {
                            defaultSaveDirectory = Environment.ExpandEnvironmentVariables(defaultSaveDirectory);
                        }

                        try
                        {
                            foreach (var key in timelineEvent.CommandArgs)
                            {
                                if (key.ToString().StartsWith("save-array:"))
                                {
                                    var savePathString =
                                        key.ToString().Replace("save-array:", "").Replace("'", "\"");
                                    savePathString =
                                        savePathString.Replace("\\",
                                            "/"); //can't seem to deserialize windows path \
                                    var savePaths = JsonConvert.DeserializeObject<string[]>(savePathString);
                                    defaultSaveDirectory =
                                        savePaths.PickRandom().Replace("/", "\\"); //put windows path back
                                    if (defaultSaveDirectory.Contains("%"))
                                    {
                                        defaultSaveDirectory = Environment.ExpandEnvironmentVariables(defaultSaveDirectory);
                                    }
                                    break;
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Log.Trace($"save-array exception: {e}");
                        }

                        defaultSaveDirectory = ApplicationDetails.GetPath(defaultSaveDirectory);

                        if (!Directory.Exists(defaultSaveDirectory))
                        {
                            Directory.CreateDirectory(defaultSaveDirectory);
                        }

                        var path = $"{defaultSaveDirectory}\\{rand}.pptx";

                        //if directory does not exist, create!
                        Log.Trace($"Checking directory at {path}");
                        var f = new FileInfo(path).Directory;
                        if (f == null)
                        {
                            Log.Trace($"Directory does not exist, creating directory at {path}");
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
                            Log.Debug(e);
                        }

                        Thread.Sleep(5000);
                        if (string.IsNullOrEmpty(document.Path))
                        {
                            document.SaveAs(path);
                            FileListing.Add(path, handler.HandlerType);
                            Log.Trace($"{handler.HandlerType} saving new file: {path}");
                        }
                        else
                        {
                            document.Save();
                            Log.Trace($"{handler.HandlerType} saving existing file: {document.FullName}");
                        }

                        Report(new ReportItem { Handler = handler.HandlerType.ToString(), Command = timelineEvent.Command, Arg = timelineEvent.CommandArgs[0].ToString(), Trackable = timelineEvent.TrackableId });

                        if (timelineEvent.CommandArgs.Contains("pdf"))
                        {
                            // Save document into PDF Format
                            var outputFileName = timelineEvent.CommandArgs.Contains("pdf-vary-filenames")
                                ? $"{defaultSaveDirectory}\\{RandomFilename.Generate()}.pdf"
                                : path.Replace(".pptx", ".pdf");
                            object fileFormat = PpSaveAsFileType.ppSaveAsPDF;

                            document.SaveAs(outputFileName, fileFormat, MsoTriState.msoCTrue);
                            // end save as pdf
                            Report(new ReportItem { Handler = handler.HandlerType.ToString(), Command = timelineEvent.Command, Arg = "pdf", Trackable = timelineEvent.TrackableId });
                            FileListing.Add(outputFileName, handler.HandlerType);
                        }

                        if (_random.Next(100) < 50)
                            document.Close();

                        if (timelineEvent.DelayAfterActual > 0 && jitterFactor < 1)
                        {
                            //sleep and leave the app open
                            Log.Trace($"Sleep after for {timelineEvent.DelayAfterActual}");
                            Thread.Sleep(timelineEvent.DelayAfterActual.GetSafeSleepTime(writeSleep));
                        }

                        document.Dispose();
                        document = null;

                        // close power point and dispose reference
                        powerApplication.Quit();

                        try
                        {
                            powerApplication.Dispose();
                        }
                        catch
                        {
                            // ignore
                        }
                            

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
                    }

                    GC.Collect();
                }
                catch (ThreadAbortException e)
                {
                    Log.Error(e);
                    Log.Trace("Powerpoint closing abnormally...");
                    KillApp();
                }
                catch (Exception e)
                {
                    Log.Debug(e);
                }
                finally
                {
                    if (timelineEvent.DelayAfterActual > 0 && jitterFactor > 0)
                    {
                        //sleep and leave the app open
                        Log.Trace($"Sleep after for {timelineEvent.DelayAfterActual} with jitter");
                        // Thread.Sleep(timelineEvent.DelayAfterActual - writeSleep);
                        Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, jitterFactor));
                    }
                    else
                    {
                        Thread.Sleep(5000);
                    }
                }
            }
        }
        catch (ThreadAbortException e)
        {
            Log.Error(e);
        }
        catch (Exception e)
        {
            Log.Debug(e);
        }
        finally
        {
            Log.Trace("PowerPoint closing normally...");
            KillApp();
        }
    }
}