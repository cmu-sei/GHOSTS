// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Diagnostics;

namespace Ghosts.Domain.Code.Helpers
{
    public static class ProcessExtensions
    {
        public static void SafeKill(this Process process)
        {
            try
            {
                var info = new ProcessStartInfo
                {
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    FileName = "taskkill",
                    Arguments = $"/pid {process.Id} /F /T"
                };
                Process.Start(info);

            }
            catch
            {
                try
                {
                    process.Kill();
                }
                catch
                {
                    // ignore
                }
            }
            finally
            {
                try
                {
                    process.Dispose();
                    process = null;
                }
                catch
                {
                    // ignore
                }
            }
        }
    }
}
