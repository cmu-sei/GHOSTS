// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Client.Code;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Microsoft.Office.Interop.Excel;
using NetOffice.OfficeApi.Tools.Utils;
using NLog;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
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
                            var pids = ProcessManager.GetPids(ProcessManager.ProcessNames.Excel).ToList();
                            if (pids.Count > timeline.TimeLineHandlers.Count(o => o.HandlerType == HandlerType.Excel))
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
                foreach (TimelineEvent timelineEvent in handler.TimeLineEvents)
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
                            var pids = ProcessManager.GetPids(ProcessManager.ProcessNames.Excel).ToList();
                            if (pids.Count > timeline.TimeLineHandlers.Count(o => o.HandlerType == HandlerType.Excel))
                            {
                                return;
                            }
                        }

                        // start excel and turn off msg boxes
                        Excel.Application excelApplication = new Excel.Application
                        {
                            DisplayAlerts = false,
                            Visible = true
                        };

                        try
                        {
                            excelApplication.WindowState = XlWindowState.xlMinimized;
                            foreach (Excel.Workbook item in excelApplication.Workbooks)
                            {
                                item.Windows[1].WindowState = XlWindowState.xlMinimized;
                            }
                        }
                        catch (Exception e)
                        {
                            _log.Trace($"Could not minimize: {e}");
                        }

                        // create a utils instance, not need for but helpful to keep the lines of code low
                        CommonUtils utils = new CommonUtils(excelApplication);

                        _log.Trace("Excel adding workbook");
                        // add a new workbook
                        Excel.Workbook workBook = excelApplication.Workbooks.Add();
                        _log.Trace("Excel adding worksheet");
                        Excel.Worksheet workSheet = (Excel.Worksheet) workBook.Worksheets[1];

                        // draw back color and perform the BorderAround method
                        workSheet.Range("$B2:$B5").Interior.Color = utils.Color.ToDouble(Color.DarkGreen);
                        workSheet.Range("$B2:$B5").BorderAround(XlLineStyle.xlContinuous, XlBorderWeight.xlMedium,
                            XlColorIndex.xlColorIndexAutomatic);

                        // draw back color and border the range explicitly
                        workSheet.Range("$D2:$D5").Interior.Color = utils.Color.ToDouble(Color.DarkGreen);
                        workSheet.Range("$D2:$D5")
                            .Borders[(Excel.Enums.XlBordersIndex) XlBordersIndex.xlInsideHorizontal]
                            .LineStyle = XlLineStyle.xlDouble;
                        workSheet.Range("$D2:$D5")
                            .Borders[(Excel.Enums.XlBordersIndex) XlBordersIndex.xlInsideHorizontal]
                            .Weight = 4;
                        workSheet.Range("$D2:$D5")
                            .Borders[(Excel.Enums.XlBordersIndex) XlBordersIndex.xlInsideHorizontal]
                            .Color = utils.Color.ToDouble(Color.Black);

                        Thread.Sleep(180000); //wait 3 minutes

                        workSheet.Cells[1, 1].Value = "We have 2 simple shapes created.";

                        Thread.Sleep(3000);

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

                        string path = $"{dir}\\{rand}.xlsx";

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
                            _log.Error($"Excel file delete exception: {e}");
                        }

                        _log.Trace($"Excel saving to path - {path}");
                        workBook.SaveAs(path);
                        FileListing.Add(path);
                        Report(handler.HandlerType.ToString(), timelineEvent.Command,
                            timelineEvent.CommandArgs[0].ToString());

                        if (timelineEvent.DelayAfter > 0)
                        {
                            //sleep and leave the app open
                            _log.Trace($"Sleep after for {timelineEvent.DelayAfter}");
                            Thread.Sleep(timelineEvent.DelayAfter);
                        }

                        // close excel and dispose reference
                        excelApplication.Quit();
                        excelApplication.Dispose();

                        try
                        {
                            if (excelApplication != null)
                            {
                                System.Runtime.InteropServices.Marshal.ReleaseComObject(excelApplication);
                            }
                        }
                        catch (Exception e)
                        {
                            _log.Error($"Excel com release exception: {e}");
                        }

                        excelApplication = null;
                        workBook = null;
                        workSheet = null;

                        GC.Collect();
                    }
                    catch (Exception e)
                    {
                        _log.Error($"Excel handler exception: {e}");
                    }
                    finally
                    {
                        Thread.Sleep(3000);
                    }
                }
            }
            catch (Exception e)
            {
                _log.Error($"Excel execute events exception: {e}");

            }
            finally
            {
                KillApp();
                FileListing.FlushList();
                _log.Trace($"Excel closing after successfully kill and flush");
            }
        }
    }
}
