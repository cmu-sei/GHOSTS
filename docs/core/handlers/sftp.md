# Secure File Transfer Protocol (sFTP) Configuration

???+ info "Sample Configuration"
    The sample configuration below is also available in the [GHOSTS GitHub repository](<https://github.com/cmu-sei/GHOSTS/blob/master/src/Ghosts.Client/Sample%20Timelines/clicks>

Each CommandArg is of the formation shown below, if multiple CommandArgs are present a random one is chosen for execution on each cycle.

- Credential handling is done in the same manner as the SSH handler, see that sample timeline for documentation
- After the `cred_key` is a ';' delimited list of SFTP commands that are executed in sequence during a cycle.
- Downloaded files are placed in the user's default downloads directory

Supported commands:

- get [remotefile] - downloads random remote file from remote host. Can specify absolute/relative path instead of [remotefile]
- put [localfile] - uploads random remote file from local upload directory to remote host. Can specify absolute/relative path instead of [localfile]
- cd [remotedir] - change to random directory in current directory on remote host. Can specify absolute/relative path instead of [remotedir]
- rm [remotefile] - deletes random remote file from remote host. Can specify absolute/relative path instead of [remotefile]
- ls [remotedir] - list remote contents of current directory, if no directory specified use current directory. Can specify absolute/relative path instead of [remotedir]
- mkdir [randomname] - make a random directory in cwd on remote host. Can specify absolute/relative path instead of [randomname]

```json
{
  "Status": "Run",
  "TimeLineHandlers": [
    {
      "HandlerType": "Sftp",
      "HandlerArgs": {
        "TimeBetweenCommandsMax": 5000, //max,min between individual SFTP commands
        "TimeBetweenCommandsMin": 1000,
        "CredentialsFile": "<path to credentials>", //required, file path to a JSON file containing the SSH credentials
        "UploadDirectory": "<path to uploads directory>", //optional, directory that contains files for upload, it not specified user Downloads directory is used
        "delay-jitter": 0 //optional, default =0, range 0 to 50, if specified, DelayAfter varied by delay-%jitter*delay to delay+%jitter*delay
      },
      "Initial": "",
      "UtcTimeOn": "00:00:00",
      "UtcTimeOff": "24:00:00",
      "Loop": "True",
      "TimeLineEvents": [
        {
          "Command": "random",
          "CommandArgs": [
            "<someIp>|<credKey>|<a_cmd>;<a_cmd>;<a_cmd>....;<a_cmd>"
          ],
          "DelayAfter": 20000,
          "DelayBefore": 0
        }
      ]
    } 

  ]
}
```
