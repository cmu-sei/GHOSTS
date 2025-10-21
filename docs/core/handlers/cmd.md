# CMD Handler Configuration

???+ info "Sample Configuration"
    Sample command handler configurations are available in the [GHOSTS GitHub repository](https://github.com/cmu-sei/GHOSTS/tree/master/src/Ghosts.Client/Sample%20Timelines)

The CMD handler allows you to execute command-line operations as part of your NPC timeline. This can include running PowerShell scripts, batch files, or any command-line utilities available on the system.

## Basic Configuration Format

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

## How It Works

In this example, a command window is launched with the following sequence:

1. Navigate to the user's Downloads folder (`cd %homedrive%%homepath%\\Downloads`)
2. Extract a zip file using PowerShell
3. Navigate into the extracted folder
4. List the contents of the folder

The `CommandArgs` array allows you to execute multiple commands sequentially.

## Command Window Behavior

By default, the command window closes after execution. To keep it open, use the `/k` flag:

```json
{
    "Command": "cmd /k cd %homedrive%%homepath%\\Downloads",
    "CommandArgs": [
        "powershell expand-archive -Path italian_breakfast.zip -destinationpath x",
        "cd x",
        "dir"
    ],
    "DelayAfter": 10,
    "DelayBefore": 10000
}
```

The `/k` flag keeps the command window open after executing the commands, allowing for more realistic simulation of user behavior.

## Environment Variables

You can use Windows environment variables in your commands:

- `%HOMEDRIVE%` - User's home drive (typically `C:`)
- `%HOMEPATH%` - User's home path
- `%USERNAME%` - Current username
- `%TEMP%` - Temporary files directory
- `%PROGRAMFILES%` - Program Files directory

## Common Use Cases

**Execute a PowerShell Script:**
```json
{
    "Command": "powershell",
    "CommandArgs": [
        "-ExecutionPolicy Bypass",
        "-File C:\\scripts\\my-script.ps1"
    ]
}
```

**Run System Utilities:**
```json
{
    "Command": "ipconfig",
    "CommandArgs": ["/all"]
}
```

**File Operations:**
```json
{
    "Command": "xcopy",
    "CommandArgs": [
        "%HOMEDRIVE%%HOMEPATH%\\Documents\\*.txt",
        "%HOMEDRIVE%%HOMEPATH%\\Backup\\",
        "/Y"
    ]
}
```

## Best Practices

- Use environment variables instead of hardcoded paths for better portability
- Keep command windows open (`/k`) during active work hours for realism
- Use `DelayBefore` and `DelayAfter` to simulate realistic user pacing
- Combine with `TrackableId` when you need to verify specific commands were executed
