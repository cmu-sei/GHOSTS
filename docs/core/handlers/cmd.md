# CMD Handler Configuration

???+ info "Sample Configuration"
    The sample configuration below is also available in the [GHOSTS GitHub repository](<https://github.com/cmu-sei/GHOSTS/blob/master/src/Ghosts.Client/Sample%20Timelines/clicks>

The following is the format for a basic timeline handler:

```json
{
    "TimeLineHandlers": [
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

In this example, the command window is launched, and the initial command of moving into the current user's Downloads folder is executed. Then, the command window is used to extract the contents of a zip file, move into the extracted folder, and list the contents of the folder. Thus, CommandArgs can be used to execute any number of commands in sequence.

Note that the command window is not closed after the commands are executed. This is because the command window is launched in a separate process, and the process is terminated after the commands are executed. If you want to keep the command window open, you can use the following configuration:

```json
{
    "Command": "cmd /k cd %homedrive%%homepath%\\Downloads",
    "CommandArgs": [
        "powershell expand-archive -Path italian_breakfast.zip -destinationpath x",
        "cd x",
        "dir"
    ],
    "TrackableId": "<guid id from trackables table/>",
    "DelayAfter": 10,
    "DelayBefore": 10000
}
```
