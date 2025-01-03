# File Watcher Configuration

???+ info "Sample Configuration"
    The sample configuration below is also available in the [GHOSTS GitHub repository](<https://github.com/cmu-sei/GHOSTS/blob/master/src/Ghosts.Client/Sample%20Timelines/clicks>

The 'folder' command for Watcher is intended to monitor diskspace in a target folder. The CommandArgs are in key:value pairs:

- path:`some folder`  - folder to watch
- size:`max size in MB` - maximum folder size in MB
- deletionApproach:oldest|largest|random

 If max size is exceeded, then files are deleted from the folder using the deletionApproach until the folder size is under max size.
 One obvious use of this is to monitor the browser downloads directory, i.e. path:%HOMEDRIVE%%HOMEPATH%\\Downloads. The deletion action can result in deleting a file that is in the progress of being downloaded which results in a failed download by the browser for this file. But since the goal is traffic generation, this is not a deal breaker.  


```json
{
  "TimeLineHandlers": [
    {
      "HandlerType": "Watcher",
      "Initial": "",
      "UtcTimeOn": "00:00:00",
      "UtcTimeOff": "24:00:00",
      "Loop": true,
      "TimeLineEvents": [
        {
          "Command": "file",
          "CommandArgs": [ "C:\\Temp\\test.txt", "300000" ],
          "DelayAfter": 0,
          "DelayBefore": 0
        }
      ]
    },
    {
      "HandlerType": "Watcher",
      "Initial": "",
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
}
```
