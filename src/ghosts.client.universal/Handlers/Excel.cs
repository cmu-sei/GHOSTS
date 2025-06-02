// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Domain;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Client.Infrastructure;
using Ghosts.Client.Universal.Infrastructure;
using Ghosts.Domain.Code;
using Ghosts.Domain.Code.Helpers;
using Newtonsoft.Json;

namespace Ghosts.Client.Universal.Handlers;

public class ExcelHandler(Timeline entireTimeline, TimelineHandler timelineHandler, CancellationToken cancellationToken)
    : BaseHandler(entireTimeline, timelineHandler, cancellationToken)
{
    protected override Task RunOnce()
    {
        if (!OperatingSystem.IsWindows())
        {
            _log.Info("Word handler automation is not currently supported on this OS");
            return Task.CompletedTask;
        }

        _log.Info("Starting Word handler automation...");

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
                    _log.Trace($"Word event - {timelineEvent}");
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

                    var applicationType = Type.GetTypeFromProgID("Excel.Application");
                    dynamic officeApplication = Activator.CreateInstance(applicationType);
                    officeApplication.Visible = true;
                    dynamic document = null;

                    if (OfficeHelpers.ShouldOpenExisting(handler))
                    {
                        document = officeApplication.Workbooks.Open(FileListing.GetRandomFile(handler.HandlerType));
                        _log.Trace($"{handler.HandlerType} opening existing file: {document.FullName}");
                    }

                    if (document == null)
                    {
                        document = officeApplication.Workbooks.Add();
                        _log.Trace($"{handler.HandlerType} adding new...");
                    }

                    for (var i = 0; i < _random.Next(1, 8); i++)
                        document.Worksheets.Add(Type.Missing, document.Worksheets[document.Worksheets.Count]);

                    var workSheet = document.Worksheets[1];

                    for (var i = 2; i < 10; i++)
                    {
                        for (var j = 1; j < 10; j++)
                        {
                            if (_random.Next(0, 30) != 1) // 1 in x cells are blank
                            {
                                workSheet.Cells[i, j].Value = _random.Next(0, 9999);
                                workSheet.Cells[i, j].Dispose();
                            }
                        }
                    }

                    try
                    {
                        const int xlMinimized = 2;
                        officeApplication.WindowState = xlMinimized;
                        foreach (var item in officeApplication.Workbooks)
                        {
                            item.Windows[1].WindowState = xlMinimized;
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
                        _log.Trace($"save-array exception: {e}");
                    }

                    defaultSaveDirectory = ApplicationDetails.GetPath(defaultSaveDirectory);

                    if (!Directory.Exists(defaultSaveDirectory))
                    {
                        Directory.CreateDirectory(defaultSaveDirectory);
                    }

                    var path = $"{defaultSaveDirectory}\\{rand}.xlsx";

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
                        {
                            File.Delete(path);
                        }
                    }
                    catch (Exception e)
                    {
                        _log.Error($"Excel file delete exception: {e}");
                    }

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
                        var pdfFileName = timelineEvent.CommandArgs.Contains("pdf-vary-filenames")
                            ? $"{defaultSaveDirectory}\\{RandomFilename.Generate()}.pdf"
                            : path.Replace(".xlsx", ".pdf");
                        // Save document into PDF Format
                        document.ExportAsFixedFormat(17,
                            pdfFileName);
                        // end save as pdf

                        Report(new ReportItem
                        {
                            Handler = handler.HandlerType.ToString(),
                            Command = timelineEvent.Command,
                            Arg = "pdf",
                            Trackable = timelineEvent.TrackableId
                        });
                        FileListing.Add(pdfFileName, handler.HandlerType);
                    }

                    if (_random.Next(100) < 50)
                        document.Close();

                    if (timelineEvent.DelayAfterActual > 0 && jitterFactor < 1)
                    {
                        //sleep and leave the app open
                        _log.Trace($"Sleep after for {timelineEvent.DelayAfterActual}");
                        Thread.Sleep(timelineEvent.DelayAfterActual.GetSafeSleepTime(writeSleep));
                    }

                    workSheet.Dispose();

                    document.Dispose();

                    // close excel and dispose reference
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

                    GC.Collect();
                }
                catch (ThreadAbortException e)
                {
                    _log.Error(e);
                    _log.Trace("Excel closing abnormally...");
                }
                catch (Exception e)
                {
                    _log.Error($"Excel handler exception: {e}");
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
            _log.Trace("Excel closing normally...");
        }

        return Task.CompletedTask;
    }
}
