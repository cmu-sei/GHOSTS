// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Diagnostics;
using NLog;

namespace Ghosts.Client.Infrastructure.Email;

public static class Registrar
{
    private static Logger log = LogManager.GetCurrentClassLogger();

    public static void Register(string path)
    {
        try
        {
            //’/s’ : Specifies regsvr32 to run silently and to not display any message boxes.
            var reg = new Process();
            //This file registers .dll files as command components in the registry.
            reg.StartInfo.FileName = "regsvr32.exe";
            reg.StartInfo.Arguments = "/s \"" + path + "\"";
            reg.StartInfo.UseShellExecute = false;
            reg.StartInfo.CreateNoWindow = true;
            reg.StartInfo.RedirectStandardOutput = true;
            reg.Start();
            reg.WaitForExit();
            reg.Close();
        }
        catch (Exception ex)
        {
            log.Trace(ex);
        }
    }
}