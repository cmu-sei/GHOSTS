// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Ghosts.Client.Universal.Infrastructure;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Newtonsoft.Json;

using System.Text;

#if WINDOWS_BUILD
using AutoItX3Lib;
#endif

namespace Ghosts.Client.Universal.Handlers;

/// <summary>
/// Remote Desktop Protocol Handler.
/// Opens an RDP session to a randomly chosen target, maintains the connection for a configured
/// execution time, then closes the session and repeats for each timeline event.
/// On Windows uses mstsc, on Linux uses xfreerdp or rdesktop.
/// 
/// For Windows, The AutoIt library is used to automate actions from Ghosts Client, such as dealing with popup windows
/// during connection and moving the mouse once the desktop connection is established.
/// 
/// For Linux, no desktop automation is supported.
/// 
/// The credential file is the same format as WMI, SFTP, SSH, etc.
/// If only the password is specified, then the login user is assumed to be the current user.
/// If username is specified (and optionally domain), and the current user is not the logged in user,
/// then a login using the specified domain\username is used.
/// 
/// Windows only notes:
/// 
/// Remote Desktop Protocol Handler
/// Action:  The logged-in user (i.e. ghosts user) opens a remote desktop to randomly chosen target, and does random
/// mouse movements for the specified ExecutionTime. When this time has elapsed, the connection is closed,
/// another target is chosen, and the cycle repeats.
/// 
/// See the sample Rdp timeline for all options.
/// 
/// This handler will supply a password (and also usename if supplied) if prompted, and dismiss a untrused certificate check prompt if one
/// appears.
/// 
/// It is assumed that the logged-in user (or the specified user) has the right to log into the target system.
/// Caveat: This has only be tested in a Windows domain system where the domain policy was altered to allow all users
/// remote desktop priviledges, and only tested using RDP to a domain controller server.
/// It has also been tested using the Domain Admin user (not the current logged in user), in which the handler
/// supplied both username and password to the crdential prompts. Using the Domain Admin user does not require
/// modification of the domain policy to enable RDP.
/// 
/// </summary>
public class Rdp(Timeline entireTimeline, TimelineHandler timelineHandler, CancellationToken cancellationToken)
    : BaseHandler(entireTimeline, timelineHandler, cancellationToken)
{
    private Credentials _currentCreds;
    private string _currentTarget;
    private int _jitterFactor;
    private int _executionTime = 20000;
    private int _mouseSleepTime = 10000;
    private int _executionProbability = 100;

    private string _customLogin = null;  //only used for Windows

    protected override Task RunOnce()
    {
        try
        {
            if (Handler.HandlerArgs != null)
            {
                if (Handler.HandlerArgs.TryGetValue("CredentialsFile", out var credFileArg))
                {
                    try
                    {
                        _currentCreds = JsonConvert.DeserializeObject<Credentials>(File.ReadAllText(credFileArg.ToString()));
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                    }
                }

                if (Handler.HandlerArgs.TryGetValue("Credentials", out var credentialsArg))
                {
                    try
                    {
                        _currentCreds = JsonConvert.DeserializeObject<Credentials>(credentialsArg.ToString());
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                    }
                }

                if (Handler.HandlerArgs.TryGetValue("execution-time", out var execTimeArg))
                {
                    if (int.TryParse(execTimeArg.ToString(), out var et))
                    {
                        _executionTime = et < 0 ? 20000 : et;
                    }
                }

                if (Handler.HandlerArgs.TryGetValue("mouse-sleep-time", out var mouseSleepArg))
                {
                    if (int.TryParse(mouseSleepArg.ToString(), out var ms))
                    {
                        _mouseSleepTime = ms < 0 ? 10000 : ms;
                    }
                }

                if (Handler.HandlerArgs.TryGetValue("execution-probability", out var execProbArg))
                {
                    if (int.TryParse(execProbArg.ToString(), out var ep))
                    {
                        _executionProbability = (ep < 0 || ep > 100) ? 100 : ep;
                    }
                }

                if (Handler.HandlerArgs.TryGetValue("delay-jitter", out var jitterArg))
                {
                    _jitterFactor = Jitter.JitterFactorParse(jitterArg.ToString());
                }

                if (Handler.HandlerArgs.ContainsKey("custom-login") &&
                    !string.IsNullOrEmpty(Handler.HandlerArgs["custom-login"].ToString()))
                {
                    _customLogin = Handler.HandlerArgs["custom-login"].ToString();
                }
            }

            if (_currentCreds == null)
            {
                _log.Error("RDP:: No credentials supplied, either CredentialsFile or Credentials must be supplied in handler args, exiting.");
                return Task.CompletedTask;
            }

            Ex();
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

    private void Ex()
    {
        foreach (var timelineEvent in Handler.TimeLineEvents)
        {
            Token.ThrowIfCancellationRequested();
            WorkingHours.Is(Handler);

            if (timelineEvent.DelayBeforeActual > 0){
                if (Token.WaitHandle.WaitOne(timelineEvent.DelayBeforeActual)) Token.ThrowIfCancellationRequested();
            }

            _log.Trace($"RDP Command: {timelineEvent.Command} with delay after of {timelineEvent.DelayAfterActual}");

            switch (timelineEvent.Command)
            {
                case "random":
                    if (_executionProbability < _random.Next(0, 100))
                    {
                        _log.Trace("RDP:: Choice skipped due to execution probability");
                        if (Token.WaitHandle.WaitOne(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, _jitterFactor))) Token.ThrowIfCancellationRequested();
                        continue;
                    }

                    var cmd = timelineEvent.CommandArgs[_random.Next(0, timelineEvent.CommandArgs.Count)];
                    if (!string.IsNullOrEmpty(cmd.ToString()))
                    {
#if WINDOWS_BUILD
                            WinExecuteRdp(Handler, timelineEvent, cmd.ToString());
#else
                 
                            ExecuteRdp(timelineEvent, cmd.ToString());   
#endif
                    }
                    if (Token.WaitHandle.WaitOne(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, _jitterFactor))) Token.ThrowIfCancellationRequested();
                    break;
            }

            if (timelineEvent.DelayAfterActual > 0)
                if (Token.WaitHandle.WaitOne(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, _jitterFactor))) Token.ThrowIfCancellationRequested();
        }
    }

    private void ExecuteRdp(TimelineEvent timelineEvent, string command)
    {
        var charSeparators = new char[] { '|' };
        var cmdArgs = command.Split(charSeparators, 2, StringSplitOptions.None);
        var target = cmdArgs[0];
        var credKey = cmdArgs[1];
        var domain = _currentCreds.GetDomain(credKey);
        var username = _currentCreds.GetUsername(credKey);
        var password = _currentCreds.GetPassword(credKey);

        _log.Trace($"RDP:: Spawning RDP connection for target {target}");

        if (username == null || password == null)
        {
            _log.Error($"RDP:: Missing username or password for credential key '{credKey}', skipping.");
            return;
        }

        Process process = null;
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                process = StartWindowsRdp(target);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                process = StartLinuxRdp(target, username, password, domain);
            }
            else
            {
                _log.Trace("RDP:: Unsupported platform, skipping.");
                return;
            }

            if (process == null)
            {
                _log.Error($"RDP:: Failed to start RDP process for target {target}");
                return;
            }

            _log.Trace($"RDP:: Connected to {target}, maintaining session for {_executionTime}ms");

            // Maintain the session for the configured execution time, sleeping in intervals
            var totalTime = 0;
            while (totalTime < _executionTime)
            {
                Token.ThrowIfCancellationRequested();
                var sleepTime = Jitter.JitterFactorDelay(_mouseSleepTime, _jitterFactor);
                if (sleepTime <= 0) sleepTime = 1000;
                if (Token.WaitHandle.WaitOne(sleepTime)) Token.ThrowIfCancellationRequested();
                totalTime += sleepTime;
            }

            Report(new ReportItem { Handler = Handler.HandlerType.ToString(), Command = target, Arg = credKey, Trackable = timelineEvent.TrackableId });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception e)
        {
            _log.Error(e);
        }
        finally
        {
            if (process != null && !process.HasExited)
            {
                _log.Trace($"RDP:: Closing RDP process for target {target}");
                try
                {
                    process.Kill();
                }
                catch (Exception e)
                {
                    _log.Trace($"RDP:: Error killing process: {e.Message}");
                }
            }
            process?.Dispose();
        }
    }

    private static Process StartWindowsRdp(string target)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "mstsc",
            Arguments = $"/v:{target}",
            UseShellExecute = false,
            CreateNoWindow = true
        };
        return Process.Start(startInfo);
    }

    private static Process StartLinuxRdp(string target, string username, string password, string domain)
    {
        // Try xfreerdp first, fall back to rdesktop
        var userArg = domain != null ? $"{domain}\\{username}" : username;

        var xfreerdpPath = FindExecutable("xfreerdp");
        if (xfreerdpPath != null)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = xfreerdpPath,
                Arguments = $"/v:{target} /u:{userArg} /p:{password} /cert:ignore",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            return Process.Start(startInfo);
        }

        var rdesktopPath = FindExecutable("rdesktop");
        if (rdesktopPath != null)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = rdesktopPath,
                Arguments = $"-u {userArg} -p {password} {target}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            return Process.Start(startInfo);
        }

        _log.Error("RDP:: Neither xfreerdp nor rdesktop found on this system.");
        return null;
    }

    private static string FindExecutable(string name)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "which",
                Arguments = name,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using var proc = Process.Start(startInfo);
            if (proc == null) return null;
            var output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit();
            return proc.ExitCode == 0 && !string.IsNullOrEmpty(output) ? output : null;
        }
        catch
        {
            return null;
        }
    }

