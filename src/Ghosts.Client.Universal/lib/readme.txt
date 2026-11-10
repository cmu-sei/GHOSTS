The redemption libraries are included here for use with the Outlookv2.cs
handler which does automated email via Outlook.

To register these, do:

regsvr32.exe <fullpath>\redemption.dll
regsvr32.exe <fullpath>\redemption64.dll

The redemption.dll is needed if you are using a x86 Ghosts + Outlook 2013 (32-bit).
The redemption64.dll is needed for x64 Ghosts + Outlook later than 2013 (like 2016, 2019).
It does not hurt to register both of them.

The Outlookv2 handler uses Redemption via:
using Microsoft.Office.Interop.Outlook;
using Redemption;

and there is a reference to lib\Interop.Redemption.dll in the Ghosts.Client.Univeral.csproj file.

The Interop.AutoItX3Lib.dll is used by the Rdp.cs to automate entering of credentials
after successful RDP connection, to automate mouse movement after connection.

There is a reference to the AutoItX3lib.dll in the Ghosts.Client.Univeral.csproj file
and the AutoItX3.dll, AutoItX3_x64.dll files will be automatically added during the build
process to the binary distribution directory. The AutoItX3.dll, and AutoItX4_64.dll should also
be registered during your installation process.

To use the OutlookV2.cs and and Rdp.cs windows capabilities, the dotnet build needs to 
target the 10.0-windows framework.  The Ghosts.Client.Univeral.csproj file supports targeting
either the 10.0 framework or 10.0-windows framework.

A self-contained build be create using the following command:

64bit build:
dotnet publish -f net11.0-windows -c Release -r win-x64 --self-contained true

x86 build (32-bit build):
dotnet publish -f net11.0-windows -c Release -r win-x86 --self-contained true

Obviously, the net11.0-windows builds will only work on Windows platforms.

The Outlookv2 and Rdp handlers will build using the "net11.0" framework but they will not function
as expected.