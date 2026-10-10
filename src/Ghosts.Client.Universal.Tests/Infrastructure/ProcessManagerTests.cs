// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Ghosts.Client.Universal.Infrastructure;
using Ghosts.Domain;
using Xunit;

namespace Ghosts.Client.Universal.Tests.Infrastructure;

public class ProcessManagerTests
{
    [Fact]
    public void GetThisProcessPid_ReturnsCurrentProcessId()
    {
        var expected = Process.GetCurrentProcess().Id;
        var actual = ProcessManager.GetThisProcessPid();
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GetPids_ForCurrentProcess_ReturnsAtLeastOne()
    {
        var processName = Process.GetCurrentProcess().ProcessName;
        var pids = ProcessManager.GetPids(processName).ToList();
        Assert.NotEmpty(pids);
        Assert.Contains(Process.GetCurrentProcess().Id, pids);
    }

    [Fact]
    public void GetPids_ForNonexistentProcess_ReturnsEmpty()
    {
        var pids = ProcessManager.GetPids("nonexistent_process_xyz_12345").ToList();
        Assert.Empty(pids);
    }

    [Fact]
    public void KillProcessAndChildrenByName_ForNonexistentProcess_DoesNotThrow()
    {
        var ex = Record.Exception(() => ProcessManager.KillProcessAndChildrenByName("nonexistent_process_xyz_12345"));
        Assert.Null(ex);
    }

    [Fact]
    public void KillProcessAndChildrenByPid_ForInvalidPid_DoesNotThrow()
    {
        var ex = Record.Exception(() => ProcessManager.KillProcessAndChildrenByPid(99999999));
        Assert.Null(ex);
    }

    [Fact]
    public void KillProcessAndChildrenByPid_ForZero_DoesNothing()
    {
        var ex = Record.Exception(() => ProcessManager.KillProcessAndChildrenByPid(0));
        Assert.Null(ex);
    }

    [Fact]
    public void KillProcessAndChildrenByHandler_ForUnmappedType_DoesNotThrow()
    {
        var handler = new TimelineHandler { HandlerType = HandlerType.NpcSystem };
        var ex = Record.Exception(() => ProcessManager.KillProcessAndChildrenByHandler(handler));
        Assert.Null(ex);
    }

    [Theory]
    [InlineData(HandlerType.BrowserChrome, "chrome", "chromedriver")]
    [InlineData(HandlerType.BrowserEdge, "msedge", "msedgedriver")]
    [InlineData(HandlerType.BrowserFirefox, "firefox", "geckodriver")]
    public void GetProcessNames_ForBrowser_ReturnsBrowserAndDriver(HandlerType handlerType, string browser, string driver)
    {
        Assert.Equal(new[] { browser, driver }, ProcessManager.GetProcessNames(handlerType));
    }

    [Theory]
    [InlineData(HandlerType.NpcSystem)]
    [InlineData(HandlerType.Outlook)]
    [InlineData(HandlerType.Outlookv2)]
    public void GetProcessNames_ForHandlerWithoutItsOwnProcess_ReturnsEmpty(HandlerType handlerType)
    {
        Assert.Empty(ProcessManager.GetProcessNames(handlerType));
    }

    [Fact]
    public void ProcessNames_Chrome_ReturnsExpectedValue()
    {
        Assert.Equal("chrome", ProcessManager.ProcessNames.Chrome);
    }

    [Fact]
    public void ProcessNames_ChromeDriver_ReturnsExpectedValue()
    {
        Assert.Equal("chromedriver", ProcessManager.ProcessNames.ChromeDriver);
    }

    [Fact]
    public void ProcessNames_Firefox_ReturnsExpectedValue()
    {
        Assert.Equal("firefox", ProcessManager.ProcessNames.Firefox);
    }

    [Fact]
    public void ProcessNames_Command_IsCmd()
    {
        Assert.Equal("cmd", ProcessManager.ProcessNames.Command);
    }

    [Fact]
    public void ProcessNames_Curl_ReturnsExpectedValue()
    {
        Assert.Equal("curl", ProcessManager.ProcessNames.Curl);
    }

    [Fact]
    public void GetDotnetHostedPids_FindsInstancesButNotHandleRuns()
    {
        if (!OperatingSystem.IsLinux()) return; //fakes dotnet hosts with a copy of bash

        var dir = Directory.CreateTempSubdirectory().FullName;
        var fakeDotnet = Path.Combine(dir, "dotnet");
        File.Copy("/bin/bash", fakeDotnet);
        var assembly = $"Ghosts.Test{Guid.NewGuid():N}";

        Process Start(params string[] args)
        {
            var psi = new ProcessStartInfo(fakeDotnet);
            foreach (var arg in new[] { "-c", "sleep 60; true" }.Concat(args)) psi.ArgumentList.Add(arg);
            return Process.Start(psi);
        }

        var instance = Start($"{assembly}.dll");
        var handleRun = Start($"{assembly}.dll", "--handle");
        var other = Start("Other.dll");
        try
        {
            Thread.Sleep(500);
            Assert.Equal(new[] { instance.Id }, ProcessManager.GetDotnetHostedPids(assembly));
        }
        finally
        {
            foreach (var p in new[] { instance, handleRun, other }) p.Kill(true);
            Directory.Delete(dir, true);
        }
    }
}
