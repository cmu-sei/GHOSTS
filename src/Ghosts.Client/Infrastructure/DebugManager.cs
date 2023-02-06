using System;
using System.IO;
using System.Security.Principal;
using Ghosts.Domain.Code;

namespace Ghosts.Client.Infrastructure;

internal static class DebugManager
{
    public static void Evaluate()
    {
        var mode = "production";
        if (Program.IsDebug)
        {
            mode = "debug";
        }
        Console.WriteLine($"GHOSTS ({ApplicationDetails.Name}:{ApplicationDetails.Version} [{ApplicationDetails.VersionFile}]) running in {mode} mode");
        Console.WriteLine($"Installed path: {ApplicationDetails.InstalledPath}");
        Console.WriteLine($"Running as Username: {Environment.UserName} - WindowsIdentity: {WindowsIdentity.GetCurrent().Name}");
        Console.WriteLine($"{ApplicationDetails.ConfigurationFiles.Application} == {File.Exists(ApplicationDetails.ConfigurationFiles.Application)}");
        Console.WriteLine($"{ClientConfigurationResolver.Dictionary} == {File.Exists(ClientConfigurationResolver.Dictionary)}");
        Console.WriteLine($"{ClientConfigurationResolver.EmailContent} == {File.Exists(ClientConfigurationResolver.EmailContent)}");
        Console.WriteLine($"{ClientConfigurationResolver.EmailReply} == {File.Exists(ClientConfigurationResolver.EmailReply)}");
        Console.WriteLine($"{ClientConfigurationResolver.EmailDomain} == {File.Exists(ClientConfigurationResolver.EmailDomain)}");
        Console.WriteLine($"{ClientConfigurationResolver.EmailOutside} == {File.Exists(ClientConfigurationResolver.EmailOutside)}");
        Console.WriteLine($"{ApplicationDetails.ConfigurationFiles.Health} == {File.Exists(ApplicationDetails.ConfigurationFiles.Health)}");
        Console.WriteLine($"{ApplicationDetails.ConfigurationFiles.Timeline} == {File.Exists(ApplicationDetails.ConfigurationFiles.Timeline)}");
        Console.WriteLine($"{ApplicationDetails.InstanceFiles.Id} == {File.Exists(ApplicationDetails.InstanceFiles.Id)}");
        Console.WriteLine($"{ApplicationDetails.InstanceFiles.FilesCreated} == {File.Exists(ApplicationDetails.InstanceFiles.FilesCreated)}");
        Console.WriteLine($"{ApplicationDetails.InstanceFiles.Trackables} == {File.Exists(ApplicationDetails.InstanceFiles.Trackables)}");
        Console.WriteLine($"{ApplicationDetails.InstanceFiles.SurveyResults} == {File.Exists(ApplicationDetails.InstanceFiles.SurveyResults)}");
        Console.WriteLine($"{ApplicationDetails.LogFiles.ClientUpdates} == {File.Exists(ApplicationDetails.LogFiles.ClientUpdates)}");
    }
}