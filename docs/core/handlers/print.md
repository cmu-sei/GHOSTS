# Printing Configuration

The sample configuration below is also available in the [GHOSTS GitHub repository](<https://github.com/cmu-sei/GHOSTS/blob/master/src/Ghosts.Client.Windows/Sample%20Timelines/Print.json>

- Command is the printer to be used for printing (this must already be setup on the system).
- CommandArgs is the path to the file to be printed.

```json
{
    "TimeLineHandlers": [
        {
            "HandlerType": "Print",
            "Initial": "",
            "UtcTimeOn": "00:00:00",
            "UtcTimeOff": "24:00:00",
            "Loop": "False",
            "TimeLineEvents": [
                {
                    "Command": "MyPrinter",
                    "CommandArgs": [
                        "C:\\Temp\\print_test.txt"
                    ],
                    "DelayAfter": 900000,
                    "DelayBefore": 0
                }
            ]
        }
    ]
}
```
