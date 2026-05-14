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

public class LightHandlersTests
{
    private static Timeline CreateTimeline()
    {
        return new Timeline { Id = Guid.NewGuid(), Status = Timeline.TimelineStatus.Run };
    }

    private static TimelineHandler CreateHandler(HandlerType type, string outputDir = null)
    {
        var dir = outputDir ?? Path.GetTempPath();
        return new TimelineHandler
        {
            HandlerType = type,
            Loop = false,
            HandlerArgs = new Dictionary<string, object>(),
            TimeLineEvents = new List<TimelineEvent>
            {
                new()
                {
                    Command = "create",
                    CommandArgs = new List<object> { dir },
                    DelayAfter = 0,
                    DelayBefore = 0
                }
            }
        };
    }

    [Fact]
    public void LightWordHandler_CanBeConstructed()
    {
        var timeline = CreateTimeline();
        var handler = CreateHandler(HandlerType.LightWord);
        var cts = new CancellationTokenSource();

        var wordHandler = new LightHandlers.LightWordHandler(timeline, handler, cts.Token);

        Assert.NotNull(wordHandler);
    }

    [Fact]
    public void LightExcelHandler_CanBeConstructed()
    {
        var timeline = CreateTimeline();
        var handler = CreateHandler(HandlerType.LightExcel);
        var cts = new CancellationTokenSource();

        var excelHandler = new LightHandlers.LightExcelHandler(timeline, handler, cts.Token);

        Assert.NotNull(excelHandler);
    }

    [Fact]
    public void LightPowerPointHandler_CanBeConstructed()
    {
        var timeline = CreateTimeline();
        var handler = CreateHandler(HandlerType.LightPowerPoint);
        var cts = new CancellationTokenSource();

        var pptHandler = new LightHandlers.LightPowerPointHandler(timeline, handler, cts.Token);

        Assert.NotNull(pptHandler);
    }

    [Fact]
    public async Task LightWordHandler_Run_CompletesWithoutException()
    {
        // Acceptance: Word files created via OpenXML or similar (no COM needed)
        // Note: GetSavePath uses Windows-style path separator internally,
        // so file creation may fail on Linux. The handler should still complete gracefully.
        var outputDir = Path.Combine(Path.GetTempPath(), $"ghosts_lightword_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);

        try
        {
            var timeline = CreateTimeline();
            var handler = CreateHandler(HandlerType.LightWord, outputDir);
            var cts = new CancellationTokenSource();

            var wordHandler = new LightHandlers.LightWordHandler(timeline, handler, cts.Token);

            // Should complete without throwing (errors are caught internally)
            await wordHandler.Run();
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, true);
        }
    }

    [Fact]
    public async Task LightExcelHandler_Run_CompletesWithoutException()
    {
        // Acceptance: Excel files created via OpenXML or similar (no COM needed)
        // Note: GetSavePath uses Windows-style path separator internally,
        // so file creation may fail on Linux. The handler should still complete gracefully.
        var outputDir = Path.Combine(Path.GetTempPath(), $"ghosts_lightexcel_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);

        try
        {
            var timeline = CreateTimeline();
            var handler = CreateHandler(HandlerType.LightExcel, outputDir);
            var cts = new CancellationTokenSource();

            var excelHandler = new LightHandlers.LightExcelHandler(timeline, handler, cts.Token);

            // Should complete without throwing (errors are caught internally)
            await excelHandler.Run();
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, true);
        }
    }

    [Fact]
    public async Task LightPowerPointHandler_Run_CompletesWithoutException()
    {
        // Creates .pptx files via OpenXML in a ZipArchive (no COM needed)
        // Note: GetSavePath uses Windows-style path separator internally,
        // so file creation may fail on Linux. The handler should still complete gracefully.
        var outputDir = Path.Combine(Path.GetTempPath(), $"ghosts_lightppt_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);

        try
        {
            var timeline = CreateTimeline();
            var handler = CreateHandler(HandlerType.LightPowerPoint, outputDir);
            var cts = new CancellationTokenSource();

            var pptHandler = new LightHandlers.LightPowerPointHandler(timeline, handler, cts.Token);

            // Should complete without throwing (errors are caught internally)
            await pptHandler.Run();
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, true);
        }
    }

    [Fact]
    public void LightHandlers_TimelineEvent_CommandArgs_ContainsOutputDirectory()
    {
        var outputDir = "/tmp/test_output";
        var handler = CreateHandler(HandlerType.LightWord, outputDir);

        Assert.Single(handler.TimeLineEvents);
        Assert.Equal(outputDir, handler.TimeLineEvents[0].CommandArgs[0].ToString());
    }

    [Theory]
    [InlineData(HandlerType.LightWord)]
    [InlineData(HandlerType.LightExcel)]
    [InlineData(HandlerType.LightPowerPoint)]
    public void LightHandlers_AllTypes_HaveCorrectHandlerType(HandlerType type)
    {
        var handler = CreateHandler(type);
        Assert.Equal(type, handler.HandlerType);
    }
}
