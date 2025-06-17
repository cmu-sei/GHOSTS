// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Client.Infrastructure;
using Ghosts.Domain;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Ghosts.Domain.Code;
using Ghosts.Domain.Code.Helpers;
using NetOffice.ExcelApi.Enums;
using Newtonsoft.Json;
using Excel = NetOffice.ExcelApi;
namespace Ghosts.Client.Handlers;

public class ExcelHandler : BaseHandler
{
    public ExcelHandler(Timeline timeline, TimelineHandler handler)
    {
        base.Init(handler);
        Log.Trace("Launching Excel handler");

        try
        {
            if (handler.Loop)
            {
                Log.Trace("Excel loop");
                while (true)
                {
                    if (timeline != null)
                    {
                        var processIds = ProcessManager.GetPids(ProcessManager.ProcessNames.Excel).Count();
                        if (processIds > timeline.TimeLineHandlers.Count(o => o.HandlerType == HandlerType.Excel))
                        {
                            continue;
                        }
                    }

                    ExecuteEvents(timeline, handler);
                }
            }
            else
            {
                Log.Trace("Excel single run");
                KillApp();
                ExecuteEvents(timeline, handler);
                KillApp();
            }
        }
        catch (ThreadAbortException)
        {
            KillApp();
            Log.Trace("Thread aborted, Excel closing...");
        }
        catch (Exception e)
        {
            Log.Error($"Excel launch handler exception: {e}");
            KillApp();
        }
    }

    private static void KillApp()
    {
        ProcessManager.KillProcessAndChildrenByName(ProcessManager.ProcessNames.Excel);
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
                    Log.Trace($"Excel event - {timelineEvent}");
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
                        var processIds = ProcessManager.GetPids(ProcessManager.ProcessNames.Excel).ToList();
                        if (processIds.Count > 2 && processIds.Count >
                            timeline.TimeLineHandlers.Count(o => o.HandlerType == HandlerType.Excel))
                        {
                            return;
                        }
                    }

                    using (var excelApplication = new Excel.Application
                           {
                               DisplayAlerts = false,
                               Visible = true
                           })
                    {
                        Excel.Workbook document = null;
                        if (OfficeHelpers.ShouldOpenExisting(handler))
                        {
                            document = excelApplication.Workbooks.Open(FileListing.GetRandomFile(handler.HandlerType));
                            Log.Trace($"{handler.HandlerType} opening existing file: {document.FullName}");
                        }
                        if (document == null)
                        {
                            document = excelApplication.Workbooks.Add();
                            Log.Trace($"{handler.HandlerType} adding new...");

                        }

                        for(var i = 0; i < _random.Next(1,8); i++)
                            document.Worksheets.Add(Type.Missing, document.Worksheets[document.Worksheets.Count]);

                        var workSheet = (Excel.Worksheet)document.Worksheets[1];

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
                            excelApplication.WindowState = XlWindowState.xlMinimized;
                            foreach (var item in excelApplication.Workbooks)
                            {
                                item.Windows[1].WindowState = XlWindowState.xlMinimized;
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

                        var path = $"{defaultSaveDirectory}\\{rand}.xlsx";

                        //if directory does not exist, create!
                        Log.Trace($"Checking directory at {path}");
                        var f = new FileInfo(path).Directory;
                        if (f == null)
                        {
                            Log.Trace($"Directory does not exist, creating directory at {f.FullName}");
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
                            Log.Error($"Excel file delete exception: {e}");
                        }

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
                            var pdfFileName = timelineEvent.CommandArgs.Contains("pdf-vary-filenames")
                                ? $"{defaultSaveDirectory}\\{RandomFilename.Generate()}.pdf"
                                : path.Replace(".xlsx", ".pdf");
                            // Save document into PDF Format
                            document.ExportAsFixedFormat(NetOffice.ExcelApi.Enums.XlFixedFormatType.xlTypePDF,
                                pdfFileName);
                            // end save as pdf
                            Report(new ReportItem { Handler = handler.HandlerType.ToString(), Command = timelineEvent.Command, Arg = "pdf", Trackable = timelineEvent.TrackableId });
                            FileListing.Add(pdfFileName, handler.HandlerType);
                        }

                        if (_random.Next(100) < 50)
                            document.Close();

                        if (timelineEvent.DelayAfterActual > 0 && jitterFactor < 1)
                        {
                            //sleep and leave the app open
                            Log.Trace($"Sleep after for {timelineEvent.DelayAfterActual}");
                            Thread.Sleep(timelineEvent.DelayAfterActual.GetSafeSleepTime(writeSleep));
                        }

                        workSheet.Dispose();
                        workSheet = null;

                        document.Dispose();
                        document = null;

                        // close excel and dispose reference
                        excelApplication.Quit();

                        try
                        {
                            excelApplication.Dispose();
                        }
                        catch
                        {
                            // ignore
                        }

                        try
                        {
                            Marshal.ReleaseComObject(excelApplication);
                        }
                        catch
                        {
                            // ignore
                        }

                        try
                        {
                            Marshal.FinalReleaseComObject(excelApplication);
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
                    Log.Trace("Excel closing abnormally...");
                    KillApp();
                }
                catch (Exception e)
                {
                    Log.Error($"Excel handler exception: {e}");
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
            Log.Error(e);
        }
        finally
        {
            Log.Trace("Excel closing normally...");
            KillApp();
        }
    }

    private string GetRandomRange()
    {
        var x = _random.Next(1, 40);
        var y = _random.Next(x, 50);
        var a1 = RandomText.GetRandomCapitalLetter();
        var a2 = RandomText.GetRandomCapitalLetter(a1);

        return $"${a1}{x}:${a2}{y}";
    }
}