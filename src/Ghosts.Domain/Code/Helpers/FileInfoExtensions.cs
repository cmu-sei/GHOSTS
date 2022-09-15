// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.IO;
using NLog;

namespace Ghosts.Domain.Code.Helpers
{
    public static class FileInfoExtensions
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public static bool IsFileLocked(this FileInfo file)
        {
            try
            {
                using (var stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    stream.Close();
                }
            }
            catch (IOException e)
            {
                //the file is unavailable because it is:
                //(1) still being written to
                //(2) being processed by another thread
                //(3) does not exist
                _log.Trace($"{file.FullName}: {e}");
                return true;
            }

            //file is not locked
            return false;
        }
    }
}
