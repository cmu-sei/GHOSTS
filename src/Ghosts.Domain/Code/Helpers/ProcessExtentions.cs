// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Diagnostics;
using NLog;

namespace Ghosts.Domain.Code.Helpers
{
    public static class ProcessExtensions
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public static void SafeKill(this Process process)
        {
            try
            {
                process.Close();
            }
            catch (Exception exc)
            {
                _log.Trace($"Process {process.ProcessName} proving hard to close {exc.Message}");
                try
                {
                    process.Kill();
                }
                catch
                {
                    _log.Trace($"Process {process.ProcessName} could not be killed {exc.Message}");
                }
            }
            finally
            {
                try
                {
                    process.WaitForExit();
                }
                catch (Exception exc)
                {
                    _log.Trace($"Process {process.ProcessName} proving that the waiting is indeed the hardest part {exc.Message}");
                }

                try
                {
                    
                    process.Dispose();
                    process = null;
                }
                catch (Exception exc)
                {
                    _log.Trace($"Process {process.ProcessName} could not be disposed {exc.Message}");
                }
            }
        }
    }
}
