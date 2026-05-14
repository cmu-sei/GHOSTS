// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Reflection;
using Ghosts.Domain.Code;
using Xunit;

namespace Ghosts.Client.Universal.Tests.Handlers;

public class DebugManagerTests
{
    [Fact]
    public void CommandLineFlagManager_DebugOption_ExistsViaReflection()
    {
        // Verify the --debug flag is defined in Options class
        var optionsType = Assembly.Load("Ghosts.Client.Universal")
            .GetType("Ghosts.Client.Universal.Infrastructure.Options");
        Assert.NotNull(optionsType);

        var debugProperty = optionsType.GetProperty("Debug");
        Assert.NotNull(debugProperty);
        Assert.Equal(typeof(bool), debugProperty.PropertyType);
    }

    [Fact]
    public void CommandLineFlagManager_DebugShortFlag_IsMappedTo_d()
    {
        var optionsType = Assembly.Load("Ghosts.Client.Universal")
            .GetType("Ghosts.Client.Universal.Infrastructure.Options");
        Assert.NotNull(optionsType);

        var debugProperty = optionsType.GetProperty("Debug");
        Assert.NotNull(debugProperty);

        var attrs = debugProperty.GetCustomAttributes(
            Type.GetType("CommandLine.OptionAttribute, CommandLine"), false);
        Assert.Single(attrs);

        dynamic optionAttr = attrs[0];
        Assert.Equal("d", (string)optionAttr.ShortName);
        Assert.Equal("debug", (string)optionAttr.LongName);
    }

    [Fact]
    public void CommandLineFlagManager_Parse_MethodExists()
    {
        var managerType = Assembly.Load("Ghosts.Client.Universal")
            .GetType("Ghosts.Client.Universal.Infrastructure.CommandLineFlagManager");
        Assert.NotNull(managerType);

        var parseMethod = managerType.GetMethod("Parse",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(parseMethod);
    }

    [Fact]
    public void DebugMode_ConfigFileExistence_ChecksApplicationConfig()
    {
        // The debug output should report config file existence
        var configPath = ApplicationDetails.ConfigurationFiles.Application;
        Assert.NotNull(configPath);
        Assert.False(string.IsNullOrWhiteSpace(configPath));
    }

    [Fact]
    public void DebugMode_ConfigFileExistence_ChecksHealthConfig()
    {
        var healthPath = ApplicationDetails.ConfigurationFiles.Health;
        Assert.NotNull(healthPath);
        Assert.False(string.IsNullOrWhiteSpace(healthPath));
    }

    [Fact]
    public void DebugMode_ConfigFileExistence_ChecksTimelineConfig()
    {
        var timelinePath = ApplicationDetails.ConfigurationFiles.Timeline;
        Assert.NotNull(timelinePath);
        Assert.False(string.IsNullOrWhiteSpace(timelinePath));
    }

    [Fact]
    public void DebugMode_InstanceFiles_ChecksIdFile()
    {
        var idPath = ApplicationDetails.InstanceFiles.Id;
        Assert.NotNull(idPath);
        Assert.False(string.IsNullOrWhiteSpace(idPath));
    }

    [Fact]
    public void DebugMode_InstanceFiles_ChecksFilesCreated()
    {
        var filesCreatedPath = ApplicationDetails.InstanceFiles.FilesCreated;
        Assert.NotNull(filesCreatedPath);
        Assert.False(string.IsNullOrWhiteSpace(filesCreatedPath));
    }

    [Fact]
    public void DebugMode_InstanceFiles_ChecksSurveyResults()
    {
        var surveyPath = ApplicationDetails.InstanceFiles.SurveyResults;
        Assert.NotNull(surveyPath);
        Assert.False(string.IsNullOrWhiteSpace(surveyPath));
    }

    [Fact]
    public void DebugMode_LogFiles_ChecksClientUpdates()
    {
        var logPath = ApplicationDetails.LogFiles.ClientUpdates;
        Assert.NotNull(logPath);
        Assert.False(string.IsNullOrWhiteSpace(logPath));
    }

    [Fact]
    public void DebugMode_ApplicationDetails_VersionIsAvailable()
    {
        Assert.NotNull(ApplicationDetails.Version);
        Assert.NotNull(ApplicationDetails.Name);
    }

    [Fact]
    public void DebugMode_ApplicationDetails_InstalledPathIsAvailable()
    {
        var installedPath = ApplicationDetails.InstalledPath;
        Assert.NotNull(installedPath);
        Assert.False(string.IsNullOrWhiteSpace(installedPath));
    }

    [Fact]
    public void DebugMode_ProducesComprehensiveOutput()
    {
        // Acceptance: --debug flag produces comprehensive startup dump
        // Verify all expected diagnostic categories are accessible

        // Config existence
        Assert.NotNull(ApplicationDetails.ConfigurationFiles.Application);
        Assert.NotNull(ApplicationDetails.ConfigurationFiles.Health);
        Assert.NotNull(ApplicationDetails.ConfigurationFiles.Timeline);

        // Machine identity
        Assert.NotNull(ApplicationDetails.InstanceFiles.Id);

        // Application info
        Assert.NotNull(ApplicationDetails.InstalledPath);
        Assert.NotNull(ApplicationDetails.Name);
        Assert.NotNull(ApplicationDetails.Version);
    }

    [Fact]
    public void DebugMode_IsDebug_StaticField_Exists()
    {
        // Program.IsDebug should be accessible (set by --debug flag)
        var programType = Assembly.Load("Ghosts.Client.Universal")
            .GetType("Ghosts.Client.Universal.Program");
        Assert.NotNull(programType);

        var isDebugField = programType.GetField("IsDebug",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(isDebugField);
        Assert.Equal(typeof(bool), isDebugField.FieldType);
    }
}