#if WINDOWS_BUILD

    public void CheckGenericPrompt(AutoItX3 au, string closeString)
    {
        var caption = "Remote Desktop Connection";
        var winHandle = FindDialogWindow(caption);
        if (winHandle != IntPtr.Zero)
        {
            //this is a standard dialog class. By default, chooses 'No'. Select yes prompt
            Winuser.SetForegroundWindow(winHandle);
            au.Send(closeString); //send the close string
            if (Token.WaitHandle.WaitOne(1000)) Token.ThrowIfCancellationRequested();
            _log.Trace($"RDP:: Found window prompt, caption: {caption} for {_currentTarget}.");
        }
        return;
    }

    public void DoMouseLoop(string caption, string target, AutoItX3 au, TimelineEvent timelineEvent)
    {
        var winHandle = Winuser.FindWindow("TscShellContainerClass", caption);
        if (winHandle != IntPtr.Zero)
        {
            _log.Trace($"RDP:: Connected to {target}, beginning activity loop");
            // found the remote desktop window
            int totalTime = 0;
            while (totalTime <  _executionTime)
            {
                var sleepTime = Jitter.JitterFactorDelay(_mouseSleepTime, JitterFactor);
                if (Token.WaitHandle.WaitOne(sleepTime)) Token.ThrowIfCancellationRequested();  
                totalTime += sleepTime;
                //move the mouse
                Winuser.SetForegroundWindow(winHandle);
                Winuser.RECT rc;
                Winuser.GetWindowRect(winHandle, out rc);
                au.MouseMove(rc.Left, rc.Top, 30);
                au.MouseMove(_random.Next(rc.Left, rc.Right), _random.Next(rc.Top, rc.Bottom), 30);
                Winuser.SetForegroundWindow(winHandle);
                Report(new ReportItem { Handler = "RDP", Command = _currentTarget, Arg = "mouseloop", Trackable = timelineEvent.TrackableId });
            }
            //close the window
            _log.Trace($"RDP:: Finished activity loop for {target}.");
            au.WinClose(caption);
            if (Token.WaitHandle.WaitOne(1000)) Token.ThrowIfCancellationRequested();  
            // handle the close dialog
            CheckGenericPrompt(au, "{ENTER}");
            if (Token.WaitHandle.WaitOne(1000)) Token.ThrowIfCancellationRequested();  
        }
        else
        {
            _log.Trace($"RDP:: Unable to find remote desktop window for {target}.");
        }
    }


    public static string EscapePassword(string password)
        {
            char[] special = { '+', '^', '%', '(', ')', '~', '{', '}', '[', ']' };
            var s = password;
            foreach (var c in special)
            {

                var cstr = c.ToString();
                if (s.Contains(cstr))
                {
                    var rep = "{" + cstr + "}";
                    s = s.Replace(cstr, rep);
                }

            }
            return s;
        }
        public void checkPasswordPrompt(AutoItX3 au, string password, string username, bool usePasswordOnly)
        {
            _log.Trace($"RDP:: Checking for password prompt, usePasswordOnly is  {usePasswordOnly}.");
            bool foundWinSecurity = false; 
            var winHandle = Winuser.FindWindow("Credential Dialog Xaml Host", "Windows Security");
            if (winHandle == IntPtr.Zero)
            {
                //try harder
                foundWinSecurity = true;
                winHandle = FindDialogWindow("Windows Security");
                if (winHandle != IntPtr.Zero)
                {
                   _log.Trace($"RDP:: Found window prompt, caption: 'Windows Security' for {_currentTarget}.");
                }
            } else
            {
                _log.Trace($"RDP:: Found window prompt, caption: 'Credential Dialog Xaml Host' for {_currentTarget}.");
            }
            if (winHandle != IntPtr.Zero)
            {
                if (_customLogin != null)
                {
                    _log.Trace($"RDP:: Executing custom login to {_currentTarget}.");
                    string[] cmds = _customLogin.Split('\n');
                    foreach ( string cmd in cmds )
                    {
                        if (cmd.Contains("#USERNAME"))
                        {
                            System.Windows.Forms.SendKeys.SendWait(username);
                        }
                        else if (cmd.Contains("#PASSWORD"))
                        {
                           
                            var s = EscapePassword(password);
                            System.Windows.Forms.SendKeys.SendWait(s);  //fill in password field
                        }
                        else if (cmd.Contains("#DELAY"))
                        {
                            var s = cmd.Replace("#DELAY", "");
                            int delay;
                            if (int.TryParse(s, out delay))
                            {
                               Thread.Sleep(delay);
                            }
                            
                        }
                        else
                        {
                            au.Send(cmd);
                        }
                        
                    }
                    _log.Trace($"RDP:: Finished custom login to {_currentTarget}.");

                }
                else if (usePasswordOnly)
                {
                    string responseString;
                    if (foundWinSecurity)
                    {
                        responseString = "{TAB}{TAB}{ENTER}";  //this form has a different response string
                    } else
                    {
                        responseString = "{TAB}{TAB}{TAB}{ENTER}";
                    }
                    //password prompt is up.  handle it.
                    Winuser.SetForegroundWindow(winHandle);
                    var s = EscapePassword(password);
                    System.Windows.Forms.SendKeys.SendWait(s);  //fill in password field
                    au.Send(responseString);
                    Thread.Sleep(1000);
                    _log.Trace($"RDP:: Found password prompt for {_currentTarget}.");
                }  else
                {
                    //try entering full username
                    Winuser.SetForegroundWindow(winHandle);
                    if (foundWinSecurity)
                    {
                        _log.Trace($"RDP:: Attempting user/password fill on 'Windows Security' window for {_currentTarget}.");
                        au.Send("{DOWN}");  //moves down to next user
                        Thread.Sleep(1000);
                        Winuser.SetForegroundWindow(winHandle);
                        System.Windows.Forms.SendKeys.SendWait(username);  //fill in user field, tab to password field
                        au.Send("{TAB}");
                        var s = EscapePassword(password);
                        System.Windows.Forms.SendKeys.SendWait(s);  //fill in password field
                        au.Send("{ENTER}");
                        Thread.Sleep(1000);
                    }
                    else
                    {
                        _log.Trace($"RDP:: Attempting More Choices option for user/password prompt for {_currentTarget}.");
                        au.Send("{TAB}{TAB}{ENTER}");  //this clicks on 'More Choices'
                        Thread.Sleep(1000);
                        Winuser.SetForegroundWindow(winHandle);
                        au.Send("{TAB}{TAB}{ENTER}");  //this clicks on 'Use diff user'
                        Thread.Sleep(1000);
                        Winuser.SetForegroundWindow(winHandle);
                        System.Windows.Forms.SendKeys.SendWait(username);  //fill in user field, tab to password field
                        au.Send("{TAB}");
                        var s = EscapePassword(password);
                        System.Windows.Forms.SendKeys.SendWait(s);  //fill in password field
                        au.Send("{ENTER}");
                        Thread.Sleep(1000);
                    }
                    _log.Trace($"RDP:: Found user/password prompt for {_currentTarget}.");

                }


            }
            return;
        }

 
    public void Cleanup(AutoItX3 au, string caption,Process process)
    {
        //close window if still open
        var winHandle = Winuser.FindWindow("TscShellContainerClass", caption);
        if (winHandle != IntPtr.Zero)
        {
            _log.Trace($"RDP:: Closing Remote Desktop window.");
            au.WinClose(caption);
            if (Token.WaitHandle.WaitOne(1000)) Token.ThrowIfCancellationRequested();
            // handle the close dialog
            CheckGenericPrompt(au, "{ENTER}");
            if (Token.WaitHandle.WaitOne(1000)) Token.ThrowIfCancellationRequested();
        }
        if (process != null)
        {
            if (!process.HasExited)
            {
                _log.Trace($"RDP:: Closing Remote Desktop process window.");
                process.CloseMainWindow();
                if (Token.WaitHandle.WaitOne(10000)) Token.ThrowIfCancellationRequested();
                if (!process.HasExited)
                {
                    _log.Trace($"RDP:: Killing Remote Desktop process.");
                    process.Kill();
                    if (Token.WaitHandle.WaitOne(1000)) Token.ThrowIfCancellationRequested();
                }
            } else
            {
                _log.Trace($"RDP:: Remote Desktop process has exited.");
            }
        }
        else
        {
            _log.Trace($"RDP:: No Remote Desktop process to cleanup.");
        }
    }


    public void WinExecuteRdp(TimelineHandler handler, TimelineEvent timelineEvent, string choice)
    {
        
        AutoItX3 au = new AutoItX3();
        char[] charSeparators = new char[] { '|' };
        var cmdArgs = choice.Split(charSeparators, 2, StringSplitOptions.None);
        var target = cmdArgs[0];
        _currentTarget = target;
        string command = $"mstsc /v:{target}";
        var credKey = cmdArgs[1];
        var domain = _currentCreds.GetDomain(credKey);
        var username = _currentCreds.GetUsername(credKey);
        var password = _currentCreds.GetPassword(credKey);
        _log.Trace($"RDP:: Spawning RDP connection for target {target}");
        var localUser = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
        bool usePasswordOnly = true;
        if (username != null)
        {
            if (domain != null)
            {
                username = $"{domain}\\{username}";
            }
            if (!localUser.ToLower().Contains(username.ToLower())) {
                usePasswordOnly = false;
            }
        }

        var processStartInfo = new ProcessStartInfo("cmd.exe")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        var caption = $"{target} - Remote Desktop Connection";

        using (var process = Process.Start(processStartInfo))
        {
            try
            {
                if (Token.WaitHandle.WaitOne(1000)) Token.ThrowIfCancellationRequested();
                if (process != null)
                {
                    process.StandardInput.WriteLine(command);
                    process.StandardInput.Close(); // line added to stop process from hanging on ReadToEnd()
                    if (Token.WaitHandle.WaitOne(2000)) Token.ThrowIfCancellationRequested();  //give time for response
                    checkPasswordPrompt(au, password, username, usePasswordOnly);
                    //wait 15 seconds for connection window
                    if (!FindRdpWindow(target, 15))
                    {
                        //may have a certificate problem
                        _log.Trace("RDP:: Unable to find a RDP window, attempting to accept an untrusted certificate");
                        CheckGenericPrompt(au, "{TAB}{TAB}{TAB}{ENTER}");  //this is for a certificate prompt
                    }
                    //try again for another 4.25 minutes to find
                    if (FindRdpWindow(target, 255))
                    {
                        DoMouseLoop(caption, target, au, timelineEvent);
                    }
                    else
                    {
                        _log.Trace($"RDP:: Unable to connect to remote desktop for {target}");
                    }
                    if (Token.WaitHandle.WaitOne(2000)) Token.ThrowIfCancellationRequested();  
                    Cleanup(au, caption,process);
                }
                else
                {
                    _log.Trace($"RDP:: Failed to execute the remote desktop connection program for {target}");
                }
            }
            catch (OperationCanceledException)
            {
                Cleanup(au, caption, process);
                throw;  //pass up
            }
            catch (Exception e)
            {
                Cleanup(au, caption, process);
                _log.Error(e);
            }
        }
    }

    public bool FindRdpWindow(string target,int timeout)
    {
        var caption = $"{target} - Remote Desktop Connection";
        var winHandle = Winuser.FindWindow("TscShellContainerClass", caption);
        int wait = 1; //time to wait for this loop (exponential back off: 2^n starting with n=0)
        //wait for window to appear 
        while (winHandle == IntPtr.Zero)
        {
            _log.Trace($"RDP:: Unable to find desktop window for {target}, sleeping {wait} seconds");
            if (Token.WaitHandle.WaitOne(wait * 1000)) Token.ThrowIfCancellationRequested();
            if (2 * wait - 1 >= timeout) break; // sum(2^n) from n=0 -> n=x is 2 * 2^x - 1
            wait = 2 * wait; //double the time to wait next loop (2^n)
            winHandle = Winuser.FindWindow("TscShellContainerClass", caption);
        }
        return winHandle != IntPtr.Zero;
    }
    public IntPtr FindDialogWindow(string caption)
    {
        var winHandle = Winuser.FindWindow(null, caption);
        if (winHandle != IntPtr.Zero)
        {
            //this finds any window with this caption
            //determine if class name is for standard dialogue
            int bufSize = 256;
            StringBuilder buffer = new StringBuilder(bufSize);
            Winuser.GetClassName(winHandle, buffer, bufSize);
            var s = buffer.ToString();
            if (s.Contains("32770")) return winHandle; //success
        }
        return IntPtr.Zero;
    }
       
#endif

}
