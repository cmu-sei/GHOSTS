// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Ghosts.Domain.Code;

namespace Ghosts.Client.Universal.Infrastructure;

internal static class DebugManager
{
    internal static void Run()
    {
        // Header
        Console.WriteLine("=== GHOSTS Debug Diagnostics ===");
        Console.WriteLine();

        // Application info
        PrintSection("Application");
        PrintItem("Name", ApplicationDetails.Name);
        PrintItem("Version", ApplicationDetails.Version);
        PrintItem("File Version", ApplicationDetails.VersionFile);
        PrintItem("Installed Path", ApplicationDetails.InstalledPath);

        // Platform info
        PrintSection("Platform");
        PrintItem("OS", RuntimeInformation.OSDescription);
        PrintItem("Architecture", RuntimeInformation.OSArchitecture.ToString());
        PrintItem("Process Arch", RuntimeInformation.ProcessArchitecture.ToString());
        PrintItem(".NET Runtime", RuntimeInformation.FrameworkDescription);
        PrintItem("Target Framework", Assembly.GetEntryAssembly()?.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName ?? "unknown");

        // Config files
        PrintSection("Configuration Files");
        PrintFileStatus("Application Config", ApplicationDetails.ConfigurationFiles.Application);
        PrintFileStatus("Health Config", ApplicationDetails.ConfigurationFiles.Health);
        PrintFileStatus("Timeline Config", ApplicationDetails.ConfigurationFiles.Timeline);

        // Instance files (machine identity)
        PrintSection("Machine Identity");
        PrintFileStatus("Client ID", ApplicationDetails.InstanceFiles.Id);
        if (File.Exists(ApplicationDetails.InstanceFiles.Id))
        {
            try
            {
                var id = File.ReadAllText(ApplicationDetails.InstanceFiles.Id).Trim();
                PrintItem("  ID Value", id);
            }
            catch { }
        }
        PrintFileStatus("Files Created Log", ApplicationDetails.InstanceFiles.FilesCreated);
        PrintFileStatus("Survey Results", ApplicationDetails.InstanceFiles.SurveyResults);

        // Log files
        PrintSection("Log Files");
        PrintFileStatus("Client Updates", ApplicationDetails.LogFiles.ClientUpdates);

        // API URLs - try to load config
        PrintSection("API Configuration");
        try
        {
            var config = ClientConfigurationLoader.Config;
            if (config != null)
            {
                PrintItem("API Root URL", config.ApiRootUrl ?? "(not set)");
                PrintItem("Sockets Enabled", config.Sockets?.IsEnabled.ToString() ?? "false");
                PrintItem("Survey Enabled", config.Survey?.IsEnabled.ToString() ?? "false");
                PrintItem("Health Enabled", config.HealthIsEnabled.ToString());
                PrintItem("Handlers Enabled", config.HandlersIsEnabled.ToString());
            }
            else
            {
                PrintItem("Status", "Configuration not loaded");
            }
        }
        catch (Exception e)
        {
            PrintItem("Status", $"Failed to load: {e.Message}");
        }

        Console.WriteLine();
        Console.WriteLine("=== End Debug Diagnostics ===");
        Console.WriteLine();
    }

    private static void PrintSection(string name)
    {
        Console.WriteLine();
        Console.WriteLine($"--- {name} ---");
    }

    private static void PrintItem(string label, string value)
    {
        Console.WriteLine($"  {label}: {value}");
    }

    private static void PrintFileStatus(string label, string path)
    {
        var exists = File.Exists(path);
        Console.WriteLine($"  {label}: {path} [{(exists ? "EXISTS" : "MISSING")}]");
    }
}
