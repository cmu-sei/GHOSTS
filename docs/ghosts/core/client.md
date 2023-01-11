# GHOSTS Core Client Overview

???+ info "GHOSTS Source Code"
    The [GHOSTS Source Code Repository](https://github.com/cmu-sei/GHOSTS) is hosted on GitHub.

The GHOSTS client simulates what anyone might do at a computer given their particular role or membership within some team. It creates documents, browses websites, downloads files, and uses all sorts of popular applications on many versions of Windows and Linux machines. Whether you're a friendly administrator or a powerful cyber adversary, GHOSTS can replicate your expected behavior.

![Types of NPCs](../../assets/img/npc-types.png)

GHOSTS has many use cases in cyber training and exercises, most notably for bringing non-player characters (NPCs) to life, but GHOSTS can be used for many other purposes where realistic activity on a computer or network is needed as well - testing or generating datasets, for example.

???+ warning "Do I need the API?"
    Although clients are fully functional without the API server, the latter enables logging, reports, and remote administration capabilities. Without the API, changes to clients must be managed in some other manner.

???+ warning "Run as the NPC you're representing"
    The GHOSTS client should typically be run as a user, and not as root. Training or exercising teams will notice this immediately otherwise.

## The Windows Client

GHOSTS on Windows is a .NET Console application that performs user activity on client workstations (Win10 and Win7 are currently supported) - web browsing, working with office documents, using the command prompt or PowerShell, etc. Our team typically installs in an out-of-game directory (`c:\exercise\ghosts\`), where no event or injects will originate. It is recommended you verify one working client before deploying to multiple machines. You will need the base URL from the API installation for the client to communicate home.

???+ info "Additional configuration required for web browsing"
    For any client utilizing the FireFox or Chrome web browser, an automation driver is necessary to be included in the same folder as the GHOSTS binary. For Firefox, [download the appropriate 🦎&nbsp; Geckodriver for your version of the browser here](https://github.com/mozilla/geckodriver/releases) :material-open-in-new:. For Chrome, [download the appropriate Chromedriver for your version of the browser here](https://chromedriver.chromium.org/downloads) :material-open-in-new:.

???+ info "Additional configuration required for email"
    Using the Windows client email functions requires the use of [Redemption](http://www.dimastr.com/redemption/home.htm) :material-open-in-new: which provides robust Outlook automation.
    The full Redemption library of .dll files should be found in `/lib`.

### Windows Installation

- Your client Windows machine will need to have (at least) the [Microsoft DotNet 4.6.1 runtime installed](https://dotnet.microsoft.com/download/dotnet-framework/net47) :material-open-in-new:. Again, note that you only need the runtime, not the full SDK.

- [Download 6.2.0 Win x64 (for use with Office x64)](https://cmu.box.com/s/3g8x4sowss1u4ngm4vy68obdsq4ir354)

- [Download 6.2.0 Win x32 (for use with Office x32)](https://cmu.box.com/s/ip8xuntjbsalarb4kblswyne1hhcfo4n)

Unzip to your client machine in a directory such as `c:\exercise\ghosts`. You will need to adjust configuration in `config/application.json` in order for your client to talk to your already installed API server above.

## Linux Client

Your client Linux machine will need to have the latest [Microsoft dotnetcore runtime](https://dotnet.microsoft.com/download) :material-open-in-new: installed (Note that for the GHOSTS client, there are versions for dotnetcoreapp3.1 - this will eventually go away — and dotnet6.0, which is long term support (LTS) and will stick around for a while). Again, note that you only need the runtime installed, not the full SDK.

### Linux Installation

- [Download the current Linux client](https://cmu.box.com/s/onpe9b8dzw3ysk4h577wferfyhsrus1w)

Unzip to a folder such as `~/ghosts` for the user that you want GHOSTS to run as.

Note that on Linux machines running the client as root and utilizing web browsing may result in failures due to Gecko/Chromedriver display issues.

## Client Directory Structure

- `config/` - configuration files are stored here.
- `instance/` - generated files and information relative to this particular installed instance of ghosts is stored here. **This folder should never be copied from one machine to another**
- `lib/` - third party libraries used by ghosts are stored here
- `logs/` - output logs for the installed instance (logs/app.log), and logs that are transferred to the server (`logs/clientupdates.log`)

???+ danger "Do not copy the instance folder"
    You should never copy the `instance` folder from one machine to another.

## Configuration Quick Start

To get the client running quickly, there are just two files that we might need to adjust:

### application.json

In this file, often all we need to change are the URLs for the API, IdUrl, ClientResultsUrl, ClientUpdatesUrl, and the like. Change the hostname to your installed API location, and GHOSTS should check in as expected.

```javascript
{
    "IdEnabled": true,                                                      //enabled in order to command and control from api (C2) server
    "IdUrl": "http://yourapiurl.com/api/clientid",                          //url for API endpoint to get clientid
    "ClientResultsEnabled": true,                                           //enabled to report results to C2
    "ClientResultsUrl": "http://yourapiurl.com/api/clientresults",          //url for API endpoint to report results
    "ClientResultsCycleSleep": 90,                                          //report results every x ms
    "ClientUpdatesEnabled": true,                                           //enabled to get updates from C2
    "ClientUpdatesUrl": "http://yourapiurl.com/api/clientupdates",          //url for API endpoint to get updates
    "ClientUpdatesCycleSleep": 90,                                          //check for updates every x ms
    "Survey": {                                                             //survey is a local report of processes running, etc.
        "IsEnabled": true,                                                  //on/off
        "Frequency": "once",                                                //how often to survey
        "MaxAgeInHours": 168,                                               //how long to wait until new survey
        "OutputFormat": "indent"                                            //compact/fancy(indent)
    },
    "HealthIsEnabled": true,                                                //enable local health checks
    "HandlersIsEnabled": true,                                              //enable local timeline activity
    "ChromeExtensions": "",                                                 //comma separated local extensions (used for injects in the past)
    "FirefoxInstallLocation": "",                                           //geckodriver needs this for non-standard installs (is pesky)
    "FirefoxMajorVersionMinimum": 48,                                       //geckodriver is picky about versions
    "OfficeDocsMaxAgeInHours": 6,                                           //cleanup kills docs in the documents folder older than this setting
}
```

### timeline.json

The other file we may want to adjust is the default timeline. This is what the agent does all day, including browsing the internet, creating documents, and similar. The defaults hopefully give you a good idea of what is possible, and of course, the array of configurations here is endless - be creative!

The primary item is the HandlerType. This tells GHOSTS to run a command (Command), use Firefox to browse an array of websites (BrowserFirefox), create Excel documents (Excel)  and so on. Some of the other items related to a handler's configuration are:

- Initial: The initial command for a handler to execute. For a web browser, you might enter either a URL or "about:blank".
- UtcTimeOn | UtcTimeOff: "00:00:00": "24:00:00" to not shut off. Otherwise, enter an on and an off time to simulate things such as office hours of 9-5, etc. There are 30 minutes of jitter plus or minus from the time entered.
- Loop: Set this to true to continue to execute this same command on a loop, or false to execute something just one time.

```javascript
{
   "HandlerType": "Command",
   "Initial": "",
   "UtcTimeOn": "00:00:00",
   "UtcTimeOff": "24:00:00",
   "Loop": "True",
   "TimeLineEvents": [
      {
         "Command": "NETSTAT",    
         "CommandArgs": [],
         "DelayAfter": 900000,
         "DelayBefore": 0
      }
   ]
}
```

To access a network share file, the command might be: net use X:\\SERVER\Share

To RDP to another machine: mstsc.exe {ConnectionFile | /v:ServerName[:Port]} [/console] [/f] [/w:Width/h:Height]

`/v` - specifies the remote computer and port (optional) you wish to connect to
`/console` – connects to the console of a Windows Server 2003 based system
`/f` – starts the remote desktop connection in full screen mode
`/w & /h` – specifies the width and height of the remote desktop connection

Actions can also be created for standard copy/move/deletion of files via their respective commands.

Chrome

We have to pass the browser window an initial value. If we don't want it to go anywhere at start, we could pass about:blank, otherwise we'd pass a url. These can be http or https.

```javascript
{
   "HandlerType": "BrowserChrome",
   "Initial": "http://google.com",
   "UtcTimeOn": "00:00:00",
   "UtcTimeOff": "24:00:00",
   "Loop": "True",
   "TimeLineEvents": [
      {
         "Command": "random",
         "CommandArgs": [
            "http://google.com",
            "http://facebook.com",
         ],
         "DelayAfter": 1000,
         "DelayBefore": 0
      }
   ]
}
```

Excel, PowerPoint, Word

```javascript
{
   "HandlerType": "Word",
   "Initial": "",
   "UtcTimeOn": "00:00:00",
   "UtcTimeOff": "24:00:00",
   "Loop": "True",
   "TimeLineEvents": [
      {
         "Command": "create",
         "CommandArgs": [ "%homedrive%%homepath%\\Documents" ],
         "DelayAfter": 900000,
         "DelayBefore": 0
      }
   ]
}
```

## Trackables

For specific Timeline Events where the outcome is needed to be tracked, like for example, a client machine spawned inject, use a Trackable (via TrackableId in the following example):

```javascript
{
    "TimeLineHandlers": [
        {
            "HandlerType": "BrowserChrome",
            "Initial": "about:blank",
            "UtcTimeOn": "00:00:00",
            "UtcTimeOff": "24:00:00",
            "Loop": false,
            "TimeLineEvents": [
                {
                    "Command": "browse",
                    "CommandArgs": [ "https://dl.dafont.com/dl/?f=italian_breakfast" ],
                    "DelayAfter": 0,
                    "DelayBefore": 0
                },
                {
                    "Command": "download",
                    "CommandArgs": [ "//a[contains(@class, 'dl')]" ],
                    "TrackableId": "<guid id from trackables table/>",
                    "DelayAfter": 0,
                    "DelayBefore": 0
                }
            ]
        },
        {
            "HandlerType": "Command",
            "Initial": "",
            "UtcTimeOn": "00:00:00",
            "UtcTimeOff": "24:00:00",
            "Loop": false,
            "TimeLineEvents": [
                {
                    "Command": "cd %homedrive%%homepath%\\Downloads",
                    "CommandArgs": [
                        "powershell expand-archive -Path italian_breakfast.zip -destinationpath x",
                        "cd x",
                        "dir"
                    ],
                    "TrackableId": "<guid id from trackables table/>",
                    "DelayAfter": 10,
                    "DelayBefore": 10000
                }
            ]
        }
    ]
}
```

## Troubleshooting

> Clients aren't running (immediately exiting, throwing copious exceptions, or similar)

- Is the dotnet framework runtime 4.x installed on the machine?
- If GPO is doing white-listing of what apps can run, is `ghosts.exe` white-listed?
- Will the client run by simply double-clicking on the exe?
- Does it report anything to the windows application event logs?
- What's in `logs/app.log`?
- Is the ghosts executable set to execute automatically when the machine restarts?

> Clients aren't reporting their activity to the API

- Is the client running correctly? (if not, see above)
- Is there entries in the logs/clientupdates.log?
- If there are, is the file too large? (Try removing it, ghosts might be hung trying to process a lot of log data)
- If the folder `instance` created? Does the file `instance/id.json` exist? (If it does and has an ID within, then ghosts has reported home to the api at least once)
- The file `logs/app.log` indicating any fatal issues? (Logging can be ratcheted up and down via nlog configuration)

> Can I update what clients are doing?

- Clients operate off their `config/timeline.json` file and this can be updated via Powershell, Ansible, or other means - it's just a file.
- Clients can also do just-in-time activities via the `instance/timeline/in` folder. Anything placed here will be picked up, executed, and moved to the corresponding out folder once complete. This does not affect any activity currently controlled with the default timeline file.

> Can I reset a client on a box?

- Yes, launching a new instance of Ghosts kills the previous one and all associated tasks from the timeline (any instances of Word, PowerShell, etc.). Only one instance of Ghosts will be running on a client box at any time.

> What is the easiest way to determine the running version of the client?

- run the version flag: `ghosts.exe --version`
