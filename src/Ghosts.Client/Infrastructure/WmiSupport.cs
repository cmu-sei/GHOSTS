using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ghosts.Client.Handlers;
using Microsoft.Management.Infrastructure;
using Microsoft.Management.Infrastructure.Options;
using System.Security;
using System.IO.StreamWriter;

namespace Ghosts.Client.Infrastructure
{
    public class WmiSupport
    {
        // declare the CimSession as a field of the WmiSupportSupport class
        private CimSession? session = null;
        private readonly string _computerName;
        private readonly string _domain;
        private readonly string _username;
        private readonly string _password;
        private readonly SecureString _securepassword;

        public WmiSupportSupport(string computerName, string username, string password)
        {
            string _computerName = computerName;
            string _domain = computerName;
            string _username = username;
            string _password = password;
            _securepassword = new SecureString();
            foreach (char c in _password)
            {
                _securepassword.AppendChar(c);
            }
            // session = new CimSession();
            return;
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
                        Console.WriteLine($"Connection Test Was Successful. Continuing...");
                    }
                    else
                    {
                        Log.Error($"WMI:: Connection Test Failed!");
                    }
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
                        var GetOperatingSystem = new GetOperatingSystem(session);
                        GetOperatingSystem.Info();

                        var GetBios = new GetBios(session);
                        GetBios.Info();

                        var GetProcessor = new GetProcessor(session);
                        GetProcessor.Info();

                        var GetUserList = new GetUserList(session);
                        GetUserList.Info();

                        var GetNetworkInfo = new GetNetworkInfo(session);
                        GetNetworkInfo.Info();

                        // produces a lot of output use sparingly
                        // var GetFilesList = new GetFilesList(session);
                        // GetFilesList.Info();

                        // produces a lot of output use sparingly
                        // var GetProcessList = new GetProcessList(session);
                        // GetProcessList.Info();

                        // troubleshoot locally on system using powershell with:
                        // (Get-CimInstance -ClassName Win32_Directory -Property *).CimInstanceProperties
                        // https://powershell.one/wmi/root/cimv2/win32_directory

                        session.Close;
                    }
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
            catch
            {
                // handle any errors that occur when connecting to the remote computer
                Log.Error(
                  "Wmi:: An error occurred while connecting to the remote computer: {0}",
        

                );
                throw;
            }
        }

        public void GetBios(CimSession session)
        {
            var _session = session;
            try
            {
                // use the CimSession to create a CimInstance object representing a WMI instance
                // in this case, we're using the Win32_BIOS class to get information about the BIOS
                var instances = _session.EnumerateInstances(@ "root\cimv2", "Win32_BIOS");
                var BiosVersionList = new List<string>();
                string[] BiosVersions = BiosVersionList.ToArray();
                // print out the instance's properties to a list
                foreach (var instance in instances)
                {
                    if (null != instance.CimInstanceProperties["Version"].Value)
                    {
                        BiosVersionList.Add(instance.CimInstanceProperties["Version"].Value);
                        return;
                    }
                }
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
                var instances = _session.EnumerateInstances(@ "root\cimv2", "Win32_Processor");
                var ProcessorList = new List<string>();
                string[] ProcessorInfo = ProcessorList.ToArray();
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
                return;
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
                var instances = _session.EnumerateInstances(@ "root\cimv2", "Win32_UserAccount");
                var UserList = new List<string>();
                string[] UserInfo = UserList.ToArray();
                // print out the instance's properties to a list
                foreach (var instance in instances)
                {
                    if (null != instance.CimInstanceProperties["Name"].Value)
                    {
                        UserList.Add(instance.CimInstanceProperties["Name"].Value);
                    }
                }
                return;
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
                var instances = _session.EnumerateInstances(@ "root\cimv2", "Win32_NetworkAdapter");
                var NetworkList = new List<string>();
                string[] NetworkInfo = NetworkList.ToArray();

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
                return;
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
                var instances = _session.EnumerateInstances(@ "root\cimv2", "Win32_Directory");
                var FilesList = new List<string>();
                string[] FilesInfo = FilesList.ToArray();

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
                return;
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
                var instances = _session.EnumerateInstances(@ "root\cimv2", "Win32_Process");
                var ProcessList = new List<string>();
                string[] ProcessInfo = ProcessList.ToArray();

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
                return;
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
        return;
    }
}