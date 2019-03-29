using System;
using System.Diagnostics;
using Ghosts.Domain;
using NLog;

namespace Ghosts.Client.Code
{
    public static class GuestInfoVars
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public static void Load(ResultMachine machine)
        {
            //if confgigured, try to set machine.name based on some vmtools.exe value
            try
            {
                if (Program.Configuration.IdFormat.Equals("guestinfo", StringComparison.InvariantCultureIgnoreCase))
                {
                    var p = new Process();
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.RedirectStandardOutput = true;

                    p.StartInfo.FileName = $"{Program.Configuration.VMWareToolsLocation}";
                    p.StartInfo.Arguments = $"--cmd \"info-get {Program.Configuration.IdFormatKey}\"";

                    p.Start();

                    var output = p.StandardOutput.ReadToEnd().Trim();
                    p.WaitForExit();

                    if (!string.IsNullOrEmpty(output))
                    {
                        var o = Program.Configuration.IdFormatValue;
                        o = o.Replace("$formatkeyvalue$", output);
                        o = o.Replace("$machinename$", machine.Name);

                        machine.SetName(o);
                    }
                }
            }
            catch (Exception e)
            {
                _log.Debug(e);
            }
        }
    }
}
