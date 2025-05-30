// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using NLog;
using Redemption;

namespace Ghosts.Client.Infrastructure.Email;

public static class RedemptionLoader
{
    private static Logger log = LogManager.GetCurrentClassLogger();

    #region public methods
    //64 bit dll location - defaults to <assemblydir>\Redemption64.dll
    public static string DllLocation64Bit;
    //32 bit dll location - defaults to <assemblydir>\Redemption.dll
    public static string DllLocation32Bit;


    //The only creatable RDO object - RDOSession
    public static RDOSession new_RDOSession()
    {
        return (RDOSession)NewRedemptionObject(new Guid("29AB7A12-B531-450E-8F7A-EA94C2F3C05F"));
    }

    //Safe*Item objects
    public static SafeMailItem new_SafeMailItem()
    {
        return (SafeMailItem)NewRedemptionObject(new Guid("741BEEFD-AEC0-4AFF-84AF-4F61D15F5526"));
    }

    public static SafeContactItem new_SafeContactItem()
    {
        return (SafeContactItem)NewRedemptionObject(new Guid("4FD5C4D3-6C15-4EA0-9EB9-EEE8FC74A91B"));
    }

    public static SafeAppointmentItem new_SafeAppointmentItem()
    {
        return (SafeAppointmentItem)NewRedemptionObject(new Guid("620D55B0-F2FB-464E-A278-B4308DB1DB2B"));
    }

    public static SafeTaskItem new_SafeTaskItem()
    {
        return (SafeTaskItem)NewRedemptionObject(new Guid("7A41359E-0407-470F-B3F7-7C6A0F7C449A"));
    }

    public static SafeJournalItem new_SafeJournalItem()
    {
        return (SafeJournalItem)NewRedemptionObject(new Guid("C5AA36A1-8BD1-47E0-90F8-47E7239C6EA1"));
    }

    public static SafeMeetingItem new_SafeMeetingItem()
    {
        return (SafeMeetingItem)NewRedemptionObject(new Guid("FA2CBAFB-F7B1-4F41-9B7A-73329A6C1CB7"));
    }

    public static SafePostItem new_SafePostItem()
    {
        return (SafePostItem)NewRedemptionObject(new Guid("11E2BC0C-5D4F-4E0C-B438-501FFE05A382"));
    }

    public static SafeReportItem new_SafeReportItem()
    {
        return (SafeReportItem)NewRedemptionObject(new Guid("D46BA7B2-899F-4F60-85C7-4DF5713F6F18"));
    }

    public static MAPIFolder new_MAPIFolder()
    {
        return (MAPIFolder)NewRedemptionObject(new Guid("03C4C5F4-1893-444C-B8D8-002F0034DA92"));
    }

    public static SafeCurrentUser new_SafeCurrentUser()
    {
        return (SafeCurrentUser)NewRedemptionObject(new Guid("7ED1E9B1-CB57-4FA0-84E8-FAE653FE8E6B"));
    }

    public static SafeDistList new_SafeDistList()
    {
        return (SafeDistList)NewRedemptionObject(new Guid("7C4A630A-DE98-4E3E-8093-E8F5E159BB72"));
    }

    public static AddressLists new_AddressLists()
    {
        return (AddressLists)NewRedemptionObject(new Guid("37587889-FC28-4507-B6D3-8557305F7511"));
    }

    public static MAPITable new_MAPITable()
    {
        return (MAPITable)NewRedemptionObject(new Guid("A6931B16-90FA-4D69-A49F-3ABFA2C04060"));
    }

    public static MAPIUtils new_MAPIUtils()
    {
        return (MAPIUtils)NewRedemptionObject(new Guid("4A5E947E-C407-4DCC-A0B5-5658E457153B"));
    }

    public static SafeInspector new_SafeInspector()
    {
        return (SafeInspector)NewRedemptionObject(new Guid("ED323630-B4FD-4628-BC6A-D4CC44AE3F00"));
    }

    public static SafeExplorer new_SafeExplorer()
    {
        return (SafeExplorer)NewRedemptionObject(new Guid("C3B05695-AE2C-4FD5-A191-2E4C782C03E0"));
    }

    #endregion


    #region private methods



    static RedemptionLoader()
    {
        //default locations of the dlls

        //use CodeBase instead of Location because of Shadow Copy.
        string codebase = Assembly.GetExecutingAssembly().CodeBase;
        var vUri = new UriBuilder(codebase);
        string vPath = Uri.UnescapeDataString(vUri.Path + vUri.Fragment);
        string directory = Path.GetDirectoryName(vPath);
        if (!string.IsNullOrEmpty(vUri.Host)) directory = @"\\" + vUri.Host + directory;
        DllLocation64Bit = Path.Combine(directory, "redemption64.dll");
        DllLocation32Bit = Path.Combine(directory, "redemption.dll");
    }



