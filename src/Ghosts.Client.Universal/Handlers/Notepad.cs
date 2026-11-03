// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Client.Universal.Infrastructure;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using System.Diagnostics;
using Ghosts.Client.Universal.Infrastructure;

namespace Ghosts.Client.Universal.Handlers;

public class Notepad(Timeline entireTimeline, TimelineHandler timelineHandler, CancellationToken cancellationToken)
    : BaseHandler(entireTimeline, timelineHandler, cancellationToken)
{
    private int _jitterFactor;
    private int _executionProbability = 100;
    private int _creationProbability = 25;
    private int _modificationProbability = 25;
    private int _deletionProbability = 25;
    private int _viewProbability = 25;
    private string _inputDirectory;
    private string _outputDirectory;
    private int _paragraphsMin = 1;
    private int _paragraphsMax = 10;
    public Process NotepadProcess;

    protected override Task RunOnce()
    {
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                _log.Info("Word handler automation is not currently supported on this OS");
                return Task.CompletedTask;
            }
            ParseHandlerArgs();

            var probabilityList = new[] { _creationProbability, _modificationProbability, _deletionProbability, _viewProbability };
            var actionList = new[] { "create", "modify", "delete", "view" };

            foreach (var timelineEvent in Handler.TimeLineEvents)
            {
                Token.ThrowIfCancellationRequested();
                WorkingHours.Is(Handler);

                if (timelineEvent.DelayBeforeActual > 0) {
                    if (Token.WaitHandle.WaitOne(timelineEvent.DelayBeforeActual)) Token.ThrowIfCancellationRequested();
                }
                if (_random.Next(0, 100) >= _executionProbability)
                {
                    _log.Trace("Notepad:: Action skipped due to execution probability.");
                    if (timelineEvent.DelayAfterActual > 0) {
                        if (Token.WaitHandle.WaitOne(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, _jitterFactor))) Token.ThrowIfCancellationRequested();
                    }
                    continue;
                }

                var action = SelectActionFromProbabilities(probabilityList, actionList);
                if (action == null)
                {
                    _log.Trace("Notepad:: No action this cycle.");
                    if (timelineEvent.DelayAfterActual > 0) {
                        if (Token.WaitHandle.WaitOne(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, _jitterFactor))) Token.ThrowIfCancellationRequested();
                    }
                    continue;
                }

                // Fall back to create if there are no files for the chosen action
                if ((action == "modify" || action == "view") && GetRandomFile(_inputDirectory) == null)
                    action = "create";
                if (action == "delete" && GetRandomFile(_outputDirectory) == null)
                    action = "create";

                String filepath;
                switch (action)
                {
                    case "create":
                        filepath = DoCreateAction();
                        if (!StartNotePad(filepath)) return Task.CompletedTask;
                        break;
                    case "modify":
                        filepath = DoModifyAction();
                        if (!StartNotePad(filepath)) return Task.CompletedTask;
                        break;
                    case "delete":
                        DoDeleteAction();
                        break;
                    case "view":
                        filepath = DoViewAction();
                        if (!StartNotePad(filepath)) return Task.CompletedTask;
                        break;
                }

                Report(new ReportItem
                {
                    Handler = Handler.HandlerType.ToString(),
                    Command = action,
                    Arg = _outputDirectory,
                    Trackable = timelineEvent.TrackableId
                });

                if (timelineEvent.DelayAfterActual > 0) {
                    if (Token.WaitHandle.WaitOne(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, _jitterFactor))) Token.ThrowIfCancellationRequested();
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception e)
        {
            _log.Error(e);
        }

        return Task.CompletedTask;
    }

    private String DoCreateAction()
    {
        try
        {
            var fileName = GenerateRandomFileName();
            var filePath = Path.Combine(_outputDirectory, fileName);
            var text = GenerateRandomText();
            File.WriteAllText(filePath, text, Encoding.UTF8);
            _log.Trace($"Notepad:: File {filePath} created.");
            return filePath;
        }
        catch (Exception e)
        {
            if (e is ThreadAbortException || e is ThreadInterruptedException || e is OperationCanceledException)
            {
                throw;
            }
            _log.Trace("Notepad:: Error creating file.");
            _log.Error(e);
        }
        return null;
    }

    private String DoModifyAction()
    {
        try
        {
            var filePath = GetRandomFile(_outputDirectory);
            if (filePath == null) return null;
            var additionalText = GenerateRandomText();
            File.AppendAllText(filePath, Environment.NewLine + additionalText, Encoding.UTF8);
            _log.Trace($"Notepad:: File {filePath} modified.");
            return filePath;
        }
        catch (Exception e)
        {
            if (e is ThreadAbortException || e is ThreadInterruptedException || e is OperationCanceledException)
            {
                throw;
            }
            _log.Trace("Notepad:: Error modifying file.");
            _log.Error(e);
        }
        return null;
    }

    private void DoDeleteAction()
    {
        try
        {
            var filePath = GetRandomFile(_outputDirectory);
            if (filePath == null) return;
            File.Delete(filePath);
            _log.Trace($"Notepad:: File deleted {filePath}.");
        }
        catch (Exception e)
        {
            if (e is ThreadAbortException || e is ThreadInterruptedException || e is OperationCanceledException)
            {
                throw;
            }
            _log.Trace("Notepad:: Error deleting file.");
            _log.Error(e);
        }
    }

    private String DoViewAction()
    {
        try
        {
            var filePath = GetRandomFile(_inputDirectory);
            if (filePath == null) return null;
            var content = File.ReadAllText(filePath, Encoding.UTF8);
            _log.Trace($"Notepad:: File viewed {filePath} ({content.Length} chars).");
            return filePath;
        }
        catch (Exception e)
        {
            if (e is ThreadAbortException || e is ThreadInterruptedException || e is OperationCanceledException)
            {
                throw;
            }
            _log.Trace("Notepad:: Error viewing file.");
            _log.Error(e);
        }
        return null;
    }

    private string GenerateRandomText()
    {
        var list = RandomText.GetDictionary.GetDictionaryList();
        using (var rt = new RandomText(list))
        {
            var numberParagraphs = _random.Next(_paragraphsMin, _paragraphsMax + 1);
            rt.AddContentParagraphs(numberParagraphs, 4, 10, 4, 15);
            var formattedContent = rt.FormatContent(60, 90);
            return formattedContent;
        }
    }

    private static String GenerateRandomFileName()
    {
        return $"{RandomFilename.Generate()}.txt";
    }

    private static string GetRandomFile(string directory)
    {
        try
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return null;
            var files = Directory.GetFiles(directory, "*.txt");
            if (files.Length == 0) return null;
            return files[_random.Next(files.Length)];
        }
        catch (Exception e)
        {
            if (e is ThreadAbortException || e is ThreadInterruptedException || e is OperationCanceledException)
            {
                throw;
            }
            return null;
        }
    }

    private void ParseHandlerArgs()
    {
        if (Handler.HandlerArgs == null) return;

        if (Handler.HandlerArgs.TryGetValue("execution-probability", out var ep))
        {
            if (int.TryParse(ep.ToString(), out var val) && val >= 0 && val <= 100)
                _executionProbability = val;
        }

        if (Handler.HandlerArgs.TryGetValue("creation-probability", out var cp))
        {
            if (int.TryParse(cp.ToString(), out var val) && val >= 0 && val <= 100)
                _creationProbability = val;
        }

        if (Handler.HandlerArgs.TryGetValue("modification-probability", out var mp))
        {
            if (int.TryParse(mp.ToString(), out var val) && val >= 0 && val <= 100)
                _modificationProbability = val;
        }

        if (Handler.HandlerArgs.TryGetValue("deletion-probability", out var dp))
        {
            if (int.TryParse(dp.ToString(), out var val) && val >= 0 && val <= 100)
                _deletionProbability = val;
        }

        if (Handler.HandlerArgs.TryGetValue("view-probability", out var vp))
        {
            if (int.TryParse(vp.ToString(), out var val) && val >= 0 && val <= 100)
                _viewProbability = val;
        }

        var probSum = _creationProbability + _modificationProbability + _deletionProbability + _viewProbability;
        if (probSum > 100 || probSum == 0)
        {
            _log.Trace("Notepad:: Probability sum invalid, resetting to defaults (25 each).");
            _creationProbability = 25;
            _modificationProbability = 25;
            _deletionProbability = 25;
            _viewProbability = 25;
        }

        _outputDirectory = TempFiles.GetGhostsTempPath();
        if (Handler.HandlerArgs.TryGetValue("output-directory", out var od))
        {
            var expanded = Environment.ExpandEnvironmentVariables(od.ToString());
            if (!string.IsNullOrEmpty(expanded))
                _outputDirectory = expanded;
        }

        _inputDirectory = TempFiles.GetGhostsTempPath();
        if (Handler.HandlerArgs.TryGetValue("input-directory", out var id))
        {
            var expanded = Environment.ExpandEnvironmentVariables(id.ToString());
            if (!string.IsNullOrEmpty(expanded))
                _inputDirectory = expanded;
        }

        if (!Directory.Exists(_outputDirectory))
            Directory.CreateDirectory(_outputDirectory);
        if (!Directory.Exists(_inputDirectory))
            Directory.CreateDirectory(_inputDirectory);

        if (Handler.HandlerArgs.TryGetValue("min-paragraphs", out var minP))
        {
            if (int.TryParse(minP.ToString(), out var val) && val > 0)
                _paragraphsMin = val;
        }

        if (Handler.HandlerArgs.TryGetValue("max-paragraphs", out var maxP))
        {
            if (int.TryParse(maxP.ToString(), out var val) && val > 0)
                _paragraphsMax = val;
        }

        if (_paragraphsMax < _paragraphsMin)
            _paragraphsMax = _paragraphsMin;

        if (Handler.HandlerArgs.TryGetValue("delay-jitter", out var dj))
        {
            _jitterFactor = Jitter.JitterFactorParse(dj.ToString());
        }
    }

    private void CloseNotePad()
    {
        //do our best at cleaning this up
        if (this.NotepadProcess == null || this.NotepadProcess.HasExited) return;
        try
        {
            //close the running process
            this.NotepadProcess.CloseMainWindow();
            if (Token.WaitHandle.WaitOne(1000)) Token.ThrowIfCancellationRequested();
            if (!this.NotepadProcess.HasExited)
            {
                this.NotepadProcess.Kill();   //may have a dialog window up
                if (Token.WaitHandle.WaitOne(1000)) Token.ThrowIfCancellationRequested();
            }
            this.NotepadProcess.Close();  //free resources
            if (Token.WaitHandle.WaitOne(1000)) Token.ThrowIfCancellationRequested();

        }
        catch (Exception e)
        {
            if (e is ThreadAbortException || e is ThreadInterruptedException || e is OperationCanceledException)
            {
                throw;
            }
            _log.Trace("Notepad:: Error closing Notepad process.");
            _log.Error(e);
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
                _log.Trace("Notepad:: Unable to start Notepad process, handler exiting.");
                return false;
            }
            if (Token.WaitHandle.WaitOne(1000)) Token.ThrowIfCancellationRequested();
        }
        catch (Exception e)
        {
            if (e is ThreadAbortException || e is ThreadInterruptedException || e is OperationCanceledException)
            {
                throw;
            }
            _log.Trace("Notepad:: Unable to start Notepad process, handler exiting.");
            _log.Error(e);
            return false;
        }
        return true;
    }

}
