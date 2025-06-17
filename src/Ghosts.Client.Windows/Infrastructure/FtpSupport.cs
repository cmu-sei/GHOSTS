using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Ghosts.Client.Handlers;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using OpenQA.Selenium;

namespace Ghosts.Client.Infrastructure
{
    public class FtpSupport : SshSftpSupport
    {
        public string uploadDirectory { get; set; } = null;
        public string downloadDirectory { get; set; } = null;

        public int deletionProbability { get; set; } = 20;
        public int uploadProbability { get; set; } = 40;
        public int downloadProbability { get; set; } = 40;


        public string GetUploadFilename()
        {
            return GetUploadFilenameBase(uploadDirectory, "*");
        }

        public List<string> DoFtpListDir(string hostip, NetworkCredential cred)
        {
            FtpWebRequest request = (FtpWebRequest)WebRequest.Create($"ftp://{hostip}/");
            request.Credentials = cred;
            request.Method = WebRequestMethods.Ftp.ListDirectory;
            FtpWebResponse response = (FtpWebResponse)request.GetResponse();
            if (response != null)
            {
                char[] charSeparators = new char[] { '\n' };
                Stream responseStream = response.GetResponseStream();
                StreamReader reader = new StreamReader(responseStream);
                string data = reader.ReadToEnd();
                data = data.Replace("\r\n", "\n");
                string[] files = data.Split(charSeparators);
                List<string> filelist = new List<string>();
                foreach (string fname in files)
                {
                    if (fname != "") filelist.Add(fname);
                }
                reader.Close();
                response.Close();
                Log.Trace($"Ftp:: Success, read FTP directory contents from host {hostip}");
                return filelist;
            }
            return null;
        }

        // This returns the name of a random remote file
        public string GetRemoteFile(string hostip, NetworkCredential cred)
        {
            var remoteFiles = DoFtpListDir(hostip, cred);
            if (remoteFiles != null && remoteFiles.Count > 0)
            {
                return remoteFiles[_random.Next(0, remoteFiles.Count())];
            }
            return null;
        }

        public void DoDelete(string hostip, NetworkCredential cred)
        {

            var file = GetRemoteFile(hostip, cred);
            if (file == null)
            {
                Log.Trace($"Ftp:: Cannot find a valid file to delete from remote host {hostip}.");
                return;
            }
            var remoteFilePath = file;
            
            //now delete the file
            try
            {
                FtpWebRequest request = (FtpWebRequest)WebRequest.Create($"ftp://{hostip}/{remoteFilePath}");
                request.Credentials = cred;
                request.Method = WebRequestMethods.Ftp.DeleteFile;
                FtpWebResponse response = (FtpWebResponse)request.GetResponse();
                if (response != null)
                {
                    response.Close();
                    Log.Trace($"Ftp:: Success, deleted remote file {remoteFilePath} from host {hostip}");

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




        public void DoGet(string hostip, NetworkCredential cred)
        {
           
            var file = GetRemoteFile(hostip,cred);
            if (file == null)
            {
                Log.Trace($"Ftp:: Cannot find a valid file to download from remote host {hostip}.");
                return;
            }
            var remoteFilePath = file ;
            var localFilePath = Path.Combine(downloadDirectory, file);
            
           
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
                byte[] bytes = new byte[2048];
                FtpWebRequest request = (FtpWebRequest)WebRequest.Create($"ftp://{hostip}/{remoteFilePath}");
                request.Credentials = cred;
                request.Method = WebRequestMethods.Ftp.DownloadFile;
                FtpWebResponse response = (FtpWebResponse)request.GetResponse();
                if (response != null)
                {
                    using (FileStream fileStream = File.Create(localFilePath))
                    {

                        Stream responseStream = response.GetResponseStream();
                        
                        while (true)
                        {
                            int n = responseStream.Read(bytes, 0, 2048);
                            if (n == 0) break;
                            fileStream.Write(bytes, 0, n);
                        }
                        responseStream.Close();

                        
                    }
                    response.Close();
                    Log.Trace($"Ftp:: Success, Downloaded remote file {remoteFilePath},host {hostip}  to file {localFilePath},  ");
                    
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

       

        public void writeTask(string hostip, NetworkCredential cred, string fileName)
        {
            var components = fileName.Split(Path.DirectorySeparatorChar);
            var remoteFileName = components[components.Length - 1];
            FtpWebRequest request = (FtpWebRequest)WebRequest.Create($"ftp://{hostip}/{remoteFileName}");
            request.Credentials = cred; 
            request.Method= WebRequestMethods.Ftp.UploadFile;
           
            FtpWebResponse response = (FtpWebResponse)request.GetResponse();

            if (response != null)
            {
                byte[] bytes = new byte[2048];
                using (FileStream fileStream = File.Open(fileName, FileMode.Open, FileAccess.Read))
                {
                    using (Stream requestStream = request.GetRequestStream())
                    {
                        
                        int numBytesToRead = (int)fileStream.Length;
                        int numBytesRead = 0;
                        int chunkSize;
                        while (numBytesToRead > 0)
                        {
                            chunkSize = numBytesToRead;
                            if (chunkSize > 2048)
                            {
                                chunkSize = 2048;
                            } 
                            int n = fileStream.Read(bytes, 0, chunkSize);
                            if (n == 0) break;
                            requestStream.Write(bytes, 0, n);
                            numBytesRead += n;
                            numBytesToRead -= n;
                        }
                        Log.Trace($"Ftp:: Success, Uploaded local file {fileName} to file {remoteFileName}, host {hostip}");
                    }
                }
                response.Close();
            }
        }

        /// <summary>
        /// Expecting a string of 'put filename'
        /// </summary>
        /// <param name="client"></param>
        /// <param name="cmd"></param>
        public void DoPut(string hostip, NetworkCredential cred)
        {
            
            var fileName = GetUploadFilename();
            if (fileName == null)
            {
                Log.Trace($"Ftp:: Cannot find a valid file to upload from directory {uploadDirectory}.");
                return;
            }
            
            try
            {
                writeTask(hostip, cred, fileName);
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

        public void RunFtpCommand(string hostip, NetworkCredential cred, string cmd)
        {
            if (cmd == "upload")
            {
                DoPut(hostip, cred);
                return;
            }
            else if (cmd == "download")
            {
                DoGet(hostip, cred);
                return;
            }
            
            else if (cmd == "delete")
            {
                DoDelete(hostip, cred);
                return;
            }
            else
            {
                Log.Trace($"FTP::Unsupported command, execution skipped : {cmd}.");
            }

        }


    }
}
