using Ghosts.Client.Handlers;
using Ghosts.Client.Infrastructure;
using Microsoft.Management.Infrastructure.Options;
using Microsoft.Management.Infrastructure;
using NLog;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System;
using Renci.SshNet;
using Ghosts.Domain;

 
namespace Ghosts.Client.Infrastructure
{
    public class WmiSupport
    {
        // declare the CimSession as a field of the WmiSupportSupport class
        #pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
        private CimSession? session = null;
        #pragma warning restore CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
        private string _computerName;
        private string _domain;
        private string _username;
        private string _password;
        private SecureString _securepassword;
        private bool failconnect = false;


        public static readonly Logger Log = LogManager.GetCurrentClassLogger();
 
        public int TimeBetweenCommandsMax { get; set; } = 0;
        public int TimeBetweenCommandsMin { get; set; } = 0;

        public int CommandTimeout { get; set; } = 1000;

        public string HostIp { get; set; } = null;
        public WmiSupport()
        {
        }
        public void Init(string computerName, string username, string password, string domain = "")
        {
            _computerName = computerName;
            _domain = string.IsNullOrEmpty(domain) ? computerName : domain;
            _username = username;
            _password = password;
            _securepassword = new SecureString();
            foreach (char c in _password)
            {
                _securepassword.AppendChar(c);
            }
        }

        public void Close()
        {
            try
            {
                if (session != null)
                {
                    session.Close();
                    Log.Trace($"Wmi:: Closed session to remote host: {_computerName}");
                }
            }
            catch (ThreadAbortException)
            {
                throw;  //pass up
            }
            catch (Exception e)
            {
                Log.Trace($"Wmi:: Error closing session to remote host: {_computerName}");
                Log.Trace(e);
            }
        }
        public void Connect()
        {
            try
            {
                try
                {
                    // create Credentials
                    CimCredential Credentials = new CimCredential(
                      PasswordAuthenticationMechanism.Default,
                      _domain,
                      _username,
                      _securepassword
                    );

                    // create SessionOptions using Credentials
                    WSManSessionOptions SessionOptions = new WSManSessionOptions();
                    SessionOptions.AddDestinationCredentials(Credentials);

                    // create Session using computer, SessionOptions
                    // use the field declared above to store the CimSession in the WmiSupportSupport object
                    session = CimSession.Create(_computerName, SessionOptions);

                    if (session.TestConnection())
                    {
                        Log.Trace($"Wmi:: Connection Test Was Successful to {_computerName}.  Continuing...");
                    }
                    else
                    {
                        Log.Error($"WMI:: Connection Test Failed!");
                        failconnect = true;
                    }
                }
                catch (ThreadAbortException)
                {
                    throw;  //pass up
                }
                catch
                {
                    Log.Error(
                        $"WMI:: Failed to create session with remote host. Please check that the system is configured for WMI connections and double check that your credentials are accurate \n Additionally try adding the system to the trustedhost store for WMI with the following command: \n winrm s winrm/config/client '@{{TrustedHosts = system_name}}'"
                    );
                }

                // output
                // do stuff

                try
                {
                    if (null != session)
                    {
                        //GetOperatingSystem(session);
                        //GetBios(session);
                        //GetProcessor(session);
                        //GetUserList(session);
                        //GetNetworkInfo(session);
                        //GetFilesList(session);
                        //GetProcessList(session);

                        // troubleshoot locally on system using powershell with:
                        // (Get-CimInstance -ClassName Win32_Directory -Property *).CimInstanceProperties
                        // https://powershell.one/wmi/root/cimv2/win32_directory
                    }
                }
                catch (ThreadAbortException)
                {
                    throw;  //pass up
                }
                catch (CimException ex)
                {
                    // handle any errors that occur when connecting to the remote computer
                    Log.Error(
                      "Wmi:: An error occurred while connecting to the remote computer: {0}",
                      ex.Message
                    );
                    throw;
                }
            }
            catch (ThreadAbortException)
            {
                throw;  //pass up
            }
            catch
            {
                // handle any errors that occur when connecting to the remote computer
                Log.Error(
                  "Wmi:: An error occurred while connecting to the remote computer: {0}"
                );
                throw;
            }
        }
        public void GetOperatingSystem(CimSession session)
        {
            var _session = session;
            try
            {
                // use the CimSession to create a CimInstance object representing a WMI instance
                // in this case, we're using the Win32_OperatingSystem class to get information about the operating system
                var cimInstance = new CimInstance(@"Win32_OperatingSystem");
                var instance = _session.GetInstance(@"root\cimv2", cimInstance);
                var OsInfoList = new List<object>();
                object[] OsInfo = OsInfoList.ToArray();
                // print out the instance's properties
                if (null != instance.CimInstanceProperties["Name"].Value)
                {
                    OsInfoList.Add(
                        instance.CimInstanceProperties["Name"].Value
                    );
                }
                if (null != instance.CimInstanceProperties["Version"].Value)
                {
                    OsInfoList.Add(
                        instance.CimInstanceProperties["Version"].Value
                    );
                }
                if (null != instance.CimInstanceProperties["InstallDate"].Value)
                {
                    OsInfoList.Add(
                        instance.CimInstanceProperties["InstallDate"].Value
                    );
                }
                Log.Trace($"Wmi:: Success, Cmd: GetOperatingSystem, remote host: {_computerName}");
            }
            catch (ThreadAbortException)
            {
                throw;  //pass up
            }
            catch (CimException ex)
            {
                // handle any errors that occur when querying the remote computer
                Log.Error(
                    "Failed on OperatingSystemOutput: An error occurred while querying the remote computer: {0}",
                    ex.Message
                );
            }
        }
        public void GetBios(CimSession session)
        {
            var _session = session;
            try
            {
                // use the CimSession to create a CimInstance object representing a WMI instance
                // in this case, we're using the Win32_BIOS class to get information about the BIOS
                var instances = _session.EnumerateInstances(@"root\cimv2", "Win32_BIOS");
                var BiosVersionList = new List<object>();
                object[] BiosVersions = BiosVersionList.ToArray();
                // print out the instance's properties to a list
                foreach (var instance in instances)
                {
                    if (null != instance.CimInstanceProperties["Version"].Value)
                    {
                        BiosVersionList.Add(instance.CimInstanceProperties["Version"].Value);
                        return;
                    }
                }
                Log.Trace($"Wmi:: Success, Cmd: GetBios, remote host: {_computerName}");
            }
            catch (ThreadAbortException)
            {
                throw;  //pass up
            }
            catch (CimException ex)
            {
                // handle any errors that occur when querying the remote computer
                Log.Error(
                  "Failed on GetBios: An error occurred while querying the remote computer: {0}",
                  ex.Message
                );
            }
        }

