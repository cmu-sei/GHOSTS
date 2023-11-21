using System;
using System.Threading;
using Ghosts.Domain;
using Ghosts.Domain.Code;

namespace Ghosts.Client.Handlers
{
    internal class AwsCli : BaseHandler
    {
        public string InstalledFolder;
        public bool IsInstalled;
        public bool IsAuthenticated;

        public AwsCli(TimelineHandler handler)
        {
            if (Program.Configuration.AwsCli == null ||
                string.IsNullOrEmpty(Program.Configuration.AwsCli.InstallFolder))
            {
                Log.Info($"AWS CLI configured, but proper configuration in ./config/application.json was not found. Exiting...");
                return;
            }

            InstalledFolder = Program.Configuration.AwsCli.InstallFolder;
            if (InstalledFolder.Contains("%"))
            {
                InstalledFolder = Environment.ExpandEnvironmentVariables(InstalledFolder);
            }

            // is cli installed?
            //
            // if not, install from the command line
            // msiexec.exe /i https://awscli.amazonaws.com/AWSCLIV2.msi /qn
            // CLI is typically installed in 
            // [ProgramFiles64Folder]\Amazon\AWSCLIV2
            //
            // -- this gets around requiring admin account
            // msiexec /a %USERPROFILE%\Downloads\AWSCLIV2.msi /qnb TARGETDIR=%USERPROFILE%
            // -- this _would_ require admin
            // setx PATH "%PATH%;%USERPROFILE%\Amazon\AWSCLIV2" /M
            // % USERPROFILE%\Amazon\AWSCLIV2\aws --version

            IsInstalled = System.IO.Directory.Exists(InstalledFolder);
            
            if(!IsInstalled)
            {
                Log.Info($"AWS CLI configured, but binaries could not be found at {InstalledFolder}. Exiting...");
                return;
            }

            // now check credentials
            // "~/.aws/credentials
            var credentialsFile = "%USERPROFILE%\\.aws\\credentials";
            if (credentialsFile.Contains("%"))
            {
                credentialsFile = Environment.ExpandEnvironmentVariables(credentialsFile);
            }

            IsAuthenticated = System.IO.File.Exists(credentialsFile);

            if (!IsAuthenticated)
            {
                Log.Info($"AWS CLI configured, but credentials could not be found at {credentialsFile}. Exiting...");
                return;
            }


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
            catch (ThreadAbortException e)
            {
                Log.Trace($"Cmd had a ThreadAbortException: {e}");
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        public void Ex(TimelineHandler handler)
        {
            // aws [options] <command> <subcommand> [parameters]
            
            // need to parse handler commands
            foreach (var timelineEvent in handler.TimeLineEvents)
            {
                var command = timelineEvent.Command;
                if (!command.ToLower().StartsWith("aws"))
                    command = $"aws {command}";
                command = $"cd {InstalledFolder} && {command}";
                var results = Cmd.Command(command);
                Report(new ReportItem { Handler = handler.HandlerType.ToString(), Command = command, Trackable = timelineEvent.TrackableId, Result = results });
            }
        }
    }
}