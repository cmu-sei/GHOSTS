// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Client.Infrastructure;
using Ghosts.Domain;
using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Client.Universal.Infrastructure;
using Ghosts.Domain.Code;
using Ghosts.Domain.Code.Helpers;
using Newtonsoft.Json;

namespace Ghosts.Client.Universal.Handlers;

public class Word(Timeline entireTimeline, TimelineHandler timelineHandler, CancellationToken cancellationToken)
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

                    var applicationType = Type.GetTypeFromProgID("Word.Application");
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

                    // insert some text
                    var list = RandomText.GetDictionary.GetDictionaryList();
                    using (var rt = new RandomText(list))
                    {
                        rt.AddContentParagraphs(1, 50);
                        officeApplication.Selection.TypeText(rt.Content);
                    }

                    officeApplication.Selection.HomeKey(5, 1); //wdLine = 5 and wdExtend = 1
                    officeApplication.Selection.Font.Color = GetWdColor(StylingExtensions.GetRandomColor());
                    officeApplication.Selection.Font.Bold = 1;
                    officeApplication.Selection.Font.Size = 12;

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

                    var path = $"{defaultSaveDirectory}\\{rand}.docx";

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
                        _log.Debug(e);
                    }

                    document.Saved = true;
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
                        object oMissing = System.Reflection.Missing.Value;
                        object outputFileName = timelineEvent.CommandArgs.Contains("pdf-vary-filenames")
                            ? $"{defaultSaveDirectory}\\{RandomFilename.Generate()}.pdf"
                            : path.Replace(".docx", ".pdf");

                        document.SaveAs(outputFileName, 17, oMissing, oMissing, //17 = wdFormatPDF
                            oMissing, oMissing, oMissing, oMissing,
                            oMissing, oMissing, oMissing, oMissing,
                            oMissing, oMissing, oMissing, oMissing);
                        // end save as pdf
                        Report(new ReportItem
                        {
                            Handler = handler.HandlerType.ToString(),
                            Command = timelineEvent.Command,
                            Arg = "pdf",
                            Trackable = timelineEvent.TrackableId
                        });
                        FileListing.Add(outputFileName.ToString(), handler.HandlerType);
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
                    _log.Trace("Word closing abnormally...");
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

    private int GetWdColor(Color color)
    {
        return (color.B << 16) | (color.G << 8) | color.R;
    }
}
