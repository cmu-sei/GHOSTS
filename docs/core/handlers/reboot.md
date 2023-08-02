# Reboot Configuration

???+ info "Sample Configuration"
    The sample configuration below is also available in the [GHOSTS GitHub repository](<https://github.com/cmu-sei/GHOSTS/blob/master/src/Ghosts.Client/Sample%20Timelines/clicks>

This is the only configuration possible for reboots currently. A fast loop configuration is probably not recommended, but once a day or similar is reasonable.

```json
{
    "TimeLineHandlers": [
        {
            "HandlerType": "Reboot",
            "Initial": "",
            "UtcTimeOn": "00:00:00",
            "UtcTimeOff": "24:00:00",
            "Loop": false,
            "TimeLineEvents": [
                {
                    "Command": "",
                    "CommandArgs": [ ],
                    "DelayAfter": 900000,
                    "DelayBefore": 0
                }
            ]
        }
    ]
}
```
