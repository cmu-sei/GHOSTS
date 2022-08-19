// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Diagnostics;
using NLog;

namespace Ghosts.Domain.Code.Helpers
{
    public static class ProcessExtensions
    {
        public static void SafeKill(this Process process)
        {
            try
            {
                process.Close();
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
