// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Drawing;
using System.IO;
using System.Threading;
using Ghosts.Client.Code;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Microsoft.Office.Interop.Excel;
using NetOffice.OfficeApi.Tools.Utils;
using NLog;
using Excel = NetOffice.ExcelApi;

namespace Ghosts.Client.Handlers
{
    public class ExcelHandler : BaseHandler
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public ExcelHandler(TimelineHandler handler)
        {
            _log.Trace("Launching Excel handler");
            try
            {
                if (handler.Loop)
                {
                    _log.Trace("Excel loop");
                    while (true)
                    {
                        KillApp();
                        ExecuteEvents(handler);
                        KillApp();
                    }
                }
                else
                {
                    _log.Trace("Excel single run");
                    KillApp();
                    ExecuteEvents(handler);
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
            ProcessManager.KillProcessAndChildrenByName(ProcessManager.ProcessNames.Excel);
        }

        private void ExecuteEvents(TimelineHandler handler)
        {
            try
            {
                foreach (var timelineEvent in handler.TimeLineEvents)
                {
                    _log.Trace($"Excel event - {timelineEvent}");
                    WorkingHours.Is(handler);

                    if (timelineEvent.DelayBefore > 0)
                        Thread.Sleep(timelineEvent.DelayBefore);

                    // start excel and turn off msg boxes
                    Excel.Application excelApplication = new Excel.Application();
                    excelApplication.DisplayAlerts = false;
                    excelApplication.Visible = true;

                    // create a utils instance, not need for but helpful to keep the lines of code low
                    CommonUtils utils = new CommonUtils(excelApplication);

                    _log.Trace("Excel adding workbook");
                    // add a new workbook
                    var workBook = excelApplication.Workbooks.Add();
                    _log.Trace("Excel adding worksheet");
                    var workSheet = (Excel.Worksheet) workBook.Worksheets[1];

                    // draw back color and perform the BorderAround method
                    workSheet.Range("$B2:$B5").Interior.Color = utils.Color.ToDouble(Color.DarkGreen);
                    workSheet.Range("$B2:$B5").BorderAround(XlLineStyle.xlContinuous, XlBorderWeight.xlMedium,
                        XlColorIndex.xlColorIndexAutomatic);

                    // draw back color and border the range explicitly
                    workSheet.Range("$D2:$D5").Interior.Color = utils.Color.ToDouble(Color.DarkGreen);
                    workSheet.Range("$D2:$D5").Borders[(Excel.Enums.XlBordersIndex) XlBordersIndex.xlInsideHorizontal]
                        .LineStyle = XlLineStyle.xlDouble;
                    workSheet.Range("$D2:$D5").Borders[(Excel.Enums.XlBordersIndex) XlBordersIndex.xlInsideHorizontal]
                        .Weight = 4;
                    workSheet.Range("$D2:$D5").Borders[(Excel.Enums.XlBordersIndex) XlBordersIndex.xlInsideHorizontal]
                        .Color = utils.Color.ToDouble(Color.Black);

                    Thread.Sleep(180000); //wait 3 minutes

                    workSheet.Cells[1, 1].Value = "We have 2 simple shapes created.";

                    Thread.Sleep(3000);

                    var rand = RandomFilename.Generate();

                    var dir = timelineEvent.CommandArgs[0];
                    if (dir.Contains("%"))
                        dir = Environment.ExpandEnvironmentVariables(dir);
                    if (Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

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
                            File.Delete(path);
                    }
                    catch (Exception e)
                    {
                        _log.Debug(e);
                    }

                    _log.Trace($"Excel saving to path - {path}");
                    workBook.SaveAs(path);
                    FileListing.Add(path);
                    this.Report(handler.HandlerType.ToString(), timelineEvent.Command, timelineEvent.CommandArgs[0]);

                    // close excel and dispose reference
                    excelApplication.Quit();
                    excelApplication.Dispose();

                    try
                    {
                        if (excelApplication != null)
                            System.Runtime.InteropServices.Marshal.ReleaseComObject(excelApplication);
                    }
                    catch
                    {
                    }

                    excelApplication = null;
                    workBook = null;
                    workSheet = null;

                    GC.Collect();

                    KillApp();
                    FileListing.FlushList();
                }
            }
            catch (Exception e)
            {
                _log.Error(e);

            }
            finally
            {
                KillApp();
                FileListing.FlushList();
            }
        }
    }
}