    /*
    static ~Loader()
    {
        if (!_redemptionDllHandle.Equals(IntPtr.Zero))
        {
            IntPtr dllCanUnloadNowPtr = Win32NativeMethods.GetProcAddress(_redemptionDllHandle, "DllCanUnloadNow");
            if (!dllCanUnloadNowPtr.Equals(IntPtr.Zero))
            {
                DllCanUnloadNow dllCanUnloadNow = (DllCanUnloadNow)Marshal.GetDelegateForFunctionPointer(dllCanUnloadNowPtr, typeof(DllCanUnloadNow));
                if (dllCanUnloadNow() != 0) return; //there are still live objects returned by the dll, so we should not unload the dll
            }
            Win32NativeMethods.FreeLibrary(_redemptionDllHandle);
            _redemptionDllHandle = IntPtr.Zero;
        }
    }
    */


    [ComVisible(false)]
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("00000001-0000-0000-C000-000000000046")]
    private interface IClassFactory
    {
        void CreateInstance([MarshalAs(UnmanagedType.Interface)] object pUnkOuter, ref Guid refiid, [MarshalAs(UnmanagedType.Interface)] out object ppunk);
        void LockServer(bool fLock);
    }

    [ComVisible(false)]
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("00000000-0000-0000-C000-000000000046")]
    private interface IUnknown
    {
    }

    private delegate int DllGetClassObject(ref Guid ClassId, ref Guid InterfaceId, [Out, MarshalAs(UnmanagedType.Interface)] out object ppunk);
    private delegate int DllCanUnloadNow();

    //COM GUIDs
    private static Guid IID_IClassFactory = new Guid("00000001-0000-0000-C000-000000000046");
    private static Guid IID_IUnknown = new Guid("00000000-0000-0000-C000-000000000046");

    //win32 functions to load\unload dlls and get a function pointer 
    private class Win32NativeMethods
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr LoadLibraryW(string lpFileName);
    }

    //private variables
    private static IntPtr _redemptionDllHandle = IntPtr.Zero;
    private static IntPtr _dllGetClassObjectPtr = IntPtr.Zero;
    private static DllGetClassObject _dllGetClassObject;
    private static readonly object _criticalSection = new object();

    private static IUnknown NewRedemptionObject(Guid guid)
    {
        //try to set the thread COM apartment
        //Thread.CurrentThread.ApartmentState = ApartmentState.STA;

        object res = null;

        try
        {
            lock (_criticalSection)
            {
                IClassFactory ClassFactory;
                if (_redemptionDllHandle.Equals(IntPtr.Zero))
                {
                    var dllPath = DllLocation32Bit;
                    if (IntPtr.Size == 8)
                        dllPath = DllLocation64Bit;

                    log.Trace($"Redemption is using {dllPath}");

                    _redemptionDllHandle = Win32NativeMethods.LoadLibraryW(dllPath);
                    if (_redemptionDllHandle.Equals(IntPtr.Zero))
                    {
                        log.Trace($"Could not load '{dllPath}' - Make sure the dll exists.");
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }
                    _dllGetClassObjectPtr = Win32NativeMethods.GetProcAddress(_redemptionDllHandle, "DllGetClassObject");
                    if (_dllGetClassObjectPtr.Equals(IntPtr.Zero))
                    {
                        log.Trace("Could not retrieve a pointer to the 'DllGetClassObject' function exported by the dll");
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }
                    _dllGetClassObject = (DllGetClassObject)Marshal.GetDelegateForFunctionPointer(_dllGetClassObjectPtr, typeof(DllGetClassObject));

                    try
                    {
                        log.Trace("Attempting register of Redemption COM");
                        Registrar.Register(dllPath);
                    }
                    catch (Exception e)
                    {
                        log.Trace(e);
                    }
                }

                Object unk;
                int hr = _dllGetClassObject(ref guid, ref IID_IClassFactory, out unk);
                if (hr != 0) throw new Exception("DllGetClassObject failed with error code 0x" + hr.ToString("x8"));
                ClassFactory = unk as IClassFactory;
                ClassFactory.CreateInstance(null, ref IID_IUnknown, out res);

                //If the same class factory is returned as the one still
                //referenced by .Net, the call will be marshalled to the original thread
                //where that class factory was retrieved first.
                //Make .Net forget these objects
                Marshal.ReleaseComObject(unk);
                Marshal.ReleaseComObject(ClassFactory);
            } //lock
        }
        catch (Exception e)
        {
            log.Trace($"Loading redemption failed 32 - {DllLocation32Bit}");
            log.Trace($"Loading redemption failed 64 - {DllLocation64Bit}");
            log.Trace(e);
            throw;
        }

        return (res as IUnknown);
    }

    #endregion

}