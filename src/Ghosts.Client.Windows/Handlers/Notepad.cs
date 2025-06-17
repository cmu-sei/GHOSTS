// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Ghosts.Client.Infrastructure;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using System.Text;


namespace Ghosts.Client.Handlers
{
    public class Notepad : BaseHandler
    {
        //include FindWindowEx
        [DllImport("user32.dll")]
        public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        //include SendMessage
        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int uMsg, int wParam, string lParam);

        //this is a constant indicating the window that we want to send a text message
        const int WmSetText = 0X000C;

        public Process NotepadProcess;

        private int JitterFactor = 50;  //used with Jitter.JitterFactorDelay
        private int ExecutionProbability = 100;  //probability that an action is taken this cycle
        private int DeletionProbability = 0;
        private int ModificationProbability = 0;
        private int ViewProbability = 0;
        private int CreationProbability = 0;
        private string InputDirectory = null;
        private string OutputDirectory = null;
        private string TextGeneration = "random";
        private int ParagraphsMin = 1;
        private int ParagraphsMax = 10;


        public Notepad(TimelineHandler handler)
        {
            try
            {
                base.Init(handler);
                if (handler.Loop)
                {
                    while (true)
                    {
                        Ex(handler);
                    }
                }
                else
                {
                    Ex(handler);
                }
            }
            catch (ThreadAbortException)
            {
                CloseNotePad();

            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        public void Ex(TimelineHandler handler)
        {
            ParseHandlerArgs(handler);

            int[] probabilityList = { ViewProbability, DeletionProbability, CreationProbability, ModificationProbability };
            string[] actionList = { "view", "delete", "create", "modify" };

            //arguments parsed. Enter loop that does not exit unless forced exit, which stops handler.
            while (true)
            {
                //Currently,the only supported timeline command is random
                //in which everything is done by probabilities.
                //For 'random', CommandArgs is not used.

                foreach (var timelineEvent in handler.TimeLineEvents)
                {
                    WorkingHours.Is(handler);
                    if (timelineEvent.DelayBeforeActual > 0)
                        Thread.Sleep(timelineEvent.DelayBeforeActual);
                    switch (timelineEvent.Command)
                    {
                        case "random":

                            if (ExecutionProbability < _random.Next(0, 101))
                            {
                                Log.Trace("Notepad:: Action skipped this due to execution probability.");
                                continue;
                            }
                            //decided what to do in this activity cycle
                            var action = SelectActionFromProbabilities(probabilityList, actionList);
                            if (action == null)
                            {
                                Log.Trace("Notepad:: No action this cycle.");
                            }
                            else
                            {
                                var infile = GetRandomFile(InputDirectory, "*.txt");
                                if ((action == "modify" || action == "view") && infile == null)
                                {
                                    action = "create";  //no txt files to view or modify
                                }
                                var outfile = GetRandomFile(OutputDirectory, "*.txt");
                                if (action == "delete" && outfile == null)
                                {
                                    action = "create"; //no txt files to delete
                                }
                                if (timelineEvent.DelayBeforeActual > 0)
                                    Thread.Sleep(timelineEvent.DelayBeforeActual);

                                switch (action)
                                {
                                    case "create":
                                        //create a file that does not exist, add content, save to new file name, open with Notepad
                                        while (true)
                                        {

                                            outfile = Path.Combine(OutputDirectory, SshSftpSupport.RandomString(5, 15) + ".txt");
                                            if (!File.Exists(outfile)) break;
                                        }
                                        
                                        DoCreateAction(outfile);
                                        if (!StartNotePad(outfile)) return;
                                        break;
                                    case "delete":
                                        //delete random file
                                        DoDeleteAction(outfile);
                                        break;
                                    case "modify":
                                        //open random existing file, replace content, save, open with notepad
                                        DoModifyAction(outfile);
                                        if (!StartNotePad(outfile)) return;
                                        break;
                                    case "view":
                                        if (!StartNotePad(infile)) return;
                                        //open notepad with random existing file, nothing else to do.
                                        break;

                                }
                                Report(new ReportItem { Handler = handler.HandlerType.ToString(), Command = action, Arg = "", Trackable = timelineEvent.TrackableId });
                            }
                            if (timelineEvent.DelayAfterActual > 0)
                                Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, JitterFactor));
                            CloseNotePad();  //closes notepad if it is open. New notepad will be opened on next cycle if needed

                            break;
                    }

                }
            }
        }
        private void DoModifyAction(string outfile)
        {
            try
            {
                string allText = "";
                // Open the stream and read it back.
                using (StreamReader sr = File.OpenText(outfile))
                {
                    string s = "";
                    while ((s = sr.ReadLine()) != null)
                    {
                        allText += s;
                    }
                }
                
                allText += GetRandomText(); // add text
                //now write all text to file
                using (FileStream fs = File.Create(outfile))
                {
                    
                    byte[] info = new UTF8Encoding(true).GetBytes(allText);
                    // Add some information to the file.
                    fs.Write(info, 0, info.Length);
                }

            }
            catch (ThreadAbortException)
            {
                throw;  //pass up
            }
            catch (Exception e)
            {
                Log.Trace($"Notepad:: Error modifying file {outfile}");
                Log.Trace(e);
            }
            return;
        }



