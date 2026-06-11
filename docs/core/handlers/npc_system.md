# NPC System Configuration

The sample configuration below is also available in the [GHOSTS GitHub repository](<https://github.com/cmu-sei/GHOSTS/blob/master/src/Ghosts.Client.Windows/Sample%20Timelines/NpcSystem.json>

This is currently only used to turn the client on and off (where the client binary still runs, but does nothing).  It is not used to control the client's behavior as other handlers might do.

```json
{
    "TimeLineHandlers": [
        {
            "HandlerType": "NpcSystem",
            "Initial": "",
            "UtcTimeOn": "00:00:00",
            "UtcTimeOff": "24:00:00",
            "Loop": false,
            "TimeLineEvents": [
                {
                    "Command": "Stop",
                    "CommandArgs": [],
                    "DelayAfter": 0,
                    "DelayBefore": 0
                }
            ]
        },
       {
            "HandlerType": "NpcSystem",
            "Initial": "",
            "UtcTimeOn": "00:00:00",
            "UtcTimeOff": "24:00:00",
            "Loop": false,
            "TimeLineEvents": [
                {
                    "Command": "Start",
                    "CommandArgs": [],
                    "DelayAfter": 0,
                    "DelayBefore": 0
                }
            ]
        }
    ]
}

```