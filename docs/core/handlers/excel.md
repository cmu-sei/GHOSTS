# Excel Configuration

???+ info "Sample Configuration"
    The sample configuration below is also available in the [GHOSTS GitHub repository](<https://github.com/cmu-sei/GHOSTS/blob/master/src/Ghosts.Client/Sample%20Timelines/clicks>

```json
{
    "TimeLineHandlers": [
      {
        "HandlerType": "Excel",
        "Initial": "",
        "UtcTimeOn": "00:00:00",
        "UtcTimeOff": "24:00:00",
        "HandlerArgs": {
          "workingset": {
            "max": 20,
            "max-age-in-hours": 72
          }
        },
        "Loop": true,
        "TimeLineEvents": [
          {
            "Command": "create",
            "CommandArgs": [ "%homedrive%%homepath%\\Documents", "pdf", "pdf-vary-filenames", "save-array:['c:\\tmp','c:\\tmp\\path2','c:\\tmp\\path3']" ],
            "DelayAfter": 900000,
            "DelayBefore": 0
          }
        ]
      }
    ]
}
```

Some of the key-value pairs are self-explanatory, but let's review a few important ones:

| Key                     | Value                                                                                                                                     |
| ---------------         | ---------------------------------------                                                                                                   |
| `workingset`            | For the documents Excel creates, how many should we create (max) and how old can they be before we start creating new ones (max-age-in-hours). |
| `Command`               | (create) Create new documents. This is currently the only setting, but there is space for new commands to be built in the future          |
| `CommandArgs`           | [0] The path to the folder where the documents should be created. [1] Also periodically export documents as PDFs as well as saving them natively. [2] if creating PDFs, vary their name from the original document. [3] Randomly save documents in an array of locations. |
