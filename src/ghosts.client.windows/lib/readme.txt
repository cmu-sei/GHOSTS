To install Redemption either run install.exe (make sure that redemption.dll
is in the same directory) or run the following from the command line:

regsvr32.exe <fullpath>\redemption.dll

Redemption is a regular COM library and can be installed either with
regsvr32.exe or using your favorite installer (you will need to mark
redemption.dll as self-registering).

To use Redemption in.Net languages, add "Redemption" library to your project 
references from the COM tab of the References dialog in Visual Studio.
You can instead add a reference to the included Interop.Redemption.dll 
(strongly named and signed) instead of relying on the auto-generated interop dll.

You can dynamically load Redemption without installing it in the registry at all.
See the following URL for more details:
http://www.dimastr.com/redemption/security.htm#redemptionloader


To uninstall Redemption, run the following from the command line:
regsvr32.exe /u <fullpath>\redemption.dll

