// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.IO;

namespace Ghosts.Domain.Code.Helpers
{
    public static class FileInfoExtensions
    {
        public static bool IsFileLocked(this FileInfo file)
        {
            try
            {
                using (var stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    stream.Close();
                }
            }
            catch (IOException)
            {
                //the file is unavailable because it is:
                //(1) still being written to
                //(2) being processed by another thread
                //(3) does not exist
                return true;
            }

            //file is not locked
            return false;
        }
    }
}
