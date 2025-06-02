// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Client.Infrastructure;
using Ghosts.Domain;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Client.Universal.Infrastructure;
using Ghosts.Domain.Code;
using Ghosts.Domain.Code.Helpers;
using Newtonsoft.Json;

namespace Ghosts.Client.Universal.Handlers;

public class PowerPointHandler(Timeline entireTimeline, TimelineHandler timelineHandler, CancellationToken cancellationToken)
    : BaseHandler(entireTimeline, timelineHandler, cancellationToken)
{
    protected override Task RunOnce()
    {
        if (!OperatingSystem.IsWindows())
        {
            _log.Info("PowerPoint handler automation is not currently supported on this OS");
            return Task.CompletedTask;
        }

        _log.Info("Starting PowerPoint handler automation...");

        var handler = this.Handler;

        try
        {
            var jitterFactor = 0;
            var stayOpen = 0;
            if (handler.HandlerArgs.TryGetValue("stay-open", out var arg))
            {
                int.TryParse(arg.ToString(), out stayOpen);
            }

            if (handler.HandlerArgs.TryGetValue("delay-jitter", out var handlerArg))
            {
                jitterFactor = Jitter.JitterFactorParse(handlerArg.ToString());
            }

            foreach (var timelineEvent in handler.TimeLineEvents)
            {
                try
                {
                    _log.Trace($"PowerPoint event - {timelineEvent}");
                    WorkingHours.Is(handler);

                    if (timelineEvent.DelayBeforeActual > 0)
                    {
                        if (jitterFactor > 0)
                        {
                            _log.Trace(
                                $"DelayBefore, Sleeping with jitterfactor of {jitterFactor}% {timelineEvent.DelayBeforeActual}");
                            Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayBeforeActual, jitterFactor));
                        }
                        else
                        {
                            _log.Trace($"DelayBefore, Sleeping {timelineEvent.DelayBeforeActual}");
                            Thread.Sleep(timelineEvent.DelayBeforeActual);
                        }
                    }

                    var applicationType = Type.GetTypeFromProgID("PowerPoint.Application");
                    dynamic officeApplication = Activator.CreateInstance(applicationType);
                    officeApplication.Visible = true;
                    dynamic document = null;

                    if (OfficeHelpers.ShouldOpenExisting(handler))
                    {
                        document = officeApplication.Documents.Open(FileListing.GetRandomFile(handler.HandlerType),
                            ReadOnly: false);
                        _log.Trace($"{handler.HandlerType} opening existing file: {document.FullName}");
                    }

                    if (document == null)
                    {
                        document = officeApplication.Documents.Add();
                        _log.Trace($"{handler.HandlerType} adding new...");
                    }

                    try
                    {
                        // Minimize the main Word application window
                        officeApplication.WindowState = 2; // 2 = wdWindowStateMinimize

                        // Optionally minimize all document windows (usually unnecessary)
                        foreach (var doc in officeApplication.Documents)
                        {
                            foreach (var win in doc.Windows)
                            {
                                win.WindowState = 2;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        _log.Trace($"Could not minimize: {e}");
                    }

                    var writeSleep = Jitter.Basic(100);
                    if (jitterFactor > 0)
                    {
                        writeSleep = Jitter.JitterFactorDelay((stayOpen / 4), jitterFactor);
                    }

                    _log.Trace($"Write sleep, Sleeping {writeSleep}");
                    Thread.Sleep(writeSleep);

                    // add new slide
                    document.Slides.Add(1, 26); // PpSlideLayout.ppLayoutClipArtAndVerticalText

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
                                    defaultSaveDirectory =
                                        Environment.ExpandEnvironmentVariables(defaultSaveDirectory);
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

                    var path = $"{defaultSaveDirectory}\\{rand}.pptx";

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
                    if (string.IsNullOrEmpty(document.Path))
                    {
                        document.SaveAs(path);
                        FileListing.Add(path, handler.HandlerType);
                        _log.Trace($"{handler.HandlerType} saving new file: {path}");
                    }
                    else
                    {
                        document.Save();
                        _log.Trace($"{handler.HandlerType} saving existing file: {document.FullName}");
                    }

                    Report(new ReportItem
                    {
                        Handler = handler.HandlerType.ToString(),
                        Command = timelineEvent.Command,
                        Arg = timelineEvent.CommandArgs[0].ToString(),
                        Trackable = timelineEvent.TrackableId
                    });

                    if (timelineEvent.CommandArgs.Contains("pdf"))
                    {
                        // Save document into PDF Format
                        var outputFileName = timelineEvent.CommandArgs.Contains("pdf-vary-filenames")
                            ? $"{defaultSaveDirectory}\\{RandomFilename.Generate()}.pdf"
                            : path.Replace(".pptx", ".pdf");
                        object fileFormat = 17; //PpSaveAsFileType.ppSaveAsPDF;

                        document.SaveAs(outputFileName, fileFormat, -1); //embedFonts = -1
                        // end save as pdf

                        Report(new ReportItem
                        {
                            Handler = handler.HandlerType.ToString(),
                            Command = timelineEvent.Command,
                            Arg = "pdf",
                            Trackable = timelineEvent.TrackableId
                        });
                        FileListing.Add(outputFileName, handler.HandlerType);
                    }

                    if (_random.Next(100) < 50)
                        document.Close();

                    if (timelineEvent.DelayAfterActual > 0 && jitterFactor < 1)
                    {
                        //sleep and leave the app open
                        _log.Trace($"Sleep after for {timelineEvent.DelayAfterActual}");
                        Thread.Sleep(timelineEvent.DelayAfterActual.GetSafeSleepTime(writeSleep));
                    }

                    document.Dispose();

                    // close ppt and dispose reference
                    officeApplication.Quit();

                    try
                    {
                        officeApplication.Dispose();
                    }
                    catch
                    {
                        // ignore
                    }


                    try
                    {
                        Marshal.ReleaseComObject(officeApplication);
                    }
                    catch
                    {
                        // ignore
                    }

                    try
                    {
                        Marshal.FinalReleaseComObject(officeApplication);
                    }
                    catch
                    {
                        // ignore
                    }
                }
                catch (ThreadAbortException e)
                {
                    _log.Error(e);
                    _log.Trace("Powerpoint closing abnormally...");
                }
                catch (Exception e)
                {
                    _log.Debug(e);
                }
                finally
                {
                    if (timelineEvent.DelayAfterActual > 0 && jitterFactor > 0)
                    {
                        //sleep and leave the app open
                        _log.Trace($"Sleep after for {timelineEvent.DelayAfterActual} with jitter");
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
            _log.Error(e);
        }
        catch (Exception e)
        {
            _log.Error(e);
        }
        finally
        {
            _log.Trace("Word closing normally...");
        }

        return Task.CompletedTask;
    }
}
