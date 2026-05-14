// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Domain;
using Ghosts.Domain.Code;

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

    private static readonly string[] Words =
    {
        "the", "quick", "brown", "fox", "jumps", "over", "lazy", "dog",
        "lorem", "ipsum", "dolor", "sit", "amet", "consectetur", "adipiscing",
        "elit", "sed", "do", "eiusmod", "tempor", "incididunt", "ut", "labore",
        "et", "dolore", "magna", "aliqua"
    };

    protected override Task RunOnce()
    {
        try
        {
            ParseHandlerArgs();

            var probabilityList = new[] { _creationProbability, _modificationProbability, _deletionProbability, _viewProbability };
            var actionList = new[] { "create", "modify", "delete", "view" };

            foreach (var timelineEvent in Handler.TimeLineEvents)
            {
                Token.ThrowIfCancellationRequested();
                WorkingHours.Is(Handler);

                if (timelineEvent.DelayBeforeActual > 0)
                    Thread.Sleep(timelineEvent.DelayBeforeActual);

                if (_random.Next(0, 100) >= _executionProbability)
                {
                    _log.Trace("Notepad:: Action skipped due to execution probability.");
                    if (timelineEvent.DelayAfterActual > 0)
                        Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, _jitterFactor));
                    continue;
                }

                var action = SelectActionFromProbabilities(probabilityList, actionList);
                if (action == null)
                {
                    _log.Trace("Notepad:: No action this cycle.");
                    if (timelineEvent.DelayAfterActual > 0)
                        Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, _jitterFactor));
                    continue;
                }

                // Fall back to create if there are no files for the chosen action
                if ((action == "modify" || action == "view") && GetRandomFile(_inputDirectory) == null)
                    action = "create";
                if (action == "delete" && GetRandomFile(_outputDirectory) == null)
                    action = "create";

                switch (action)
                {
                    case "create":
                        DoCreateAction();
                        break;
                    case "modify":
                        DoModifyAction();
                        break;
                    case "delete":
                        DoDeleteAction();
                        break;
                    case "view":
                        DoViewAction();
                        break;
                }

                Report(new ReportItem
                {
                    Handler = Handler.HandlerType.ToString(),
                    Command = action,
                    Arg = _outputDirectory,
                    Trackable = timelineEvent.TrackableId
                });

                if (timelineEvent.DelayAfterActual > 0)
                    Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, _jitterFactor));
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

    private void DoCreateAction()
    {
        try
        {
            var fileName = GenerateRandomFileName();
            var filePath = Path.Combine(_outputDirectory, fileName);
            var text = GenerateRandomText();
            File.WriteAllText(filePath, text, Encoding.UTF8);
            _log.Trace($"Notepad:: File {filePath} created.");
        }
        catch (Exception e)
        {
            _log.Trace("Notepad:: Error creating file.");
            _log.Error(e);
        }
    }

    private void DoModifyAction()
    {
        try
        {
            var filePath = GetRandomFile(_outputDirectory);
            if (filePath == null) return;
            var additionalText = GenerateRandomText();
            File.AppendAllText(filePath, Environment.NewLine + additionalText, Encoding.UTF8);
            _log.Trace($"Notepad:: File {filePath} modified.");
        }
        catch (Exception e)
        {
            _log.Trace("Notepad:: Error modifying file.");
            _log.Error(e);
        }
    }

    private void DoDeleteAction()
    {
        try
        {
            var filePath = GetRandomFile(_outputDirectory);
            if (filePath == null) return;
            File.Delete(filePath);
            _log.Trace($"Notepad:: Deleted file {filePath}.");
        }
        catch (Exception e)
        {
            _log.Trace("Notepad:: Error deleting file.");
            _log.Error(e);
        }
    }

    private void DoViewAction()
    {
        try
        {
            var filePath = GetRandomFile(_inputDirectory);
            if (filePath == null) return;
            var content = File.ReadAllText(filePath, Encoding.UTF8);
            _log.Trace($"Notepad:: Viewed file {filePath} ({content.Length} chars).");
        }
        catch (Exception e)
        {
            _log.Trace("Notepad:: Error viewing file.");
            _log.Error(e);
        }
    }

    private string GenerateRandomText()
    {
        var paragraphCount = _random.Next(_paragraphsMin, _paragraphsMax + 1);
        var sb = new StringBuilder();
        for (var p = 0; p < paragraphCount; p++)
        {
            if (p > 0) sb.AppendLine();
            var sentenceCount = _random.Next(3, 8);
            for (var s = 0; s < sentenceCount; s++)
            {
                var wordCount = _random.Next(5, 15);
                var sentence = new StringBuilder();
                for (var w = 0; w < wordCount; w++)
                {
                    if (w > 0) sentence.Append(' ');
                    sentence.Append(Words[_random.Next(Words.Length)]);
                }
                sentence[0] = char.ToUpper(sentence[0]);
                sentence.Append(". ");
                sb.Append(sentence);
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string GenerateRandomFileName()
    {
        var length = _random.Next(8, 16);
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        var sb = new StringBuilder(length);
        for (var i = 0; i < length; i++)
            sb.Append(chars[_random.Next(chars.Length)]);
        sb.Append(".txt");
        return sb.ToString();
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
        catch
        {
            return null;
        }
    }

    private static string SelectActionFromProbabilities(int[] probabilityList, string[] actionList)
    {
        var choice = _random.Next(0, 101);
        var startRange = 0;
        var index = 0;

        foreach (var probability in probabilityList)
        {
            if (probability > 0)
            {
                var endRange = startRange + probability;
                if (choice >= startRange && choice <= endRange) return actionList[index];
                startRange = endRange + 1;
            }
            index++;
        }

        return null;
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

        _outputDirectory = Path.GetTempPath();
        if (Handler.HandlerArgs.TryGetValue("output-directory", out var od))
        {
            var expanded = Environment.ExpandEnvironmentVariables(od.ToString());
            if (!string.IsNullOrEmpty(expanded))
                _outputDirectory = expanded;
        }

        _inputDirectory = Path.GetTempPath();
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
}