        public void GetProcessor(CimSession session)
        {
            var _session = session;

            try
            {
                // use the CimSession to create a CimInstance object representing a WMI instance
                // in this case, we're using the Win32_Processor class to get information about the processor
                var instances = _session.EnumerateInstances(@"root\cimv2", "Win32_Processor");
                var ProcessorList = new List<object>();
                object[] ProcessorInfo = ProcessorList.ToArray();
                // print out the instance's properties to a list
                foreach (var instance in instances)
                {
                    if (null != instance.CimInstanceProperties["Name"].Value)
                    {
                        ProcessorList.Add(instance.CimInstanceProperties["Version"].Value);
                    }
                    if (null != instance.CimInstanceProperties["Manufacturer"].Value)
                    {
                        ProcessorList.Add(instance.CimInstanceProperties["Manufacturer"].Value);
                    }
                }
                Log.Trace($"Wmi:: Success, Cmd: GetProcessor, remote host: {_computerName}");
                return;
            }
            catch (ThreadAbortException)
            {
                throw;  //pass up
            }
            catch (CimException ex)
            {
                // handle any errors that occur when querying the remote computer
                Log.Error(
                  "WMI:: Failed on GetProcessor: An error occurred while querying the remote computer: {0}",
                  ex.Message
                );
            }
        }

        public void GetUserList(CimSession session)
        {
            var _session = session;

            try
            {
                // use the CimSession to create a CimInstance object representing a WMI instance
                // in this case, we're using the Win32_UserAccount class to get information about users on the system
                var instances = _session.EnumerateInstances(@"root\cimv2", "Win32_UserAccount");
                var UserList = new List<object>();
                object[] UserInfo = UserList.ToArray();
                // print out the instance's properties to a list
                foreach (var instance in instances)
                {
                    if (null != instance.CimInstanceProperties["Name"].Value)
                    {
                        UserList.Add(instance.CimInstanceProperties["Name"].Value);
                    }
                }
                Log.Trace($"Wmi:: Success, Cmd: GetUserList, remote host: {_computerName}");
                return;
            }
            catch (ThreadAbortException)
            {
                throw;  //pass up
            }
            catch (CimException ex)
            {
                // handle any errors that occur when querying the remote computer
                Log.Error(
                  "WMI:: Failed on GetUserList: An error occurred while querying the remote computer: {0}",
                  ex.Message
                );

            }
        }

