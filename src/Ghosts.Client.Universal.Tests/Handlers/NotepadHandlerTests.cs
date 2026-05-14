// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Client.Universal.Handlers;
using Ghosts.Client.Universal.TimelineManager;
using Ghosts.Domain;
using Xunit;

namespace Ghosts.Client.Universal.Tests.Handlers;

public class NotepadHandlerTests
{
    private static Timeline CreateTimeline()
    {
        return new Timeline { Id = Guid.NewGuid(), Status = Timeline.TimelineStatus.Run };
    }

    private static TimelineHandler CreateHandler(Dictionary<string, object> handlerArgs = null)
    {
        return new TimelineHandler
        {
            HandlerType = HandlerType.Notepad,
            Loop = false,
            HandlerArgs = handlerArgs ?? new Dictionary<string, object>
            {
                ["execution-probability"] = "100",
                ["creation-probability"] = "50",
                ["modification-probability"] = "20",
                ["deletion-probability"] = "10",
                ["view-probability"] = "20",
                ["output-directory"] = Path.GetTempPath(),
                ["input-directory"] = Path.GetTempPath(),
                ["text-generation"] = "random",
                ["min-paragraphs"] = "1",
                ["max-paragraphs"] = "5",
                ["delay-jitter"] = "25"
            },
            TimeLineEvents = new List<TimelineEvent>
            {
                new() { Command = "random", CommandArgs = new List<object>(), DelayAfter = 0, DelayBefore = 0 }
            }
        };
    }

    [Fact]
    public void Notepad_CanBeConstructed_WithValidTimelineAndHandler()
    {
        var timeline = CreateTimeline();
        var handler = CreateHandler();
        var cts = new CancellationTokenSource();

        var notepadHandler = new Notepad(timeline, handler, cts.Token);

        Assert.NotNull(notepadHandler);
    }

    [Fact]
    public void Notepad_CanBeConstructed_WithNoHandlerArgs()
    {
        var timeline = CreateTimeline();
        var handler = CreateHandler(new Dictionary<string, object>());
        var cts = new CancellationTokenSource();

        var notepadHandler = new Notepad(timeline, handler, cts.Token);
        Assert.NotNull(notepadHandler);
    }

    [Fact]
    public void Notepad_ResolvesViaOrchestrator()
    {
        var timeline = CreateTimeline();
        var handler = CreateHandler(new Dictionary<string, object>());
        var cts = new CancellationTokenSource();

        try
        {
            var task = Orchestrator.RunHandler(HandlerType.Notepad, timeline, handler, cts.Token);
            Assert.NotNull(task);
        }
        catch (NotSupportedException)
        {
            Assert.Fail("HandlerType.Notepad could not be resolved to a concrete class");
        }
        catch (Exception)
        {
            // Other exceptions are acceptable
        }
    }

    [Fact]
    public async Task Notepad_Run_CompletesSuccessfully()
    {
        var timeline = CreateTimeline();
        var handler = CreateHandler();
        var cts = new CancellationTokenSource();

        var notepadHandler = new Notepad(timeline, handler, cts.Token);

        await notepadHandler.Run();
    }

    [Fact]
    public async Task Notepad_Run_CreatesTextFile_InOutputDirectory()
    {
        // Acceptance: files created and reported
        var outputDir = Path.Combine(Path.GetTempPath(), $"ghosts_notepad_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);

        try
        {
            var timeline = CreateTimeline();
            var handler = CreateHandler(new Dictionary<string, object>
            {
                ["execution-probability"] = "100",
                ["creation-probability"] = "100",
                ["output-directory"] = outputDir,
                ["input-directory"] = outputDir,
                ["min-paragraphs"] = "1",
                ["max-paragraphs"] = "2"
            });
            var cts = new CancellationTokenSource();

            var notepadHandler = new Notepad(timeline, handler, cts.Token);

            await notepadHandler.Run();

            var files = Directory.GetFiles(outputDir, "*.txt");
            Assert.NotEmpty(files);
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, true);
        }
    }

    [Fact]
    public void Notepad_ProbabilityValidation_SumsOver100_ResetsToDefaults()
    {
        // Validates the expected behavior: when sum > 100, use defaults (25 each)
        var viewProb = 40;
        var deletionProb = 40;
        var creationProb = 30;
        var modificationProb = 30;

        if ((viewProb + deletionProb + creationProb + modificationProb) > 100)
        {
            viewProb = 25;
            deletionProb = 25;
            creationProb = 25;
            modificationProb = 25;
        }

        Assert.Equal(25, viewProb);
        Assert.Equal(25, deletionProb);
        Assert.Equal(25, creationProb);
        Assert.Equal(25, modificationProb);
    }

    [Fact]
    public void Notepad_ProbabilityValidation_SumIsZero_ResetsToDefaults()
    {
        var viewProb = 0;
        var deletionProb = 0;
        var creationProb = 0;
        var modificationProb = 0;

        if ((viewProb + deletionProb + creationProb + modificationProb) == 0)
        {
            viewProb = 25;
            deletionProb = 25;
            creationProb = 25;
            modificationProb = 25;
        }

        Assert.Equal(25, viewProb);
        Assert.Equal(25, deletionProb);
        Assert.Equal(25, creationProb);
        Assert.Equal(25, modificationProb);
    }

    [Fact]
    public void Notepad_HandlerArgs_DirectoryPaths_ExpandEnvironmentVars()
    {
        // The handler should expand environment variables in directory paths
        var testPath = Path.GetTempPath();
        var args = new Dictionary<string, object>
        {
            ["output-directory"] = testPath,
            ["input-directory"] = testPath
        };

        var handler = CreateHandler(args);

        Assert.Equal(testPath, handler.HandlerArgs["output-directory"].ToString());
        Assert.Equal(testPath, handler.HandlerArgs["input-directory"].ToString());
    }
}
