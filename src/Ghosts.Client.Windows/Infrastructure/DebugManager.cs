using System;
using System.IO;
using System.Security.Principal;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using NLog;

namespace Ghosts.Client.Infrastructure;

internal static class DebugManager
{
    public static readonly Logger Log = LogManager.GetCurrentClassLogger();

    public static void Evaluate()
    {
        var mode = "production";
        if (Program.IsDebug)
        {
            mode = "debug";
        }
        Write($"GHOSTS ({ApplicationDetails.Name}:{ApplicationDetails.Version} [{ApplicationDetails.VersionFile}]) running in {mode} mode");
        Write($"Installed path: {ApplicationDetails.InstalledPath}");
        Write($"Running as Username: {Environment.UserName} - WindowsIdentity: {WindowsIdentity.GetCurrent().Name}");
        Write($"{ApplicationDetails.ConfigurationFiles.Application} == {File.Exists(ApplicationDetails.ConfigurationFiles.Application)}");
        Write($"{ClientConfigurationResolver.Dictionary} == {File.Exists(ClientConfigurationResolver.Dictionary)}");
        Write($"{ClientConfigurationResolver.EmailContent} == {File.Exists(ClientConfigurationResolver.EmailContent)}");
        Write($"{ClientConfigurationResolver.EmailReply} == {File.Exists(ClientConfigurationResolver.EmailReply)}");
        Write($"{ClientConfigurationResolver.EmailDomain} == {File.Exists(ClientConfigurationResolver.EmailDomain)}");
        Write($"{ClientConfigurationResolver.EmailOutside} == {File.Exists(ClientConfigurationResolver.EmailOutside)}");
        Write($"{ApplicationDetails.ConfigurationFiles.Health} == {File.Exists(ApplicationDetails.ConfigurationFiles.Health)}");
        Write($"{ApplicationDetails.ConfigurationFiles.Timeline} == {File.Exists(ApplicationDetails.ConfigurationFiles.Timeline)}");
        Write($"{ApplicationDetails.InstanceFiles.Id} == {File.Exists(ApplicationDetails.InstanceFiles.Id)}");
        Write($"{ApplicationDetails.InstanceFiles.FilesCreated} == {File.Exists(ApplicationDetails.InstanceFiles.FilesCreated)}");
        Write($"{ApplicationDetails.InstanceFiles.Trackables} == {File.Exists(ApplicationDetails.InstanceFiles.Trackables)}");
        Write($"{ApplicationDetails.InstanceFiles.SurveyResults} == {File.Exists(ApplicationDetails.InstanceFiles.SurveyResults)}");
        Write($"{ApplicationDetails.LogFiles.ClientUpdates} == {File.Exists(ApplicationDetails.LogFiles.ClientUpdates)}");

        var machine = new ResultMachine();
        GuestInfoVars.Load(machine);
        Write("------------------");
        Write("Client Check-in Values:");
        Write($"Machine.Id = {machine.Id}");
        Write($"Machine.Name = {machine.Name}");
        Write($"Machine.CurrentUsername = {machine.CurrentUsername}");
        Write($"Machine.Domain = {machine.Domain}");
        Write($"Machine.ClientIp = {machine.ClientIp}");
        Write($"Machine.FQDN = {machine.FQDN}");
        Write($"Machine.Host = {machine.Host}");
        Write($"Machine.IpAddress = {machine.IpAddress}");
        Write($"Machine.ResolvedHost = {machine.ResolvedHost}");
        Write("------------------");
        //if (Program.IsDebug)
        //{
            Write($"Configured API Base = {Program.Configuration.ApiRootUrl}");
            Write($"Configured API Id = {Program.ConfigurationUrls.Id}");
            Write($"Configured API Survey = {Program.ConfigurationUrls.Survey}");
            Write($"Configured API Socket = {Program.ConfigurationUrls.Socket}");
            Write($"Configured API Timeline = {Program.ConfigurationUrls.Timeline}");
            Write($"Configured API Results = {Program.ConfigurationUrls.Results}");
            Write($"Configured API Updates = {Program.ConfigurationUrls.Updates}");
            Write("------------------");
        //}
    }

    private static void Write(string line)
    {
        Log.Info(line);
        Console.WriteLine(line);
    }
}