# Clicks Handler Configuration

???+ info "Sample Configuration"
    The sample configuration below is also available in the [GHOSTS GitHub repository](https://github.com/cmu-sei/GHOSTS/blob/master/src/Ghosts.Client/Sample%20Timelines/clicks)

The following is the format for a basic timeline handler:

```json
{
    "TimeLineHandlers": [
        {
            "HandlerType": "Clicks",
            "Initial": "",
            "UtcTimeOn": "00:00:00",
            "UtcTimeOff": "24:00:00",
            "Loop": true,
            "TimeLineEvents": [
                {
                    "Command": "Clicks",
                    "CommandArgs": [],
                    "TrackableId": "",
                    "DelayAfter": 10000,
                    "DelayBefore": 0
                }
            ]
        }
    ]
}
```

In this example, "clicks" simply executes a mouse left-click every 10000 milliseconds. This is useful for keeping a computer awake, or because some security products monitor for this as an indication of computer activity, or for testing purposes.

GHOSTS does not currently have a method for clicking on a specific location on the screen or clicking specific buttons/alerts.