        /// <summary>
        /// This creates the file on disk.
        /// </summary>
        /// <param name="outfile"></param>
        private void DoCreateAction(string outfile)
        {
            try
            {
                // Create the file, or overwrite if the file exists.
                using (FileStream fs = File.Create(outfile))
                {
                    var newText = GetRandomText();
                    byte[] info = new UTF8Encoding(true).GetBytes(newText);
                    // Add some information to the file.
                    fs.Write(info, 0, info.Length);
                }
                Log.Trace($"Notepad::  File {outfile} created.");
            }
            catch (ThreadAbortException)
            {
                throw;  //pass up
            }
            catch (Exception e)
            {
                Log.Trace($"Notepad:: Error creating file {outfile}");
                Log.Trace(e);
            }
        }


        private string GetRandomText()
        {
            var list = RandomText.GetDictionary.GetDictionaryList();
            using (var rt = new RandomText(list))
            {
                var numberParagraphs = _random.Next(ParagraphsMin, ParagraphsMax + 1);
                rt.AddContentParagraphs(numberParagraphs, 4, 10, 4, 15);
                var formattedContent = rt.FormatContent(60, 90);
                return formattedContent;
            }
        }

       
        private void DoDeleteAction(string target)
        {

            try
            {
                File.Delete(target);
                Log.Trace($"Notepad:: Deleted file {target}");
            }
            catch (ThreadAbortException)
            {
                throw;  //pass up
            }
            catch (Exception e)
            {
                Log.Trace($"Notepad:: Error deleting file {target}");
                Log.Trace(e);
            }
            return;
        }

        public string GetRandomFile(string targetDir, string pattern)
        {
            try
            {
                string[] filelist = Directory.GetFiles(targetDir, pattern);
                if (filelist.Length > 0) return filelist[_random.Next(0, filelist.Length)];
                else return null;
            }
            catch (ThreadAbortException)
            {
                throw;  //pass up
            }
            catch { } //ignore any errors
            return null;
        }


