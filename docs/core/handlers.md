# Basic Handler Configuration

The following is the format for a basic timeline handler:

```json
"TimeLineHandlers": [
    {
      "HandlerType": "Watcher",
      "UtcTimeOn": "00:00:00",
      "UtcTimeOff": "24:00:00",
      "Loop": true,
      "TimeLineEvents": [
        {
          "Command": "folder",
          "CommandArgs": [ "path:%HOMEDRIVE%%HOMEPATH%\\Downloads", "size:2000", "deletionApproach:oldest" ],
          "DelayAfter": 0,
          "DelayBefore": 0
        }
      ]
    }
]
```

Some of the key-value pairs are self-explanatory, but let's review a few important ones:

| Key                     | Value                                                                                                                                     |
| ---------------         | ---------------------------------------                                                                                                   |
| `HandlerType`           | The application or major function we want to control. This could be FireFox, the Cmd terminal, or Word.                                   |
| `UtcTimeOn`             | The time the handler will begin to run. To simulate an agent coming into the office at 0900, we set this to your time zone's UTC value.   |
| `UtcTimeOff`            | The time the handler will stop. To simulate an agent leaving the office at 1700, we set this to your time zone's UTC value.               |
| `Loop`                  | true or false - a handler could be repeated or just run one-time.                                                                            |
