# GHOSTS Client Overview

???+ info "GHOSTS Source Code"
    The [GHOSTS Source Code Repository](https://github.com/cmu-sei/GHOSTS) is hosted on GitHub.

The GHOSTS client simulates activities on a computer based on specific roles or memberships within a team. It creates documents, browses websites, downloads files, and interacts with various applications on Windows and Linux machines. GHOSTS is useful for cyber training, exercises, and testing scenarios.

![Types of NPCs](../../assets/img/npc-types.png)

GHOSTS can be used for training, testing, or generating datasets by simulating realistic computer and network activities.

???+ warning "Do I need the API?"
    The API server is optional but provides logging, reporting, and remote administration features. Without the API, client management must be handled through other means.

### Run as the NPC you're representing

The GHOSTS client should be run as a regular user, not as an administrator or root, to accurately simulate user behavior.

## The Windows Client

GHOSTS on Windows (supporting Win7, 10, 11, and corresponding server versions) is a .NET Console application that performs various user activities. Install the client in a directory like `c:\exercise\ghosts\`. Verify one client before deploying to multiple machines and configure it with the API base URL.

### Windows Installation

- Ensure the [Microsoft DotNet 4.6.1 runtime](https://dotnet.microsoft.com/download/dotnet-framework/net47) is installed.
- [Download the latest client](https://github.com/cmu-sei/GHOSTS/releases/latest) and unzip it to `c:\exercise\ghosts`.
- Adjust `config/application.json` to point to your API server.

???+ info "Additional configuration required for web browsing"
    For Firefox or Chrome web browsing, download the appropriate automation driver: [Geckodriver](https://github.com/mozilla/geckodriver/releases) for Firefox or [Chromedriver](https://chromedriver.chromium.org/downloads) for Chrome.

???+ info "Additional configuration required for email"
    Email functions require the [Redemption library](http://www.dimastr.com/redemption/home.htm) found in `/lib`.

## Linux Client

GHOSTS on Linux requires the [Microsoft dotnetcore runtime version 8.0](https://dotnet.microsoft.com/download). We have tested with Ubuntu 24.04, but other distributions should work.

### Linux Installation

- [Download the latest Linux client](https://github.com/cmu-sei/GHOSTS/releases/latest) and unzip it to a folder such as `~/ghosts`.
- Running the client as root may cause display issues with web browsers.

## Client Directory Structure

???+ danger "Do not copy the instance folder"
    The `instance` folder should not be copied between machines.

| Folder          | Description                            |
| --------------- | ---------------------------------------|
| `config/`       | Configuration files are stored here. |
| `instance/`     | Stores files and information specific to the instance. **Do not copy this folder** |
| `lib/`          | Third-party libraries used by GHOSTS |
| `logs/`         | Output logs for the client and logs transferred to the server |

## Configuration Quick Start

After unzipping, run the client by double-clicking it. The console window may briefly appear and then disappear. To verify, check Windows Task Manager or use the `kill-ghosts.bat` script to close it.

### application.json

Adjust the API URLs in `application.json` to point to your server:

```json
{
  "ApiRootUrl": "http://localhost:5000/api",
  "Sockets": {
    "IsEnabled": true,
    "Heartbeat": 50000
  },
  "Id": {
    "IsEnabled": true,
    "Format": "guestlocal",
    "FormatKey": "guestinfo.id",
    "FormatValue": "$formatkeyvalue$-$machinename$",
    "VMWareToolsLocation": "C:\\progra~1\\VMware\\VMware Tools\\vmtoolsd.exe"
  },
  "AllowMultipleInstances": false,
  "EncodeHeaders": true,
  "ClientResults": {
    "IsEnabled": true,
    "IsSecure": false,
    "CycleSleep": 300000
  },
  "ClientUpdates": {
    "IsEnabled": true,
    "CycleSleep": 300000
  },
  "Survey": {
    "IsEnabled": false,
    "IsSecure": false,
    "Frequency": "once",
    "CycleSleepMinutes": 5,
    "OutputFormat": "indent"
  },
  "Timeline": {
    "Location": "config/timeline.json"
  },
  "Content": {
    "EmailsMax": 20,
    "EmailContent": "",
    "EmailReply": "",
    "EmailDomain": "",
    "EmailOutside": "",
    "BlogContent": "",
    "BlogReply": "",
    "FileNames": "",
    "Dictionary": ""
  },
  "ResourceControl": {
    "ManageProcesses": true
  },
  "HealthIsEnabled": false,
  "HandlersIsEnabled": true,
  "DisableStartup": false
}
```

### timeline.json

The `timeline.json` file controls client activities. Example configurations include:

- **Command Execution:**

```json
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

- **Browser Configuration:**

```json
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
            "http://facebook.com"
         ],
         "DelayAfter": 1000,
         "DelayBefore": 0
      }
   ]
}
```

- **Document Creation:**

```json
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

Use Trackables to monitor specific Timeline Events:

```json
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

> Clients aren't running (immediately exiting, throwing exceptions, etc.)

- Ensure the .NET Framework runtime 4.x is installed.
- Check if `ghosts.exe` is white-listed by GPO.
- Verify the client runs when double-clicked.
- Review Windows application event logs and `logs/app.log`.
- Confirm the executable is set to run at startup.
- Check `nlog.config` for these lines:

```xml
<AutoLoadExtensions="true"/>
<internalLogToConsole="true"/>
<internalLogFile="logs\nlog-internal.log"/>
<internalLogLevel="Error"/>
```

> Clients aren't reporting their activity to the API

- Confirm the client is running correctly.
- Check `logs/clientupdates.log` for entries and size.
- Ensure the `instance` folder and `instance/id.json` exist.
- Review `logs/app.log` for fatal errors.

> Can I update what clients are doing?

- Modify `config/timeline.json` directly.
- Use the `instance/timeline/in`

 folder for just-in-time activities.

> Can I reset a client on a box?

- Launching a new GHOSTS instance will kill the previous one.
- Use `kill-ghosts.bat` to clean up all tasks.

> What is the easiest way to determine the running version of the client?

- Run the version flag: `ghosts.exe --version`