        public void GetNetworkInfo(CimSession session)
        {
            var _session = session;

            try
            {
                // use the CimSession to create a CimInstance object representing a WMI instance
                // in this case, we're using the Win32_NetworkAdapter class to get information about network devices on the system
                var instances = _session.EnumerateInstances(@"root\cimv2", "Win32_NetworkAdapter");
                var NetworkList = new List<object>();
                object[] NetworkInfo = NetworkList.ToArray();

                // print out the network information for each network device to a list
                foreach (var instance in instances)
                {
                    if (null != instance.CimInstanceProperties["Name"].Value)
                    {
                        NetworkList.Add(
                          instance.CimInstanceProperties["Name"].Value
                        );
                    }
                    if (null != instance.CimInstanceProperties["MACAddress"].Value)
                    {
                        NetworkList.Add(
                          instance.CimInstanceProperties["MACAddress"].Value
                        );
                    }
                    if (null != instance.CimInstanceProperties["NetworkAddresses"].Value)
                    {
                        NetworkList.Add(
                          instance.CimInstanceProperties["NetworkAddresses"].Value
                        );
                    }
                }
                Log.Trace($"Wmi:: Success, Cmd: GetNetworkInfo, remote host: {_computerName}");
                return;
            }
            catch (ThreadAbortException)
            {
                throw;  //pass up
            }
            catch (CimException ex)
            {
                // handle any errors that occur when querying the remote computer
                Log.Error(
                  "WMI:: Failed on NetworkListOutput: An error occurred while querying the remote computer: {0}",
                  ex.Message
                );

            }
        }

        public void GetFilesList(CimSession session)
        {
            var _session = session;

            try
            {
                // use the CimSession to create a CimInstance object representing a WMI instance
                // in this case, we're using the Win32_Directory class to get information about a directory
                var instances = _session.EnumerateInstances(@"root\cimv2", "Win32_Directory");
                var FilesList = new List<object>();
                object[] FilesInfo = FilesList.ToArray();

                // print out the list of users on the system to list
                foreach (var instance in instances)
                {
                    if (null != instance.CimInstanceProperties["Name"].Value)
                    {
                        FilesList.Add(
                          instance.CimInstanceProperties["Name"].Value
                        );
                    }
                }
                Log.Trace($"Wmi:: Success, Cmd: GetFilesList, remote host: {_computerName}");
                return;
            }
            catch (ThreadAbortException)
            {
                throw;  //pass up
            }
            catch (CimException ex)
            {
                // handle any errors that occur when querying the remote computer
                Log.Error(
                  "WMI:: Failed on GetFilesList: An error occurred while querying the remote computer: {0}",
                  ex.Message
                );
            }
        }

        public void GetProcessList(CimSession session)
        {
            var _session = session;

            try
            {
                // use the CimSession to create a CimInstance object representing a WMI instance
                // in this case, we're using the Win32_Process class to get information about processes running on the system
                var instances = _session.EnumerateInstances(@"root\cimv2", "Win32_Process");
                var ProcessList = new List<object>();
                object[] ProcessInfo = ProcessList.ToArray();

                // print out the list of processes to list
                foreach (var instance in instances)
                {
                    if (null != instance.CimInstanceProperties["Name"].Value)
                    {
                        ProcessList.Add(
                          instance.CimInstanceProperties["Name"].Value
                        );
                    }
                    if (null != instance.CimInstanceProperties["ProcessId"].Value)
                    {
                        ProcessList.Add(
                          instance.CimInstanceProperties["ProcessId"].Value
                        );
                    }
                }
                Log.Trace($"Wmi:: Success, Cmd: GetProcessList, remote host: {_computerName}");
                return;
            }
            catch (ThreadAbortException)
            {
                throw;  //pass up
            }
            catch (CimException ex)
            {
                // handle any errors that occur when querying the remote computer
                Log.Error(
                  "WMI:: Failed on GetProcessList: An error occurred while querying the remote computer: {0}",
                  ex.Message
                );
            }
        }

        public void RunWmiCommand(string cmd)
        {
            if (failconnect == true)
            {
                return;
            }
            else
            {
                Log.Trace($"Wmi:: Running Command {cmd}");
                if (cmd == "GetOperatingSystem")
                {
                    GetOperatingSystem(session);
                    return;
                }
                else if (cmd == "GetBios")
                {
                    GetBios(session);
                    return;
                }
                else if (cmd == "GetProcessor")
                {
                    GetProcessor(session);
                    return;
                }
                else if (cmd == "GetUserList")
                {
                    GetUserList(session);
                    return;
                }
                else if (cmd == "GetNetworkInfo")
                {
                    GetNetworkInfo(session);
                    return;
                }
                else if (cmd == "GetFilesList")
                {
                    GetFilesList(session);
                    return;
                }
                else if (cmd == "GetProcessList")
                {
                    GetProcessList(session);
                    return;
                }
                else
                {
                    Log.Trace($"Wmi::Unsupported command, execution skipped : {cmd}.");
                }
                return;
            }
        }
    }
}