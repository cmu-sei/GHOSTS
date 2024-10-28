using System.Diagnostics;
using System.Reflection;

namespace Socializer.Infrastructure;

public static class ApplicationDetails
{
    public static string Header =>
        @"             ('-. .-.               .-')    .-') _     .-')    
            ( OO )  /              ( OO ). (  OO) )   ( OO ).  
  ,----.    ,--. ,--. .-'),-----. (_)---\_)/     '._ (_)---\_) 
 '  .-./-') |  | |  |( OO'  .-.  '/    _ | |'--...__)/    _ |  
 |  |_( O- )|   .|  |/   |  | |  |\  :` `. '--.  .--'\  :` `.  
 |  | .--, \|       |\_) |  |\|  | '..`''.)   |  |    '..`''.) 
(|  | '. (_/|  .-.  |  \ |  | |  |.-._)   \   |  |   .-._)   \ 
 |  '--'  | |  | |  |   `'  '-'  '\       /   |  |   \       / 
  `------'  `--' `--'     `-----'  `-----'    `--'    `-----'  ";

    public static string Version
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version != null ? version.ToString() : "";
        }
    }

    public static string VersionFile
    {
        get
        {
            var fileName = Assembly.GetEntryAssembly()?.Location;
            return (fileName != null ? FileVersionInfo.GetVersionInfo(fileName).FileVersion : "") ?? string.Empty;
        }
    }
}
