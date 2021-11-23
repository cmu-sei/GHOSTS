// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Client.Infrastructure;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Microsoft.Office.Interop.Excel;
using NLog;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Ghosts.Domain.Code.Helpers;
using NetOffice.ExcelApi.Tools;
using Excel = NetOffice.ExcelApi;
using XlWindowState = NetOffice.ExcelApi.Enums.XlWindowState;

namespace Ghosts.Client.Handlers
{
    public class ExcelHandler : BaseHandler
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public ExcelHandler(Timeline timeline, TimelineHandler handler)
        {
            _log.Trace("Launching Excel handler");
            try
            {
                if (handler.Loop)
                {
                    _log.Trace("Excel loop");
                    while (true)
                    {
                        if (timeline != null)
                        {
                            var processIds = ProcessManager.GetPids(ProcessManager.ProcessNames.Excel).ToList();
                            if (processIds.Count > timeline.TimeLineHandlers.Count(o => o.HandlerType == HandlerType.Excel))
                            {
                                continue;
                            }
                        }

                        ExecuteEvents(timeline, handler);
                    }
                }
                else
                {
                    _log.Trace("Excel single run");
                    KillApp();
                    ExecuteEvents(timeline, handler);
                    KillApp();
                }
            }
            catch (Exception e)
            {
                _log.Error($"Excel launch handler exception: {e}");
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
                foreach (var timelineEvent in handler.TimeLineEvents)
                {
                    try
                    {
                        _log.Trace($"Excel event - {timelineEvent}");
                        WorkingHours.Is(handler);

                        if (timelineEvent.DelayBefore > 0)
                        {
                            Thread.Sleep(timelineEvent.DelayBefore);
                        }

                        if (timeline != null)
                        {
                            var processIds = ProcessManager.GetPids(ProcessManager.ProcessNames.Excel).ToList();
                            if (processIds.Count > timeline.TimeLineHandlers.Count(o => o.HandlerType == HandlerType.Excel))
                            {
                                return;
                            }
                        }

                        // start excel and turn off msg boxes
                        var excelApplication = new Excel.Application
                        {
                            DisplayAlerts = false,
                        };

                        // create a utils instance, not need for but helpful to keep the lines of code low
                        var utils = new CommonUtils(excelApplication);

                        _log.Trace("Excel adding workbook");
                        // add a new workbook
                        var workBook = excelApplication.Workbooks.Add();
                        _log.Trace("Excel adding worksheet");
                        var workSheet = (Excel.Worksheet) workBook.Worksheets[1];


                        var list = RandomText.GetDictionary.GetDictionaryList();
                        var rt = new RandomText(list.ToArray());
                        rt.AddSentence(10);

                        workSheet.Cells[1, 1].Value = rt.Content;

                        var random = new Random();
                        for (var i = 2; i < 100; i++)
                        {
                            for (var j = 1; j < 100; j++)
                            {
                                if (random.Next(0, 20) != 1) // 1 in 20 cells are blank
                                    workSheet.Cells[i, j].Value = random.Next(0, 999999999);
                            }
                        }

                        for (var i = 0; i < random.Next(1,30); i++)
                        {
                            var range = GetRandomRange();
                            // draw back color and perform the BorderAround method
                            workSheet.Range(range).Interior.Color =
                                utils.Color.ToDouble(StylingExtensions.GetRandomColor());
                            workSheet.Range(range).BorderAround(XlLineStyle.xlContinuous, XlBorderWeight.xlMedium,
                                XlColorIndex.xlColorIndexAutomatic);

                            range = GetRandomRange();
                            // draw back color and border the range explicitly
                            workSheet.Range(range).Interior.Color =
                                utils.Color.ToDouble(StylingExtensions.GetRandomColor());
                            workSheet.Range(range)
                                .Borders[(Excel.Enums.XlBordersIndex) XlBordersIndex.xlInsideHorizontal]
                                .LineStyle = XlLineStyle.xlDouble;
                            workSheet.Range(range)
                                .Borders[(Excel.Enums.XlBordersIndex) XlBordersIndex.xlInsideHorizontal]
                                .Weight = 4;
                            workSheet.Range(range)
                                .Borders[(Excel.Enums.XlBordersIndex) XlBordersIndex.xlInsideHorizontal]
                                .Color = utils.Color.ToDouble(StylingExtensions.GetRandomColor());
                        }

                        var writeSleep = ProcessManager.Jitter(100);
                        Thread.Sleep(writeSleep);

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

                        var path = $"{dir}\\{rand}.xlsx";

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

                        _log.Trace($"Excel saving to path - {path}");
                        workBook.SaveAs(path);

                        FileListing.Add(path);
                        Report(handler.HandlerType.ToString(), timelineEvent.Command, timelineEvent.CommandArgs[0].ToString());

                        if (timelineEvent.CommandArgs.Contains("pdf"))
                        {
                            var pdfFileName = timelineEvent.CommandArgs.Contains("pdf-vary-filenames") ? $"{RandomFilename.Generate()}.pdf" : workBook.FullName.Replace(".xlsx", ".pdf");
                            // Save document into PDF Format
                            workBook.ExportAsFixedFormat(NetOffice.ExcelApi.Enums.XlFixedFormatType.xlTypePDF, pdfFileName);
                            // end save as pdf
                            Report(handler.HandlerType.ToString(), timelineEvent.Command, "pdf");
                            FileListing.Add(pdfFileName);
                        }

                        workBook.Close();

                        if (timelineEvent.DelayAfter > 0)
                        {
                            //sleep and leave the app open
                            _log.Trace($"Sleep after for {timelineEvent.DelayAfter}");
                            Thread.Sleep(timelineEvent.DelayAfter - writeSleep);
                        }

                        // close excel and dispose reference
                        excelApplication.Quit();
                        excelApplication.Dispose();
                        excelApplication = null;

                        workBook = null;
                        workSheet = null;

                        try
                        {
                            Marshal.ReleaseComObject(excelApplication);
                        }
                        catch { }

                        try
                        {
                            Marshal.FinalReleaseComObject(excelApplication);
                        }
                        catch { }

                        GC.Collect();
                    }
                    catch (Exception e)
                    {
                        _log.Error($"Excel handler exception: {e}");
                    }
                    finally
                    {
                        Thread.Sleep(5000);
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
                _log.Trace("Excel closing...");
            }
        }

        private string GetRandomRange()
        {
            var r = new Random();
            var x = r.Next(1, 40);
            var y = r.Next(x, 50);
            var a1 = RandomText.GetRandomCapitalLetter();
            var a2 = RandomText.GetRandomCapitalLetter(a1);

            return $"${a1}{x}:${a2}{y}";
        }
    }
}
