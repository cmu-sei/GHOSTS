# GHOSTS Core Client Overview

???+ info "Additional configuration required for web browsing"
    For any client utilizing the FireFox or Chrome web browser, an automation driver is necessary to be included in the same folder as the GHOSTS binary. For Firefox, [download the appropriate 🦎&nbsp; Geckodriver for your version of the browser here](https://github.com/mozilla/geckodriver/releases) :material-open-in-new:. For Chrome, [download the appropriate Chromedriver for your version of the browser here](https://chromedriver.chromium.org/downloads) :material-open-in-new:.

???+ info "Additional configuration required for email"
    Using the Windows client email functions requires the use of [Redemption](http://www.dimastr.com/redemption/home.htm) :material-open-in-new: which provides robust Outlook automation.
    The full Redemption library of .dll files should be found in `/lib`.

???+ warning "Run as the NPC you're representing"
    The GHOSTS client should typically be run as a user, and not as root. Exercising teams will notice this immediately otherwise.

## The Windows Client

GHOSTS on Windows is a .NET Console application that orchestrates user behavior on client workstations - web browsing, command prompt, PowerShell, etc. Our team typically installs in an out-of-game-bounds directory (`c:\exercise\ghosts\`), where no event or injects will originate. This client has been tested on Win10 and Win7 boxes. It is recommended you verify one working client before deploying to multiple machines. You will need the base url from the API installation for the client to communicate home.

### Windows Installation

- Your client Windows machine will need to have (at least) the [Microsoft DotNet 4.6.1 runtime installed](https://dotnet.microsoft.com/download/dotnet-framework/net47) :material-open-in-new:. Again, note that you only need the runtime, not the full SDK.

- [Download 6.2.0 Win x64 (for use with Office x64)](https://cmu.box.com/s/3g8x4sowss1u4ngm4vy68obdsq4ir354)

- [Download 6.2.0 Win x32 (for use with Office x32)](https://cmu.box.com/s/ip8xuntjbsalarb4kblswyne1hhcfo4n)

Unzip to your client machine in a directory such as `c:\exercise\ghosts`. You will need to adjust configuration in `config/application.json` in order for your client to talk to your already installed API server above.

## Linux Client

Your client Linux machine will need to have the latest [Microsoft dotnetcore runtime](https://dotnet.microsoft.com/download) :material-open-in-new: installed (Note that for the GHOSTS client, there are versions for dotnetcoreapp3.1 - this will eventually go away — and dotnet6.0, which is LTS and should stick around for a while). Again, note that you only need the runtime installed, not the full SDK.

### Linux Installation

- [Download the current Linux client](https://cmu.box.com/s/onpe9b8dzw3ysk4h577wferfyhsrus1w)

Unzip to a folder such as `~/ghosts` for the user that you want GHOSTS to run as.

Note that on Linux machines running the client as root and utilizing web browsing may result in failures due to Gecko/Chromedriver display issues.

## Initial client configuration

### Client Directory Structure

- `config/` - configuration files are stored here.
- `instance/` - generated files and information relative to this particular installed instance of ghosts is stored here. **This folder should never be copied from one machine to another**
- `lib/` - third party libraries used by ghosts are stored here
- `logs/` - output logs for the installed instance (logs/app.log), and logs that are transferred to the server (`logs/clientupdates.log`)

### Configuration Files

#### application.json

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
    "Email": {
        "RecipientsToMin": 1,
        "RecipientsToMax": 3,
        "RecipientsCcMin": 0,
        "RecipientsCcMax": 2,
        "RecipientsBccMin": 2,
        "RecipientsBccMax": 2,
        "RecipientsOutsideMin": 0,
        "RecipientsOutsideMax": 1,
        "SetAccountFromConfig": false,
        "SetAccountFromLocal": false,
        "SetForcedSendReceive": false,
        "SaveToOutbox": false,
        "EmailDomainSearchString": "Get-ADUser -filter * -searchbase \"CN=USERS,DC=JRSS,DC=GOV\" -properties UserPrincipalName | select -expand UserPrincipalName"
    },
    "Listener": {
        "Port": 8443                                                        //local listener port on client to receive commands from C2 (C2 requires the same port number setting as the sender)
    },
    "EmailContent": {
        "conflict_1_capital": "",
        "conflict_1_name": "",
        "conflict_1_peoples": "",
        "conflict_1_president": "",
        "localized_flashpoint_locale": "",
        "friendly_nation_leader_lastname": "",
        "friendly_nation_leader_name": "",
        "friendly_nation_name": "",
        "friendly_nation_peoples": "",
        "commander_title": "",
        "commander_name": "",
        "commander_initials": "",
        "commander_lastname": "",
        "commander_email": "",
        "commander_sub1": "",
        "commander_sub2": "",
        "us_president": "",
        "iraq": "",
        "iraqi": "",
        "iran": "",
        "iranian": "",
        "china": "",
        "chinese": "",
        "russia": "",
        "azerbaijan": "",
        "turkish": "",
        "turkey": "",
        "pakistani": "",
        "pakistan": "",
        "palestinian": "",
        "palestine": "",
        "gaza": "",
        "korea": ""
    }
}
```

#### dictionary.json

Dictionary is a straightforward word list used to generate text within random Word documents.

```javascript
[
    "data",
    ...
]
```

#### email-content.csv

Email content is used to generate random email subject and bodies. Tags are substituted with values from the email settings in application.json above.

```"7475"|"ESPINOSA"|"From: <commander_email/> \nSent:  Sunday, March 21, 2017 3:57 PM \nTo: 'ValmoroU@mail.mil'; Huma Abedin \nSubject: Espinosa Am I supposed to call her tomorrow? "
"7477"|"LATIMES OP ED"|"From: <commander_sub1/> \nSent:  Monday, September 27, 2017 10:38 AM \nTo: \nSubject: Fw: LATimes op ed Attachments: 1ATimes - Why H. <commander_lastname/>
```

#### email-reply.csv

Email reply contains short text strings used in random responses.

#### emails-domain.json

Internal domain email addresses used to generate and send random messages.

#### emails-outside.json

"Company external" emails (and are mocks of major government contractor companies).

#### health.json

Health is the local checks a client undergoes periodically:

```javascript
{
    "Sleep": 600000,                //Sleep time between posting client results up to the API for each client.
    "CheckUrls": [                  //Urls that the client will GET request to confirm their internet connectivity
        "http://google.com",       
        "http://cmu.edu"
    ]
}
```

#### timeline.json

HandlerType: Command | BrowserChrome | Excel | PowerPoint | Word

Initial: The initial command for a handler to execute. For a web browser, you might enter either a url or "about:blank".

UtcTimeOn | UtcTimeOff: "00:00:00": "24:00:00" to not shut off. Otherwise, enter an on and off time to simulate things like, office hours of 9-5, etc. There is 30 minutes of jitter plus or minus from the time entered.

Loop: Set this to true in order to continue to execute this same command on a loop, false to execute something just one time.

TimeLineEvents (Other examples)

Command

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

#### Trackables

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
