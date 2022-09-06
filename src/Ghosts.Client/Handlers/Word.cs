// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Client.Infrastructure;
using Ghosts.Domain;
using NetOffice.WordApi.Enums;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Ghosts.Domain.Code.Helpers;
using Newtonsoft.Json;
using Word = NetOffice.WordApi;
using VB = Microsoft.VisualBasic;

namespace Ghosts.Client.Handlers
{
    public class WordHandler : BaseHandler
    {
        public WordHandler(Timeline timeline, TimelineHandler handler)
        {
            try
            {
                base.Init(handler);
                Log.Trace("Launching Word handler");

                if (handler.Loop)
                {
                    Log.Trace("Word loop");
                    while (true)
                    {
                        if (timeline != null)
                        {
                            System.Collections.Generic.List<int> processIds = ProcessManager.GetPids(ProcessManager.ProcessNames.Word).ToList();
                            if (processIds.Count > 2 && processIds.Count > timeline.TimeLineHandlers.Count(o => o.HandlerType == HandlerType.Word))
                            {
                                continue;
                            }
                        }

                        ExecuteEvents(timeline, handler);
                    }
                }
                else
                {
                    Log.Trace("Word single run");
                    KillApp();
                    ExecuteEvents(timeline, handler);
                    KillApp();
                }
            }
            catch (ThreadAbortException)
            {
                KillApp();
                Log.Trace("Thread aborted, Word closing...");
            }
            catch (Exception e)
            {
                Log.Error(e);
                KillApp();
            }
        }

        private static void KillApp()
        {
            ProcessManager.KillProcessAndChildrenByName(ProcessManager.ProcessNames.Word);
        }

        private void ExecuteEvents(Timeline timeline, TimelineHandler handler)
        {
            try
            {
                foreach (var timelineEvent in handler.TimeLineEvents)
                {
                    try
                    {
                        Log.Trace($"Word event - {timelineEvent}");
                        Infrastructure.WorkingHours.Is(handler);

                        if (timelineEvent.DelayBefore > 0)
                        {
                            Thread.Sleep(timelineEvent.DelayBefore);
                        }

                        if (timeline != null)
                        {
                            var processIds = ProcessManager.GetPids(ProcessManager.ProcessNames.Word).Count();
                            if (processIds > timeline.TimeLineHandlers.Count(o => o.HandlerType == HandlerType.Word))
                            {
                                return;
                            }
                        }

                        // start word and turn off msg boxes
                        using (var wordApplication = new Word.Application
                               {
                                   DisplayAlerts = WdAlertLevel.wdAlertsNone,
                                   Visible = true
                               })
                        {

                            // add a new document
                            var newDocument = wordApplication.Documents.Add();

                            try
                            {
                                wordApplication.WindowState = WdWindowState.wdWindowStateMinimize;
                                foreach (var item in wordApplication.Documents)
                                {
                                    item.Windows[1].WindowState = WdWindowState.wdWindowStateMinimize;
                                }
                            }
                            catch (Exception e)
                            {
                                Log.Trace($"Could not minimize: {e}");
                            }

                            // insert some text
                            var list = RandomText.GetDictionary.GetDictionaryList();
                            using (var rt = new RandomText(list))
                            {
                                rt.AddContentParagraphs(1, 50);
                                wordApplication.Selection.TypeText(rt.Content);
                            }

                            var writeSleep = ProcessManager.Jitter(100);
                            Thread.Sleep(writeSleep);

                            wordApplication.Selection.HomeKey(WdUnits.wdLine, WdMovementType.wdExtend);
                            wordApplication.Selection.Font.Color = GetWdColor(StylingExtensions.GetRandomColor());
                            wordApplication.Selection.Font.Bold = 1;
                            wordApplication.Selection.Font.Size = 12;

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
                                        break;
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                Log.Trace($"save-array exception: {e}");
                            }

                            if (!Directory.Exists(defaultSaveDirectory))
                            {
                                Directory.CreateDirectory(defaultSaveDirectory);
                            }

                            var path = $"{defaultSaveDirectory}\\{rand}.docx";

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
                                Log.Debug(e);
                            }

                            newDocument.Saved = true;
                            newDocument.SaveAs(path);

                            Report(handler.HandlerType.ToString(), timelineEvent.Command,
                                timelineEvent.CommandArgs[0].ToString());
                            FileListing.Add(path);

                            if (timelineEvent.CommandArgs.Contains("pdf"))
                            {
                                // Save document into PDF Format
                                object oMissing = System.Reflection.Missing.Value;
                                object outputFileName = timelineEvent.CommandArgs.Contains("pdf-vary-filenames")
                                    ? $"{defaultSaveDirectory}\\{RandomFilename.Generate()}.pdf"
                                    : path.Replace(".docx", ".pdf");
                                object fileFormat = WdSaveFormat.wdFormatPDF;

                                newDocument.SaveAs(outputFileName, fileFormat, oMissing, oMissing,
                                    oMissing, oMissing, oMissing, oMissing,
                                    oMissing, oMissing, oMissing, oMissing,
                                    oMissing, oMissing, oMissing, oMissing);
                                // end save as pdf
                                Report(handler.HandlerType.ToString(), timelineEvent.Command, "pdf");
                                FileListing.Add(outputFileName.ToString());
                            }

                            if (_random.Next(100) < 50)
                                newDocument.Close();

                            if (timelineEvent.DelayAfter > 0)
                            {
                                //sleep and leave the app open
                                Log.Trace($"Sleep after for {timelineEvent.DelayAfter}");
                                Thread.Sleep(timelineEvent.DelayAfter - writeSleep);
                            }

                            newDocument.Dispose();
                            newDocument = null;

                            wordApplication.Quit();

                            try
                            {
                                wordApplication.Dispose();
                            }
                            catch
                            {
                                // ignore
                            }
                            
                            try
                            {
                                Marshal.ReleaseComObject(wordApplication);
                            }
                            catch
                            {
                                // ignore
                            }

                            try
                            {
                                Marshal.FinalReleaseComObject(wordApplication);
                            }
                            catch
                            {
                                // ignore
                            }
                        }

                        GC.Collect();
                    }
                    catch (ThreadAbortException)
                    {
                        KillApp();
                        Log.Trace("Word closing...");
                    }
                    catch (Exception e)
                    {
                        Log.Debug(e);
                    }
                    finally
                    {
                        Thread.Sleep(5000);
                    }
                }
            }
            catch (ThreadAbortException)
            {
                //ignore
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
            finally
            {
                KillApp();
                Log.Trace("Word closing...");
            }
        }


        private WdColor GetWdColor(Color color)
        {
            var rgbColor = VB.Information.RGB(color.R, color.G, color.B);
            var wdColor = (WdColor)rgbColor;
            return wdColor;
        }
    }
}