        private void ParseHandlerArgs(TimelineHandler handler)
        {
            if (handler.HandlerArgs.ContainsKey("execution-probability"))
            {
                int.TryParse(handler.HandlerArgs["execution-probability"].ToString(), out ExecutionProbability);
                if (!CheckProbabilityVar(handler.HandlerArgs["execution-probability"].ToString(), ExecutionProbability))
                {
                    ExecutionProbability = 100;
                }
            }
            if (handler.HandlerArgs.ContainsKey("deletion-probability"))
            {
                int.TryParse(handler.HandlerArgs["deletion-probability"].ToString(), out DeletionProbability);
                if (!CheckProbabilityVar(handler.HandlerArgs["deletion-probability"].ToString(), DeletionProbability))
                {
                    DeletionProbability = 0;
                }
            }
            if (handler.HandlerArgs.ContainsKey("view-probability"))
            {
                int.TryParse(handler.HandlerArgs["view-probability"].ToString(), out ViewProbability);
                if (!CheckProbabilityVar(handler.HandlerArgs["view-probability"].ToString(), ViewProbability))
                {
                    ViewProbability = 0;
                }
            }
            if (handler.HandlerArgs.ContainsKey("modification-probability"))
            {
                int.TryParse(handler.HandlerArgs["modification-probability"].ToString(), out ModificationProbability);
                if (!CheckProbabilityVar(handler.HandlerArgs["modification-probability"].ToString(), ModificationProbability))
                {
                    ModificationProbability = 0;
                }
            }
            if (handler.HandlerArgs.ContainsKey("creation-probability"))
            {
                int.TryParse(handler.HandlerArgs["creation-probability"].ToString(), out CreationProbability);
                if (!CheckProbabilityVar(handler.HandlerArgs["creation-probability"].ToString(), CreationProbability))
                {
                    CreationProbability = 0;
                }
            }

            if ((ViewProbability + DeletionProbability + ModificationProbability + CreationProbability) > 100)
            {
                Log.Trace($"Notepad:: The sum of the view/delete/create/modification probabilities is > 100 , using defaults.");
                setProbabilityDefaults();

            }

            if ((ViewProbability + DeletionProbability + ModificationProbability + CreationProbability) == 0)
            {
                Log.Trace($"Notepad:: The sum of the view/delete/create/modification probabilities == 0 , using defaults.");
                setProbabilityDefaults();
            }

            if (handler.HandlerArgs.ContainsKey("input-directory"))
            {
                string targetDir = handler.HandlerArgs["input-directory"].ToString();
                targetDir = Environment.ExpandEnvironmentVariables(targetDir);
                if (!Directory.Exists(targetDir))
                {
                    Log.Trace($"Notepad:: input directory {targetDir} does not exist, using browser downloads directory.");
                }
                else
                {
                    InputDirectory = targetDir;
                }
            }

            if (InputDirectory == null)
            {
                InputDirectory = KnownFolders.GetDownloadFolderPath();
            }

            if (handler.HandlerArgs.ContainsKey("output-directory"))
            {
                string targetDir = handler.HandlerArgs["output-directory"].ToString();
                targetDir = Environment.ExpandEnvironmentVariables(targetDir);
                if (!Directory.Exists(targetDir))
                {
                    Log.Trace($"Notepad:: output directory {targetDir} does not exist, using browser downloads directory.");
                }
                else
                {
                    OutputDirectory = targetDir;
                }
            }

            if (OutputDirectory == null)
            {
                OutputDirectory = KnownFolders.GetDownloadFolderPath();
            }

            if (handler.HandlerArgs.ContainsKey("text-generation"))
            {
                TextGeneration = handler.HandlerArgs["text-generation"].ToString();
            }

            if (handler.HandlerArgs.ContainsKey("min-paragraphs"))
            {
                int.TryParse(handler.HandlerArgs["min-paragraphs"].ToString(), out ParagraphsMin);
                if (ParagraphsMin < 0)
                {
                    ParagraphsMin = 1;
                }
            }

            if (handler.HandlerArgs.ContainsKey("max-paragraphs"))
            {
                int.TryParse(handler.HandlerArgs["max-paragraphs"].ToString(), out ParagraphsMax);
                if (ParagraphsMax < 0)
                {
                    ParagraphsMax = 10;
                }
            }

            if (ParagraphsMax < ParagraphsMin) ParagraphsMax = ParagraphsMin;

            if (handler.HandlerArgs.ContainsKey("delay-jitter"))
            {
                JitterFactor = Jitter.JitterFactorParse(handler.HandlerArgs["delay-jitter"].ToString());
                if (JitterFactor < 0) JitterFactor = 0;
            }
        }

        private void setProbabilityDefaults()
        {
            ViewProbability = 25;
            DeletionProbability = 25;
            CreationProbability = 25;
            ModificationProbability = 25;
        }

        private void CloseNotePad()
        {
            //do our best at cleaning this up
            if (this.NotepadProcess == null || this.NotepadProcess.HasExited) return;
            try
            {
                //close the running process
                this.NotepadProcess.CloseMainWindow();
                Thread.Sleep(1000);
                if (!this.NotepadProcess.HasExited)
                {
                    this.NotepadProcess.Kill();   //may have a dialog window up
                    Thread.Sleep(1000);
                }
                this.NotepadProcess.Close();  //free resources
                Thread.Sleep(1000);

            }
            catch (ThreadAbortException)
            {
                throw;  //pass up
            }
            catch
            {

            }
            this.NotepadProcess = null;
        }

        private bool StartNotePad(string fname)
        {
            if (this.NotepadProcess != null && !this.NotepadProcess.HasExited)
            {
                CloseNotePad();
            }
            try
            {
                if (fname == null)
                {
                    this.NotepadProcess = Process.Start("notepad.exe");
                }
                else
                {
                    this.NotepadProcess = Process.Start("notepad.exe", fname);
                }
                if (this.NotepadProcess == null)
                {
                    Log.Trace("Notepad:: Unable to start Notepad process, handler exiting.");
                    return false;
                }
                Thread.Sleep(1000);
            }
            catch (ThreadAbortException)
            {
                throw;  //pass up
            }
            catch (Exception e)
            {
                Log.Trace("Notepad:: Unable to start Notepad process, handler exiting.");
                Log.Trace(e);
                return false;
            }
            return true;
        }





        public void Paste(string text)
        {
            //getting notepad's textbox handle from the main window's handle
            //the textbox is called 'Edit'
            var notepadTextbox = FindWindowEx(NotepadProcess.MainWindowHandle, IntPtr.Zero, "Edit", null);
            //sending the message to the textbox
            Winuser.SetForegroundWindow(NotepadProcess.MainWindowHandle);
            SendMessage(notepadTextbox, WmSetText, 0, text);
        }
    }
}