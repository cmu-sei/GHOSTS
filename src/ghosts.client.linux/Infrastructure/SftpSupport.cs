using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Renci.SshNet;
using Renci.SshNet.Sftp;


namespace ghosts.client.linux.Infrastructure
{
    public class SftpSupport : SshSftpSupport
    {

        public string uploadDirectory { get; set; } = null;
        public string downloadDirectory { get; set; } = null;


        public string GetUploadFilename()
        {
            return GetUploadFilenameBase(uploadDirectory, "*");
        }

        public static SftpFile GetRemoteFile(SftpClient client)
        {

            try
            {
                var remoteFiles = client.ListDirectory(".").OfType<SftpFile>().ToList();
                List<SftpFile> normalFiles = new List<SftpFile>();
                foreach (var f in remoteFiles)
                {
                    if (f.IsDirectory || f.IsSymbolicLink) continue;
                    if (f.IsRegularFile) normalFiles.Add(f);
                }
                if (normalFiles.Count > 0)
                {
                    return normalFiles[_random.Next(0, normalFiles.Count)];
                }
            }
            catch (ThreadAbortException)
            {
                throw;  //pass up
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
            return null;

        }

        public static SftpFile GetRemoteDir(SftpClient client)
        {

            try
            {
                var remoteFiles = client.ListDirectory(".").OfType<SftpFile>().ToList();
                List<SftpFile> normalFiles = new List<SftpFile>();
                foreach (var f in remoteFiles)
                {
                    if (f.IsRegularFile || f.IsSymbolicLink) continue;
                    if (f.IsDirectory) normalFiles.Add(f);
                }
                if (normalFiles.Count > 0)
                {
                    return normalFiles[_random.Next(0, remoteFiles.Count)];
                }
            }
            catch (ThreadAbortException)
            {
                throw;  //pass up
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
            return null;

        }

        public static SftpFile FindDir(SftpClient client, string targetdir)
        {

            try
            {
                var remoteFiles = client.ListDirectory(".").OfType<SftpFile>().ToList();
                List<SftpFile> normalFiles = new List<SftpFile>();
                foreach (var f in remoteFiles)
                {
                    if (f.IsRegularFile || f.IsSymbolicLink) continue;
                    if (f.IsDirectory && f.Name == targetdir) return f;
                }
            }
            catch (ThreadAbortException)
            {
                throw;  //pass up
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
            return null; //target directory does not exist
        }


        /// <summary>
        /// Expecting a string of 'put filename'
        /// </summary>
        /// <param name="client"></param>
        /// <param name="cmd"></param>
        public void DoPut(SftpClient client, string cmd)
        {
            var charSeparators = new char[] { ' ' };
            var cmdArgs = cmd.Split(charSeparators, 2, StringSplitOptions.None);
            if (cmdArgs.Length != 2)
            {
                Log.Trace($"Sftp:: ill-formatted put command: {cmd} ");
                return;
            }
            var fileName = cmdArgs[1];
            if (fileName.Contains("[localfile]"))
            {
                fileName = GetUploadFilename();
                if (fileName == null)
                {
                    Log.Trace($"Sftp:: Cannot find a valid file to upload from directory {uploadDirectory}.");
                    return;
                }
            }

            try
            {
                using (var fileStream = System.IO.File.OpenRead(fileName))
                {
                    var components = fileName.Split(Path.DirectorySeparatorChar);
                    var remoteFileName = components[components.Length - 1];
                    client.UploadFile(fileStream, remoteFileName, true);
                    Log.Trace($"Sftp:: Success, Uploaded local file {fileName} to file {remoteFileName}, host {HostIp} ");
                    fileStream.Close();
                }
            }
            catch (ThreadAbortException)
            {
                throw;  //pass up
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        /// <summary>
        /// Expecting a string of 'rm filename'
        /// </summary>
        /// <param name="client"></param>
        /// <param name="cmd"></param>
        public void DoRemoveFile(SftpClient client, string cmd)
        {
            var charSeparators = new char[] { ' ' };
            var cmdArgs = cmd.Split(charSeparators, 2, StringSplitOptions.None);
            if (cmdArgs.Length != 2)
            {
                Log.Trace($"Sftp:: ill-formatted rm command: {cmd} ");
                return;
            }
            var fileName = cmdArgs[1];

            if (fileName.Contains("[remotefile]"))
            {
                var file = GetRemoteFile(client);
                if (file == null)
                {
                    Log.Trace($"Sftp:: Cannot find a valid file to delete from remote host {HostIp}.");
                    return;
                }
                fileName = file.FullName;
            }

            //now delete the remote file
            try
            {
                client.DeleteFile(fileName);
                Log.Trace($"Sftp:: Success, Deleted {fileName} on remote host {HostIp}.");
            }
            catch (ThreadAbortException)
            {
                throw;  //pass up
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        /// <summary>
        /// Expecting a string of 'get filename'
        /// </summary>
        /// <param name="client"></param>
        /// <param name="cmd"></param>
        public void DoGet(SftpClient client, string cmd)
        {
            var charSeparators = new char[] { ' ' };
            var cmdArgs = cmd.Split(charSeparators, 2, StringSplitOptions.None);
            if (cmdArgs.Length != 2)
            {
                Log.Trace($"Sftp:: ill-formatted get command: {cmd} ");
                return;
            }
            var fileName = cmdArgs[1];
            string localFilePath = null;
            string remoteFilePath = null;
            if (fileName.Contains("[remotefile]"))
            {
                var file = GetRemoteFile(client);
                if (file == null)
                {
                    Log.Trace($"Sftp:: Cannot find a valid file to download from remote host {HostIp}.");
                    return;
                }
                remoteFilePath = file.FullName;
                localFilePath = Path.Combine(downloadDirectory, file.Name);
            }
            else
            {
                var seperator = '\\';
                if (fileName.Contains('\\'))
                {
                    //assume this the full path to a windows box
                    remoteFilePath = fileName;
                }
                else if (fileName.Contains('/'))
                {
                    remoteFilePath = fileName;
                    seperator = '/';
                }
                if (remoteFilePath == null)
                {
                    //a local name
                    remoteFilePath = fileName;
                    localFilePath = Path.Combine(downloadDirectory, fileName);
                }
                else
                {
                    remoteFilePath = fileName;
                    //parse fullpath to get local name
                    var components = fileName.Split(seperator);
                    localFilePath = Path.Combine(downloadDirectory, components[components.Length - 1]);
                }
            }
            //at this point, localFilePath, remoteFilePath are set
            if (System.IO.File.Exists(localFilePath))
            {
                try
                {
                    //delete the file if exists locally
                    System.IO.File.Delete(localFilePath);
                }
                catch (ThreadAbortException)
                {
                    throw;  //pass up
                }
                catch (Exception e)
                {
                    Log.Error(e);
                    return;
                }
            }
            //now download the file
            try
            {
                using (var fileStream = System.IO.File.OpenWrite(localFilePath))
                {
                    client.DownloadFile(remoteFilePath, fileStream);
                    Log.Trace($"Sftp:: Success, Downloaded remote file {remoteFilePath},host {HostIp}  to file {localFilePath},  ");
                    fileStream.Close();
                }
            }
            catch (ThreadAbortException)
            {
                throw;  //pass up
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        public void DoChangeDir(SftpClient client, string cmd)
        {
            var charSeparators = new char[] { ' ' };
            var cmdArgs = cmd.Split(charSeparators, 2, StringSplitOptions.None);
            if (cmdArgs.Length != 2)
            {
                Log.Trace($"Sftp:: ill-formatted cd command: {cmd} ");
                return;
            }
            var dirName = cmdArgs[1];
            if (dirName.Contains("[remotedir]"))
            {
                var file = GetRemoteDir(client);
                if (file == null)
                {
                    Log.Trace($"Sftp:: Cannot find a valid directory to change to on remote host {HostIp}.");
                    return;
                }
                dirName = file.FullName;
            }

            try
            {
                client.ChangeDirectory(dirName);
                Log.Trace($"Sftp:: Success, Changed to directory {dirName} on remote host {HostIp}.");
            }
            catch (ThreadAbortException)
            {
                throw;  //pass up
            }
            catch (Exception e)
            {
                Log.Error(e);
            }

        }

        public void DoMakeDir(SftpClient client, string cmd)
        {
            var charSeparators = new char[] { ' ' };
            var cmdArgs = cmd.Split(charSeparators, 2, StringSplitOptions.None);
            if (cmdArgs.Length != 2)
            {
                Log.Trace($"Sftp:: ill-formatted mkdir command: {cmd} ");
                return;
            }
            var dirName = cmdArgs[1];
            if (dirName.Contains("[randomname]"))
            {
                dirName = RandomString(7, 10, true);
            }

            try
            {
                if (FindDir(client, dirName) == null)
                {
                    client.CreateDirectory(dirName);
                    Log.Trace($"Sftp:: Success, Created directory {dirName} on remote host {HostIp}.");
                }
                else
                {
                    Log.Trace($"Sftp:: mkdir directory command skipped, as {dirName} already exists remote host {HostIp}.");
                }
            }
            catch (ThreadAbortException)
            {
                throw;  //pass up
            }
            catch (Exception e)
            {
                Log.Error(e);
            }

        }

        public void DoListDir(SftpClient client, string cmd)
        {
            var charSeparators = new char[] { ' ' };
            var cmdArgs = cmd.Split(charSeparators, 2, StringSplitOptions.None);

            var dirName = ".";
            if (cmdArgs.Length == 2) dirName = cmdArgs[1];
            if (dirName.Contains("[remotedir]"))
            {
                var file = GetRemoteDir(client);
                if (file == null)
                {
                    Log.Trace($"Sftp:: Cannot find a valid directory to list in current working directory on remote host {HostIp}.");
                    return;
                }
                dirName = file.FullName;
            }

            try
            {
                var remoteFiles = client.ListDirectory(dirName).OfType<SftpFile>().ToList();
                Log.Trace($"Sftp:: Success, Found {remoteFiles.Count} in directory {dirName} on remote host {HostIp}.");
            }
            catch (ThreadAbortException)
            {
                throw;  //pass up
            }
            catch (Exception e)
            {
                Log.Error(e);
            }

        }

        /// <summary>
        /// Supported commands:
        /// get [remotefile] - downloads random remote file from remote host. Can specify absolute/relative path instead of [remotefile]
        /// put [localfile] - uploads random remote file from local upload directory to remote host. Can specify absolute/relative path instead of [localfile]
        /// cd [remotedir] - change to random directory in current directory on remote host. Can specify absolute/relative path instead of [remotedir]
        /// rm [remotefile] - deletes random remote file from remote host. Can specify absolute/relative path instead of [remotefile]
        /// ls [remotedir] - list remote contents of current directory, if no directory specified use current directory. Can specify absolute/relative path instead of [remotedir]
        /// mkdir [randomname] - make a random directory in cwd on remote host. Can specify absolute/relative path instead of [randomname]
        /// </summary>
        /// <param name="client"></param>
        /// <param name="cmd"></param>

        public void RunSftpCommand(SftpClient client, string cmd)
        {

            if (cmd.StartsWith("put"))
            {
                DoPut(client, cmd);
                return;
            }
            else if (cmd.StartsWith("get"))
            {
                DoGet(client, cmd);
                return;
            }
            else if (cmd.StartsWith("cd"))
            {
                DoChangeDir(client, cmd);
                return;
            }
            else if (cmd.StartsWith("ls"))
            {
                DoListDir(client, cmd);
                return;
            }
            else if (cmd.StartsWith("rm"))
            {
                DoRemoveFile(client, cmd);
                return;
            }
            else if (cmd.StartsWith("mkdir"))
            {
                DoMakeDir(client, cmd);
                return;
            }
            else
            {
                Log.Trace($"Sftp::Unsupported command, execution skipped : {cmd}.");
            }



            return;
        }



    }
}
