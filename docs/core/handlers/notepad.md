# Notepad Configuration

???+ info "Sample Configuration"
    The sample configuration below is also available in the [GHOSTS GitHub repository](<https://github.com/cmu-sei/GHOSTS/blob/master/src/Ghosts.Client/Sample%20Timelines/clicks>

There is currently only one supported command (random) which uses probabilities to write text.

During each activity cycle the handler will:

- Choose action as either delete, create, modify, or view
- if delete, then delete random file from output-directory
- if create, open notepad, add random text, save as random new file to output-directory
- if view, open notepad with existing file from input-directory
- if modify, open notepad with existing file from output-directory, add random text, save
- if create or modify, roll against pdf-probability and do a print to pdf if roll succeeds

For this to work, the default printer must be print to pdf - there is curently no checking is done if the correct printer is not selected.

At end of activity cycle, Notepad is closed.

CommandArgs from Timeline events is not used.


```json
{
  "Status": "Run",
  "TimeLineHandlers": [
    {
      "HandlerType": "Notepad",
      "HandlerArgs": {
        "execution-probability": 100,
        "deletion-probability": 0,
        "view-probability": 0,
        "creation-probability": 0,
        "modification-probability": 100,
        "pdf-probability": 100,
        "input-directory": "C:\\ghosts_data\\uploads",
        "output-directory": "C:\\ghosts_data\\uploads",
        "text-generation": "random",
        "min-paragraphs": 4,
        "max-paragraphs": 15,
        "delay-jitter": 50
      },
      "Initial": "",
      "UtcTimeOn": "00:00:00",
      "UtcTimeOff": "24:00:00",
      "Loop": "True",
      "TimeLineEvents": [
        {
          "Command": "random",
          "CommandArgs": [
          ],
          "DelayAfter": 20000,
          "DelayBefore": 0
        }
      ]
    }

  ]
}
```
